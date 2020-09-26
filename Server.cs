using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;
using System.Reflection;
using System.Web;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ApacheMimeTypes;

namespace CompletelyUnsafeMessenger
{
    /// <summary>
    /// <para>The server class</para>
    /// 
    /// <para>This class represents the HTTP and WebSocket server logic of the application.</para>
    /// </summary>
    class Server
    {
        /// <summary>
        /// HTTP Server backend
        /// </summary>
        private HttpListener listener = null;

        /// <summary>
        /// The desk controller
        /// </summary>
        private readonly Controller.Desk deskController = new Controller.Desk();

        /// <summary>
        /// The serializer
        /// </summary>
        private readonly Model.Serializer serializer = new Model.Serializer();

        /// <summary>
        /// An lock used during sockets and data updates
        /// </summary>
        private readonly object socketsAndMessagesLock = new object();

        /// <summary>
        /// A lock used to synchronize server stopping procedure
        /// </summary>
        private readonly object stoppingLock = new object();

        /// <summary>
        /// A lock used to synchronize file writing process
        /// </summary>
        private readonly ReaderWriterLock fileReaderWriterLock = new ReaderWriterLock();

        /// <summary>
        /// The list of client sockets currently connected to the server
        /// </summary>
        private readonly HashSet<WebSocket> connectedSockets = new HashSet<WebSocket>();

        /// <summary>
        /// Servr stopped flag
        /// </summary>
        private bool stopped;
        
        /// <summary>
        /// Path to the user-uploaded files
        /// </summary>
        private string dataFilesRoot;

        /// <summary>
        /// The variables passed into the text templates
        /// </summary>
        private Dictionary<string, string> templateVariablesTable = new Dictionary<string, string>();

        /// <summary>
        /// The executing assembly
        /// </summary>
        private readonly Assembly assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Checks if the server is currently stopped
        /// </summary>
        public bool Stopped
        {
            get
            {
                lock (stoppingLock)
                {
                    return stopped;
                }
            }
        }

        public Server(string dataFilesRoot, Dictionary<string, string> templateVariablesTable)
        {
            this.dataFilesRoot = dataFilesRoot;
            this.templateVariablesTable = templateVariablesTable;
        }

        /// <summary>
        /// Stops the server if it was currently running
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (listener != null)
                {
                    listener.Stop();
                }
            }

            lock (stoppingLock)
            {
                stopped = true;
            }
        }

        /// <summary>
        /// Loads the desk data from the file
        /// </summary>
        /// <param name="deskFileName">The desk contents file being loaded</param>
        public void Load(string deskFileName)
        {
            try
            {
                deskController.Load(deskFileName);

                Logger.Log(String.Format("Messages database {0} loaded succesfully", deskFileName));
            }
            catch (FileNotFoundException)
            {
                Logger.LogError(String.Format("Messages database {0} does not exist", deskFileName));
            }
            catch (Exception e)
            {
                Logger.LogError(String.Format("Messages database {0} can not be loaded: {1}", deskFileName, e.Message));
            }
        }

        /// <summary>
        /// Starts the server
        /// </summary>
        /// <param name="listenerPrefix">HTTP prefix to listen from (read docs for <see cref="System.Net.HttpListener"/>)</param>
        /// <returns>Asynchroneous task</returns>
        public async Task Start(string listenerPrefix)
        {
            // Checking if the server is already running
            // The running criterion is an existance of the "listener" object
            lock (this)
            {
                if (listener != null)
                {
                    throw new ArgumentException("server is already started");
                }
            }

            await Run(listenerPrefix);
        }

        /// <summary>
        /// The main server task. Should be run by the user with calling <c>Start</c>.
        /// </summary>
        /// <param name="listenerPrefix"></param>
        /// <returns>Asynchroneous task</returns>
        protected async Task Run(string listenerPrefix)
        {
            // Disabling the stop flag
            lock (stoppingLock)
            {
                stopped = false;
            }

            try
            {
                // Initializing the HTTP server backend
                lock (this)
                {
                    listener = new HttpListener();
                }
                listener.Prefixes.Add(listenerPrefix);
                listener.IgnoreWriteExceptions = true;
                listener.Start();
                Logger.Log(String.Format("Listening at {0} ...", listenerPrefix));

                // The server listening loop
                // This loop can only be interrupted by calling Stop
                while (listener.IsListening)
                {
                    try
                    {
                        // Getting the HTTP context for a client
                        HttpListenerContext listenerContext = await listener.GetContextAsync();

                        if (listenerContext.Request.IsWebSocketRequest)
                        {
                            // This is a WebSocket request. Running an asynchroneous task to process it
                            Task webSocketAsyncTask = Task.Run(() => ProcessWebSocketRequestAsync(listenerContext));
                        }
                        else
                        {
                            // This is a simple HTTP file request. Running an asynchroneous task to process it
                            Task plainHttpAsyncTask = Task.Run(() => ProcessPlainHttpRequestAsync(listenerContext));
                        }
                    }
                    catch (HttpListenerException e)
                    {
                        // Checking for user cancel
                        // This will hit if Stop() is being called during listener.GetContextAsync().
                        if (e.ErrorCode == 995 /* ERROR_OPERATION_ABORTED */)
                        {
                            Logger.LogError("Listening interrupted by the user");
                        }
                        else
                        {
                            Logger.LogError(String.Format("Exception: {0}", e));
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(String.Format("Exception: {0}", e));
                    }

                    // Checking if the server is stopped
                    lock (stoppingLock)
                    {
                        if (stopped) break;
                    }
                }

                Logger.Log("Server stopped gracefully");
            }
            finally
            {
                // In any way, the listener should be set to null if the server isn't running
                lock (this)
                {
                    listener = null;
                }
            }
        }

        private async void ProcessTextTemplate(Stream input, Stream output, Dictionary<string, string> varTable)
        {
            using var memoryStream = new MemoryStream();
            input.CopyTo(memoryStream);
            var inBytes = memoryStream.ToArray();
            string template = Encoding.UTF8.GetString(inBytes);

            foreach (var key in varTable.Keys)
            {
                template = template.Replace("{" + key + "}", varTable[key]);
            }

            await output.WriteAsync(Encoding.UTF8.GetBytes(template));
        }

        /// <summary>
        /// An asynchroneous method for a plain HTTP request processing
        /// </summary>
        /// <param name="listenerContext">Listener context</param>
        private async void ProcessPlainHttpRequestAsync(HttpListenerContext listenerContext)
        {
            try 
            { 
                Logger.Log("Request: " + listenerContext.Request.Url);

                // Finding the resource to serve
                string resourceName;
                if (listenerContext.Request.Url.AbsolutePath == "/")
                {
                    // Empty request leads to index.html
                    resourceName = "index.html.template";
                }
                else
                {
                    // If request isn't empty, removing the leading /
                    resourceName = listenerContext.Request.Url.AbsolutePath;

                    // Removing the leading '/'
                    if (resourceName[0] == '/') resourceName = resourceName.Substring(1);
                }

                if (resourceName.Length > 5 && resourceName.Substring(0, 5) == "data/")
                {
                    // '/data' urls are the resources that were uploaded by the user
                    // so we are reading them from the data folder
                    string dataResName = Path.Combine(dataFilesRoot, HttpUtility.UrlDecode(resourceName.Substring(5)));

                    try
                    {
                        fileReaderWriterLock.AcquireReaderLock(Timeout.Infinite);

                        // Loading the file and responding
                        using var fs = new FileStream(dataResName, FileMode.Open);
                        listenerContext.Response.StatusCode = 200;
                        fs.CopyTo(listenerContext.Response.OutputStream);
                        listenerContext.Response.Close();
                        Logger.Log("Responding with file: " + dataResName);
                    }
                    finally
                    {
                        fileReaderWriterLock.ReleaseReaderLock();
                    }
                }
                else
                {
                    // Prepending the prefix
                    resourceName = assembly.GetName().Name + ".res.root." + resourceName;

                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            // Resource not found
                            Logger.LogError("Resource: " + resourceName + " not found. Responding with error 404");
                            listenerContext.Response.StatusCode = 404;
                            listenerContext.Response.ContentType = "text/html";

                            using (Stream stream404 = assembly.GetManifestResourceStream(assembly.GetName().Name + ".res.error404.html.template"))
                            {
                                // Page error404.html not found
                                string four_o_four;
                                if (stream404 == null)
                                {
                                    four_o_four = "<html><body>Error 404 occured and the server can't load error 404 page :(</body></html>";
                                    listenerContext.Response.OutputStream.Write(Encoding.UTF8.GetBytes(four_o_four));
                                }
                                else
                                {
                                    ProcessTextTemplate(stream404, listenerContext.Response.OutputStream, templateVariablesTable);
                                }
                            }

                            listenerContext.Response.Close();
                        }
                        else
                        {
                            // Checking if it's a template. Setting a flag
                            var isTemplate = false;
                            if (resourceName.EndsWith(".template"))
                            {
                                resourceName = resourceName.Substring(0, resourceName.Length - 9);
                                isTemplate = true;
                            }

                            listenerContext.Response.StatusCode = 200;
                            var ext = Path.GetExtension(resourceName);
                            if (ext.Length > 0 && ext[0] == '.') ext = ext.Substring(1);
                            if (Apache.MimeTypes.ContainsKey(ext))
                            {
                                listenerContext.Response.ContentType = Apache.MimeTypes[ext];
                            }
                            Logger.Log("Responding with resource: " + resourceName + " (content type: " + listenerContext.Response.ContentType + ")");

                            if (isTemplate)
                            {
                                // Processing the template (replacing the template veariables)
                                ProcessTextTemplate(stream, listenerContext.Response.OutputStream, templateVariablesTable);
                            }
                            else
                            {
                                // Passing the file directly to the output
                                await stream.CopyToAsync(listenerContext.Response.OutputStream);
                            }

                            listenerContext.Response.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. 
                // Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` 
                // is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Logger.Log(String.Format("Exception: {0}", e));
            }
        }

        private enum Expecting { Command, AppendedBinary }

        /// <summary>
        /// An asynchroneous method for a plain HTTP request processing
        /// </summary>
        /// <param name="listenerContext">Listener context</param>
        private async void ProcessWebSocketRequestAsync(HttpListenerContext listenerContext)
        {
            // Accepting WebSocket connection from the context
            WebSocketContext webSocketContext;
            try
            {
                webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
            }
            catch (Exception e)
            {
                // The upgrade process failed somehow. For simplicity lets assume it was a failure on the part of the server and indicate this using 500.
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Logger.LogError(String.Format("Exception: {0}", e));
                return;
            }

            // This is our new WebSocket object
            WebSocket webSocket = webSocketContext.WebSocket;

            lock (socketsAndMessagesLock)
            {
                // Adding the new client to the list
                connectedSockets.Add(webSocket);

                // Sending all the previous messages to the new client
                Logger.Log("Sending " + deskController.Cards.Count + " old cards to the a new client");
                foreach (var oldCard in deskController.Cards)
                {
                    // Making an add_card command for the selected card
                    var addCommand = new Model.Commands.AddCardCommand(oldCard);
                    var addCommandGram = new TextWebSockGram(serializer.SerializeCommand(addCommand));

                    // Sending the command to the new client
                    addCommandGram.Send(webSocket).Wait();
                }
            }

            // We are expecting a commend to be sent from  the client
            Expecting expecting = Expecting.Command;

            // Here the received command will be held if we will be waiting for an appended binary data
            Model.Commands.Command receivedCommand = null;

            // The received WebSocket message collector
            WebSockGram receivedGram = null;

            // Communicating
            try
            {
                byte[] receiveBuffer = new byte[16384];

                // While the WebSocket connection remains open run a simple loop that receives data and sends it back.
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        // We've got a Close request. Sending it back
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                    else
                    {
                        // We have received a message. Text or binary. We are going to collect it to the WebSockGram object

                        if (receivedGram == null)
                        {
                            // Start building of a new message
                            receivedGram = WebSockGram.StartReceivingFromReceiveResult(receiveBuffer, receiveResult);
                        }
                        else
                        {
                            // Continue building of the message
                            receivedGram.AppendFromReceiveResult(receiveBuffer, receiveResult);
                        }

                        // If the message has just ended, let's interpret it
                        if (receivedGram.Completed)
                        {
                            // Logging the message 
                            if (receivedGram is TextWebSockGram)
                            {
                                Logger.Log("Received text: \"" + receivedGram.ToString() + "\"");
                            }
                            else if (receivedGram is BinaryWebSockGram)
                            {
                                Logger.Log("Received binary: " + receivedGram.Length + " bytes");
                            }
                            else
                            {
                                throw new Exception("Invalid case");
                            }

                            // Checking what we have received (depending on what we have been expecting)
                            if (expecting == Expecting.Command)
                            {
                                // We are expecting a command. So receivedGram has to be a text
                                if (receivedGram is TextWebSockGram)
                                {
                                    var command = serializer.DeserializeCommand(receivedGram.ToString());

                                    // Executing the received command
                                    if (command is Model.Commands.AddCardCommand)
                                    {
                                        var addCardCommand = command as Model.Commands.AddCardCommand;

                                        lock (socketsAndMessagesLock)
                                        {
                                            // Giving the received message to the controller
                                            deskController.AddCard(addCardCommand.Card);

                                            // Broadcasting the message
                                            Logger.Log("Broadcasting the new card " + addCardCommand.Card  + " to the " + connectedSockets.Count + " connected clients");
                                            receivedGram.Broadcast(connectedSockets).Wait();
                                        }
                                    }
                                    else if (command is Model.Commands.UploadImageCardCommand)
                                    {
                                        // After this command a binary should be appended (containing the image data).
                                        // So we are saving the command itself...
                                        receivedCommand = command;

                                        // ...and setting the state machine to expect binary appended to the command.
                                        expecting = Expecting.AppendedBinary;
                                    }
                                }
                                else
                                {
                                    throw new Exception("We are expecting a command, but the received WebSocket message isn't a text");
                                }
                            }
                            else if (expecting == Expecting.AppendedBinary)
                            {
                                // We are expecting an appended binary
                                if (receivedGram is BinaryWebSockGram)
                                {
                                    // We've got an appended binary
                                    if (receivedCommand is Model.Commands.UploadImageCardCommand)
                                    {
                                        // Our current command is upload_image_card
                                        var uploadImageMessageCommand = receivedCommand as Model.Commands.UploadImageCardCommand;

                                        try
                                        {
                                            fileReaderWriterLock.AcquireWriterLock(Timeout.Infinite);
                                            // Saving the received binary as a file
                                            File.WriteAllBytes(uploadImageMessageCommand.Card.Filename, (receivedGram as BinaryWebSockGram).Data.ToArray());
                                        }
                                        finally
                                        {
                                            fileReaderWriterLock.ReleaseWriterLock();
                                        }

                                        lock (socketsAndMessagesLock)
                                        {
                                            // Setting up the link to the saved file. Adding it to the received Card data
                                            var imageFileCard = uploadImageMessageCommand.Card;
                                            imageFileCard.Link = new Uri(listenerContext.Request.Url, "data/" + uploadImageMessageCommand.Card.Filename);

                                            // Creating a new card in the controller, containing the received image
                                            deskController.AddCard(imageFileCard);

                                            // Broadcasting the add_card command to all the connected clients 
                                            Logger.Log("Broadcasting the new image card " + imageFileCard.Link + " to the " + connectedSockets.Count + " connected clients");
                                            var addCommand = new Model.Commands.AddCardCommand(imageFileCard);
                                            TextWebSockGram addCardGram = new TextWebSockGram(serializer.SerializeCommand(addCommand));
                                            addCardGram.Broadcast(connectedSockets).Wait();
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("Strange case");
                                    }

                                }
                                else
                                {
                                    // Resetting the received command (we have a client error here)
                                    throw new Exception("We are expecting a binary appended to a command, but the received WebSocket message isn't a binary");
                                }

                                // After we received the appended binary, resetting the state machine to expect a command again
                                expecting = Expecting.Command;
                                receivedCommand = null;
                            }
                            receivedGram = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. 
                // Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` 
                // is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Logger.Log(String.Format("Exception: {0}", e));
            }
            finally
            {
                // Disposing the WebSocket and removing it from the list
                try
                {
                    // Clean up by disposing the WebSocket once it is closed/aborted.
                    if (webSocket != null)
                    {
                        lock (socketsAndMessagesLock)
                        {
                            connectedSockets.Remove(webSocket);
                        }
                        webSocket.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(String.Format("Exception during WebSocket disposure: {0}", e));
                }
            }
        }
    }

    /// <summary>
    /// Collecting the data sent from asocket
    /// </summary>
    abstract class WebSockGram
    {
        /// <summary>
        /// Completed flag
        /// </summary>
        protected bool completed = false;

        /// <summary>
        /// The collected data buffer
        /// </summary>
        protected List<byte> data = new List<byte>();

        public WebSockGram(bool completed = false)
        {
            this.completed = completed;
        }
        public WebSockGram(IEnumerable<byte> data, bool completed = false)
        {
            Append(data, completed);
        }

        /// <summary>
        /// Appends some data to the object
        /// </summary>
        /// <param name="part">A data buffer to append</param>
        /// <param name="completed">If <c>true</c>, sets the <c>completed</c> flag</param>
        public void Append(IEnumerable<byte> part, bool completed = false)
        {
            if (!this.completed)
            {
                data.AddRange(part);
                this.completed = completed;
            }
            else
            {
                throw new Exception("Can't append data to a completed " + GetType().Name);
            }
        }

        /// <summary>
        /// Data length
        /// </summary>
        public virtual int Length { get { return data.Count; } }

        /// <summary>
        /// Is the data message complete
        /// </summary>
        public bool Completed { get { return completed; } }

        /// <summary>
        /// The collected data
        /// </summary>
        public List<byte> Data { get { return data; } }

        /// <summary>
        /// Sends the data to a <see cref="WebSocket">WebSocket</see>
        /// </summary>
        /// <param name="webSocket"><see cref="WebSocket">WebSocket</see> object</param>
        /// <returns></returns>
        public virtual async Task Send(WebSocket webSocket)
        {
            var fullSeg = new ArraySegment<byte>(data.ToArray(), 0, data.Count);
            await webSocket.SendAsync(fullSeg, WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        /// <summary>
        /// Broadcasts the data to a collection of <see cref="WebSocket">WebSocket</see> objects
        /// </summary>
        /// <param name="webSockets">Collection of <see cref="WebSocket">WebSocket</see> objects</param>
        /// <returns></returns>
        public virtual async Task Broadcast(ICollection<WebSocket> webSockets)
        {
            foreach (WebSocket ws in webSockets)
            {
                await Send(ws);
            }
        }


        /// <summary>
        /// Gets a websocket message from the specified WebSocketReceiveResult. 
        /// Appends it to the current WebSockGram
        /// </summary>        
        public void AppendFromReceiveResult(byte[] receiveBuffer, WebSocketReceiveResult receiveResult)
        {
            // Saving the message part
            var aseg = new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count);
            Append(aseg, receiveResult.EndOfMessage);
        }

        /// <summary>
        /// Creates a new WebSockGram suitable for the specified WebSocketReceiveResult (text or binary).
        /// Appends the received data to it
        /// </summary>
        public static WebSockGram StartReceivingFromReceiveResult(byte[] receiveBuffer, WebSocketReceiveResult receiveResult)
        {
            WebSockGram result;
            if (receiveResult.MessageType == WebSocketMessageType.Text)
            {
                result = new TextWebSockGram();
            }
            else if (receiveResult.MessageType == WebSocketMessageType.Binary)
            {
                result = new BinaryWebSockGram();
            }
            else
            {
                throw new WebSocketException("Invalid type of a message. Text or Binary websocket message expected");
            }

            result.AppendFromReceiveResult(receiveBuffer, receiveResult);

            return result;
        }
    }

    /// <summary>
    /// A <see cref="WebSockGram" /> containing binary data
    /// </summary>
    class BinaryWebSockGram : WebSockGram
    {
        public BinaryWebSockGram(bool completed = false) : base(completed) { }
        public BinaryWebSockGram(IEnumerable<byte> data, bool completed = false) : base(data, completed) { }

        public override async Task Send(WebSocket webSocket)
        {
            await base.Send(webSocket);
        }
    }

    /// <summary>
    /// A <see cref="WebSockGram" /> containing text data
    /// </summary>
    class TextWebSockGram : WebSockGram
    {
        public TextWebSockGram(bool completed = false) : base(completed) { }
        public TextWebSockGram(string s, bool completed = false) : base(Encoding.UTF8.GetBytes(s), completed) { }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(data.ToArray());
        }

        public void FromString(string s)
        {
            data = new List<byte>(Encoding.UTF8.GetBytes(s));
        }

        public override int Length
        {
            get
            {
                return Encoding.UTF8.GetString(data.ToArray()).Length;
            }
        }
        public override async Task Send(WebSocket webSocket)
        {
            var fullSeg = new ArraySegment<byte>(data.ToArray(), 0, data.Count);
            await webSocket.SendAsync(fullSeg, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    /// <summary>
    /// <para>This extension method wraps the BeginGetContext/EndGetContext methods on HttpListener as a Task, 
    /// using a helper function from the Task Parallel Library (TPL).</para>
    /// <para>This makes it easy to use HttpListener with the C# 5 asynchrony features.</para>
    /// </summary>
    public static class HelperExtensions
    {
        public static Task GetContextAsync(this HttpListener listener, TaskCreationOptions taskCreationOptions = TaskCreationOptions.None)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, taskCreationOptions);
        }
    }

    public static class Logger {
        public static void Log(object message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string fileName = Path.GetFileName(filePath);
            // we'll just use a simple Console write for now    
            Console.Out.WriteLine("{0}({1}): [{2}@{3}] {4}", fileName, lineNumber, memberName, Thread.CurrentThread.ManagedThreadId, message);
        }

        public static void LogError(object message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string fileName = Path.GetFileName(filePath);
            // we'll just use a simple Console write for now    
            Console.Error.WriteLine("{0}({1}): [{2}@{3}] {4}", fileName, lineNumber, memberName, Thread.CurrentThread.ManagedThreadId, message);
        }
    }
}
