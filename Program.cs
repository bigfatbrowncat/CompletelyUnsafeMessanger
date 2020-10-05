using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace CompletelyUnsafeMessenger
{
    class Program
    {
        /// <summary>
        /// The application's version info printed everywhere
        /// </summary>
        static readonly FileVersionInfo applicationVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetCallingAssembly().Location);

        /// <summary>
        /// The application entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns><c>0</c> in case of success. Otherwise - fail.</returns>
        static int Main(string[] args)
        {
            try
            {
                // Global configuration
                Console.OutputEncoding = Encoding.UTF8;

                // Welcome
                Console.WriteLine(applicationVersionInfo.ProductName + " v" + applicationVersionInfo.ProductVersion);
                Console.WriteLine();
                if (args.Length == 0)
                {
                    Console.WriteLine("Run the application with -h or --help option to list all the command line parameters");
                }

                // Processing the command line
                string appName = Assembly.GetExecutingAssembly().GetName().Name;

                bool showHelpAndExit = false;
                short port = 8080;
                string addr = "+";
                string deskFileName = "desk.json";
                string dataFilesRoot = "data";
                string cacheFilesRoot = "cache";
                bool rebuildImageCache = false;

                var p = new OptionSet();
                p.Add<short>("p|port=", "the port number to listen to (8080 by default)", v => port = v);
                p.Add<string>("a|addr=", "the address to listen to ('+' by default)", v => addr = v);
                p.Add<string>("d|desk=", "desk file (desk.json by default)", v => deskFileName = v);
                p.Add<string>("dr|data_root=", "data files root ('data' by default)", v => dataFilesRoot = v);
                p.Add<string>("cr|cache_root=", "cache files root ('cache' by default)", v => cacheFilesRoot = v);
                p.Add("ric|rebuild_image_cache", "rebuild caches for all images (false by default)", v => rebuildImageCache = (v != null));
                p.Add("h|help", "show this message and exit", v => showHelpAndExit = (v != null));

                try
                {
                    List<string> extra = p.Parse(args);
                }
                catch (OptionException e)
                {
                    Logger.Log(e.Message);
                    Console.WriteLine("Try \"" + appName + " --help\" for more information.");
                    return 1;
                }

                if (showHelpAndExit)
                {
                    Console.WriteLine("Usage: " + appName + " [options]");
                    Console.WriteLine();
                    p.WriteOptionDescriptions(Console.Out);
                    return 0;
                }

                var templateVariables = new Dictionary<string, string>()
                {
                    { "Application.ProductName", applicationVersionInfo.ProductName },
                    { "Application.Version", applicationVersionInfo.ProductVersion }
                };

                // Setting up the server
                var server = new Server(dataFilesRoot, cacheFilesRoot, templateVariables, rebuildImageCache);

                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
                {
                    if (!server.Stopped)
                    {
                        server.Stop();
                        Logger.Log("Stop flag set (Ctrl+C again will terminate the server)");
                        e.Cancel = true;
                    }
                    else
                    {
                        Logger.Log("Server stop already requested, Ctrl+C pressed again. Terminating");
                        e.Cancel = false;
                    }
                };

                Console.WriteLine("Press Ctrl+C to stop the server and exit...");
                Console.WriteLine();

                // Loading the desk data from the saved file
                server.Load(deskFileName);

                // Starting the server
                server.Start("http://" + addr + ":" + port + "/").Wait();

                return 0;
            }
            catch (Exception e)
            {
                Logger.LogError(String.Format("Exception: {0}", e));
                return 1;
            }
        }
    }
}
