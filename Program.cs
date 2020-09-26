using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace CompletelyUnsafeMessenger
{
    class Program
    {
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
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Assembly.GetCallingAssembly().Location);
                Console.OutputEncoding = Encoding.UTF8;

                // Welcome
                Console.WriteLine(fvi.ProductName + " v" + fvi.ProductVersion);
                Console.WriteLine();

                // Processing the command line
                string appName = Assembly.GetExecutingAssembly().GetName().Name;

                bool showHelpAndExit = false;
                short port = 8080;
                string addr = "+";
                string deskFileName = "desk.json";
                string dataFilesRoot = ".";

                var p = new OptionSet();
                p.Add<short>("p|port=", "the port number to listen to (8080 by default)", v => port = v);
                p.Add<string>("a|addr=", "the address to listen to ('+' by default)", v => addr = v);
                p.Add<string>("d|desk=", "desk file (desk.json by default)", v => deskFileName = v);
                p.Add<string>("r|data_root=", "data files root ('.' by default)", v => dataFilesRoot = v);
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
                    { "Application.ProductName", fvi.ProductName },
                    { "Application.Version", fvi.ProductVersion }
                };

                // Setting up the server
                var server = new Server(dataFilesRoot, templateVariables);

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

                Logger.Log("Press Ctrl+C to exit...");

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
