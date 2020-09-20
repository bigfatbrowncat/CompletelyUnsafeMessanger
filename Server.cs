using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;
using System.Linq;
using Mono.Options;

namespace CompletelyUnsafeMessenger
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Completely Unsafe Messenger 0.1");
            Console.WriteLine();

            string appName = Assembly.GetExecutingAssembly().GetName().Name;

            bool showHelp = false;
            short port = 8080;
            string addr = "+";
            string dbName = "messages.db";

            var p = new OptionSet();
            p.Add<short>("p|port=", "the port number to listen to (8080 by default)", v => port = v);
            p.Add<string>("a|addr=", "the address to listen to ('+' by default)", v => addr = v);
            p.Add<string>("d|db=", "database file (messages.db by default)", v => dbName = v);
            p.Add("h|help", "show this message and exit", v => showHelp = (v != null));

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("[Main] " + e.Message);
                Console.WriteLine("Try \"" + appName + " --help\" for more information.");
                return 1;
            }

            if (showHelp)
            {
                Console.WriteLine("Usage: " + appName + " [options]");
                Console.WriteLine();
                p.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            // Global configuration
            Console.OutputEncoding = Encoding.UTF8;

            var server = new Server();

            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => {
                if (!server.Stopped)
                {
                    server.Stop();
                    Console.WriteLine("[Main] Stop flag set (Ctrl+C again will terminate the server)");
                    e.Cancel = true;
                }
                else
                {
                    Console.WriteLine("[Main] Server stop already requested, Ctrl+C pressed again. Terminating");
                    e.Cancel = false;
                }
            };

            Console.WriteLine("[Main] Press Ctrl+C to exit...");

            server.Load(dbName);
            server.Start("http://" + addr + ":" + port + "/").Wait();
            return 0;
        }
    }

    abstract class Message
    {
        protected List<byte> data = new List<byte>();
        public void Append(IList<byte> part)
        {
            data.AddRange(part);
        }
        public virtual int Length { get { return data.Count; } }
        public IReadOnlyCollection<byte> Data { get { return data.AsReadOnly(); } }

        public virtual async Task Send(WebSocket webSocket)
        {
            var fullSeg = new ArraySegment<byte>(data.ToArray(), 0, data.Count);
            await webSocket.SendAsync(fullSeg, WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        public virtual async Task Broadcast(ICollection<WebSocket> webSockets)
        {
            foreach (WebSocket ws in webSockets)
            {
                await Send(ws);
            }
        }
    }
    class BinaryMessage : Message
    {

    }
    class TextMessage : Message
    {
        public override string ToString()
        {
            return Encoding.UTF8.GetString(data.ToArray());
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

    class Messages
    {
        private string dbName = "messages.db";
        private enum DBMessageType { Text = 0, Binary = 1 };

        private readonly List<Message> list = new List<Message>();
        public IReadOnlyCollection<Message> List { get { return list.AsReadOnly(); } }

        public Messages(string dbName)
        {
            this.dbName = dbName;
        }

        public void Add(Message message)
        {
            lock (this)
            {
                using var connection = new SqliteConnection("Data Source=\"" + dbName + "\"");
                if (connection != null)
                {
                    if (connection.State == System.Data.ConnectionState.Closed) connection.Open();

                    // Checking for the "messages" table
                    CreateMessagesTableIfNotExists(connection);

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText =
                        @"
                            INSERT INTO messages (user, type, message)
                            VALUES($user, $type, $message);
                        ";

                        DBMessageType type;
                        if (message is BinaryMessage) type = DBMessageType.Binary;
                        else if (message is TextMessage) type = DBMessageType.Text;
                        else throw new Exception("Invalid case");

                        command.Parameters.AddWithValue("$user", "anonymous");
                        command.Parameters.AddWithValue("$type", type);
                        command.Parameters.AddWithValue("$message", message.Data.ToArray<byte>());

                        command.ExecuteNonQuery();
                    }
                    list.Add(message);
                }
                else
                {
                    Console.WriteLine("[Messages.Add] Can't add the new message to the database");
                }

            }
        }

        private bool CreateMessagesTableIfNotExists(SqliteConnection connection)
        {
            // Checking for the "messages" table
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                @"
                    CREATE TABLE IF NOT EXISTS messages (id INTEGER PRIMARY KEY AUTOINCREMENT, user TEXT, type INTEGER, message BLOB)
                ";
                int lines = command.ExecuteNonQuery();
                return lines > 0;
            }
        }


        public bool LoadFromDB()
        {
            lock (this)
            {
                list.Clear();

                using var connection = new SqliteConnection("Data Source=\"" + dbName + "\"");
                if (connection != null)
                {
                    if (connection.State == System.Data.ConnectionState.Closed) connection.Open();

                    // Checking for the "messages" table
                    CreateMessagesTableIfNotExists(connection);

                    using var command = connection.CreateCommand();
                    command.CommandText =
                    @"
                        SELECT id, user, type, message FROM messages;
                    ";

                    //int id = 345;
                    //command.Parameters.AddWithValue("$id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        Dictionary<long, Message> msgFromDB = new Dictionary<long, Message>();

                        while (reader.Read())
                        {
                            long id = reader.GetInt64(0);
                            string user = reader.GetString(1);
                            DBMessageType type = (DBMessageType)reader.GetInt16(2);
                            var messageStream = reader.GetStream(3);

                            byte[] messageData;
                            using (var messageStreamReader = new MemoryStream())
                            {
                                messageStream.CopyTo(messageStreamReader);
                                messageData = messageStreamReader.ToArray();
                            }

                            Message message;
                            if (type == DBMessageType.Binary)
                            {
                                message = new BinaryMessage();
                            }
                            else if (type == DBMessageType.Text)
                            {
                                message = new TextMessage();
                            }
                            else
                            {
                                throw new Exception("Invalid case");
                            }
                            message.Append(messageData);
                            msgFromDB.Add(id, message);

                        }

                        var keys = new List<long>(msgFromDB.Keys.AsEnumerable());
                        keys.Sort();

                        // Loop through keys.
                        foreach (var key in keys)
                        {
                            list.Add(msgFromDB[key]);
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    class Server
    {
        private Messages messages;

        private readonly Mutex socketsAndMessagesMutex = new Mutex();
        private readonly HashSet<WebSocket> connectedSockets = new HashSet<WebSocket>();

        private readonly Mutex stoppingMutex = new Mutex();
        private bool stopped;

        public bool Stopped
        {
            get
            {
                lock (stoppingMutex)
                {
                    return stopped;
                }
            }
        }

        public void Stop()
        {
            lock (this)
            {
                if (listener != null)
                {
                    listener.Stop();
                }
            }

            lock (stoppingMutex)
            {
                stopped = true;
            }
        }

        private HttpListener listener = null;


        public void Load(string dbName)
        {
            messages = new Messages(dbName);

            if (messages.LoadFromDB())
            {
                Console.WriteLine("[Server.Load] Messages database {0} loaded succesfully", dbName);
            }
            else
            {
                Console.Error.WriteLine("[Server.Load] Messages database {0} can not be loaded", dbName);
            }
        }

        public async Task Start(string listenerPrefix)
        {
            await Run(listenerPrefix);
        }

        public async Task Run(string listenerPrefix)
        {

            lock (this)
            {
                if (listener != null)
                {
                    throw new ArgumentException("server is already started");
                }
            }

            lock (stoppingMutex)
            {
                stopped = false;
            }

            lock (this)
            {
                listener = new HttpListener();
            }
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();
            Console.WriteLine("[Server.Run] Listening at {0} ...", listenerPrefix);
            
            while (true)
            {
                try
                {
                    HttpListenerContext listenerContext = await listener.GetContextAsync();

                    if (listenerContext.Request.IsWebSocketRequest)
                    {
                        ProcessWebSocketRequest(listenerContext);
                    }
                    else
                    {
                        ProcessPlainHttpRequest(listenerContext);
                    }
                }
                catch (System.Net.HttpListenerException e)
                {
                    if (e.ErrorCode == 995) 
                    {
                        Console.Error.WriteLine("[Server.Run] Listening interrupted by the user");
                    }
                }

                lock (stoppingMutex)
                {
                    if (stopped) break;
                }
            }

            Console.WriteLine("[Server.Run] Server stopped gracefully");
            lock (this)
            {
                listener = null;
            }
        }

        private async void ProcessPlainHttpRequest(HttpListenerContext listenerContext)
        {
            Console.Out.WriteLine("[Server.ProcessPlainHttpRequest] Request: " + listenerContext.Request.Url);
            var assembly = Assembly.GetExecutingAssembly();

            string resourceName;
            if (listenerContext.Request.Url.AbsolutePath == "/")
            {
                resourceName = "index.html";
            }
            else
            {
                resourceName = listenerContext.Request.Url.AbsolutePath;
                if (resourceName[0] == '/') resourceName = resourceName.Substring(1);
            }

            // Prepending the prefix
            resourceName = assembly.GetName().Name + ".res.root." + resourceName;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // Resource not found

                    byte[] result;
                    using (Stream stream404 = assembly.GetManifestResourceStream(assembly.GetName().Name + ".res.error404.html"))
                    {
                        // Page error404.html not found
                        string four_o_four;
                        if (stream404 == null)
                        {
                            four_o_four = "Error 404";
                            result = Encoding.UTF8.GetBytes(four_o_four);
                        }
                        else
                        {
                            using var memoryStream = new MemoryStream();
                            stream404.CopyTo(memoryStream);
                            result = memoryStream.ToArray();
                        }
                    }

                    Console.Error.WriteLine("[Server.ProcessPlainHttpRequest] Resource: " + resourceName + " not found. Responding with error 404");
                    listenerContext.Response.StatusCode = 404;
                    listenerContext.Response.ContentType = "text/html";
                    await listenerContext.Response.OutputStream.WriteAsync(MemoryExtensions.AsMemory(result));
                    listenerContext.Response.Close();
                }
                else
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        byte[] result = memoryStream.ToArray();

                        Console.Out.WriteLine("[Server.ProcessPlainHttpRequest] Responding with resource: " + resourceName);
                        listenerContext.Response.StatusCode = 200;
                        listenerContext.Response.ContentType = "text/html";
                        await listenerContext.Response.OutputStream.WriteAsync(MemoryExtensions.AsMemory(result));
                        listenerContext.Response.Close();
                    }
                }
            }

        }

        private async void ProcessWebSocketRequest(HttpListenerContext listenerContext)
        {
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
                Console.Error.WriteLine("[Server.ProcessPlainHttpRequest] Exception: {0}", e);
                return;
            }
                                
            WebSocket webSocket = webSocketContext.WebSocket;

            lock (socketsAndMessagesMutex)
            {
                // Adding the new client to the list
                connectedSockets.Add(webSocket);

                // Sending all the previous messages to the new client
                foreach (Message m in messages.List)
                {
                    m.Send(webSocket).Wait();
                }
            }


            Message messageBuilder = null;

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
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                    else 
                    {
                        // We have received a message. Text or binary

                        // Start saving the message (if not started)

                        if (messageBuilder == null)
                        {
                            if (receiveResult.MessageType == WebSocketMessageType.Text)
                            {
                                messageBuilder = new TextMessage();
                            }
                            else
                            {
                                messageBuilder = new BinaryMessage();
                            }
                        }

                        // Saving the message part

                        var aseg = new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count);
                        messageBuilder.Append(aseg);

                        // Logging the message 

                        // If the message is just ended, let's broadcast it to the clients

                        if (receiveResult.EndOfMessage)
                        {
                            if (receiveResult.MessageType == WebSocketMessageType.Text)
                            {
                                string messagePart = System.Text.Encoding.UTF8.GetString(aseg);
                                Console.WriteLine("[Server.ProcessWebSocketRequest] Received text: \"" + messageBuilder.ToString() + "\"");
                            }
                            else
                            {
                                Console.WriteLine("[Server.ProcessWebSocketRequest] Received binary: " + messageBuilder.Length + " bytes");
                            }

                            // Adding new message and taking all the connected sockets
                            lock (socketsAndMessagesMutex)
                            {
                                messages.Add(messageBuilder);
                                messageBuilder.Broadcast(connectedSockets).Wait();
                            }

                            messageBuilder = null;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Console.WriteLine("[Server.ProcessWebSocketRequest] Exception: {0}", e);
            }
            finally
            {
                // Clean up by disposing the WebSocket once it is closed/aborted.
                if (webSocket != null)
                {
                    lock (socketsAndMessagesMutex)
                    {
                        connectedSockets.Remove(webSocket);
                    }
                    webSocket.Dispose();
                }
            }
        }
    }

    // This extension method wraps the BeginGetContext / EndGetContext methods on HttpListener as a Task, using a helper function from the Task Parallel Library (TPL).
    // This makes it easy to use HttpListener with the C# 5 asynchrony features.
    public static class HelperExtensions
    {        
        public static Task GetContextAsync(this HttpListener listener)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, TaskCreationOptions.None);
        }
    }
}
