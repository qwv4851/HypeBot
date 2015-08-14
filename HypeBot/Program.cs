using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;
using SKYPE4COMLib;

namespace HypeBot
{
    // Structure which stores data about messages which include URLs.
    class Hype
    {
        public Hype(string handle, string name, string url, string body, DateTime date)
        {
            this.handle = handle;
            this.name = name;
            this.url = url;
            this.body = body;
            this.date = date;
        }

        public string handle;
        public string name;
        public string url;
        public string body;
        public DateTime date;
    }

    class Program
    {
        private static Skype skype;
        private static Chat chatRoom;
        private static string host = GetConfig("host");
        private static string user = GetConfig("user");
        private static string password = GetConfig("password");
        private static string schema = GetConfig("schema");
        private static string port = GetConfig("port");
        private static string createStr = String.Format("server={0};user={1};password={2};port={3};", host, user, password, port);
        private static string connStr = String.Format("{0}database={1};", createStr, schema);

        private static Object skypeLock = new Object();
        private static int roomNumber = -1;

        public static HypeBotTwitter twitter = new HypeBotTwitter();

        // Initializes the bot and waits for a key press to quit.
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out roomNumber))
                {
                    Console.WriteLine("Invalid room number: {0}", args[0]);
                }
            }

            Console.WriteLine("Initializing Hip Chat");
            var t = Task.Factory.StartNew(HipChat.InitHipChat);
            Console.WriteLine("Initializing Skype...");
            Console.WriteLine("Note: You may have to accept a request from the Skype client.");
            InitSkype();
            chatRoom = SelectChatRoom();
            InitDatabase();
            skype.MessageStatus += OnMessageStatus;
            skype.FileTransferStatusChanged += OnFileStatusChanged;
            Console.WriteLine("Hype Bot initialized.\n");
            ShowHelp();
            
            while (true)
            {
                Console.Write("\n>");
                string input = Console.ReadLine();
                Console.WriteLine();
                switch (input)
                {
                    case "help":
                        ShowHelp();
                        break;
                    case "reset":
                        Reset();
                        break;
                    case "quit":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine(String.Format("Error: Invalid command '{0}'. Type 'help' for a list of valid commands.", input));
                        break;
                }
            }
        }

        private static string UploadImage(string p)
        {
            string url = string.Empty;
            using (var w = new WebClient())
            {
                var values = new NameValueCollection
                {
                    { "image", Convert.ToBase64String(File.ReadAllBytes(p)) }
                };

                w.Headers["Authorization"] = "Client-ID 98b87f5fc485025";
                byte[] response = w.UploadValues("https://api.imgur.com/3/image", values);

                string json = Encoding.ASCII.GetString(response);
                int startIndex = json.IndexOf("http:");
                int endIndex = Math.Max(Math.Max(json.IndexOf(".png"), json.IndexOf(".jpg")), json.IndexOf(".gif"));
                url = json.Substring(startIndex, endIndex - startIndex).Replace("\\", "");
            }
            return url;
        }

        static void OnFileStatusChanged(IFileTransfer pTransfer, TFileTransferStatus Status)
        {
            string lowerpath = pTransfer.FilePath.ToLower();
            if (Status == TFileTransferStatus.fileTransferStatusCompleted)
            {
                if ((lowerpath.EndsWith(".png") || lowerpath.EndsWith(".jpg") || lowerpath.EndsWith(".gif")))
                {
                    Console.WriteLine("Uploading file: " + pTransfer.FilePath);
                    string url = UploadImage(pTransfer.FilePath);
                    SendMessage(String.Format("{0}: {1}", pTransfer.Filename, url));
                }
                else
                {
                    Console.WriteLine("Invalid file extension for file: " + pTransfer.Filename);
                }
                Console.WriteLine("Deleting " + pTransfer.Filename);
                File.Delete(pTransfer.FilePath);
            }
        }

        public static string GetConfig(string key)
        {
            return ConfigurationSettings.AppSettings[key];
        }

        private static void InitDatabase()
        {
            string query = String.Format(@"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{0}'", schema);
            MySqlConnection conn = new MySqlConnection(createStr);
            try
            {
                conn.Open();
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, conn);
                adapter.SelectCommand = new MySqlCommand(query, conn);
                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet);
                DataTable table = dataSet.Tables[0];
                if (table.Rows.Count == 0)
                {
                    Reset();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            conn.Close();
        }

        private static void Reset()
        {
            Console.WriteLine("Creating database...");
            CreateDatabase();
            ParseChatRoom();
            Console.WriteLine("Parsing complete.");
        }

        private static void CreateDatabase()
        {
            string file = "create_schema.sql";
            string relative = "../../../";
            string path = file;
            if (!File.Exists(path))
            {
                path = relative + path;
            }
            if (File.Exists(path))
            {
                string query = File.ReadAllText(path);
                MySqlConnection conn = new MySqlConnection(createStr);
                try
                {
                    conn.Open();
                    MySqlCommand command = new MySqlCommand(query, conn);
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                conn.Close();
            }
            else
            {
                Console.WriteLine("Missing file: " + file);
                Console.Write("Press any key to continue...");
                while (!Console.KeyAvailable);
                Environment.Exit(1);
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Commands");
            Console.WriteLine("-----------------------------------------------------------------");
            ShowCommand("help", "Displays a list of commands.");
            ShowCommand("reset", "Reset the database and parse the chat room for URLs.");
            ShowCommand("quit", "Stop running the bot and close the application.");
        }

        private static void ShowCommand(string command, string description)
        {
            Console.WriteLine(String.Format("{0,-10}" + description, command));
        }

        // Searches the entire chat room for URLs and saves them to the database
        private static void ParseChatRoom()
        {
            MySqlConnection conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();
                foreach (ChatMessage message in chatRoom.Messages)
                {
                    string fullUrl;
                    string url = GetUrl(message.Body, out fullUrl);
                    if (url.Length > 0)
                    {
                        Console.WriteLine(url);
                        AddHype(new Hype(message.Sender.Handle, message.Sender.FullName, url, message.Body, message.Timestamp), conn);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            conn.Close();
        }

        // Adds the given hype to the database.
        private static bool AddHype(Hype hype)
        {
            MySqlConnection conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();
                AddHype(hype, conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            conn.Close();
            return true;
        }

        private static void AddHype(Hype hype, MySqlConnection conn)
        {
            string query = "REPLACE INTO old_hype VALUES (@url, @handle, @name, @date, @body)";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, conn);
            adapter.InsertCommand = new MySqlCommand(query, conn);
            adapter.InsertCommand.Parameters.Add(new MySqlParameter("@handle", hype.handle));
            adapter.InsertCommand.Parameters.Add(new MySqlParameter("@name", hype.name));
            adapter.InsertCommand.Parameters.Add(new MySqlParameter("@url", hype.url));
            adapter.InsertCommand.Parameters.Add(new MySqlParameter("@body", hype.body));
            adapter.InsertCommand.Parameters.Add(new MySqlParameter("@date", hype.date));
            adapter.InsertCommand.ExecuteNonQuery();
        }

        // Seaches the database for the given URL and returns the associated Hype if found.
        public static Hype GetHype(string url)
        {
            MySqlConnection conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();
                string query = "SELECT * FROM old_hype WHERE url=@url";
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, conn);
                adapter.SelectCommand = new MySqlCommand(query, conn);
                adapter.SelectCommand.Parameters.AddWithValue("@url", url);
                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet);
                DataTable table = dataSet.Tables[0];
                if (table.Rows.Count > 0)
                {
                    DataRow r = table.Rows[0];
                    Hype hype = new Hype((string)r["handle"], (string)r["name"], url, (string)r["body"], (DateTime)r["date"]);
                    return hype;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            conn.Close();
            return null;
        }

        // Starts Skype if not currently running.
        private static void InitSkype()
        {
            skype = new SKYPE4COMLib.Skype();
            if (!skype.Client.IsRunning)
                skype.Client.Start();
        }

        // Select a chat room
        static Chat SelectChatRoom()
        {
            var chats = skype.Chats.Cast<Chat>().OrderByDescending(chat => chat.Members.Count).ToArray<Chat>();
            for (int i = 0; i < chats.Length; i++)
            {
                Chat chat = chats[i];
                Console.WriteLine("{0}: Name: {1} Members: {2}", i, chat.Name, chat.Members.Count);
            }

            if (roomNumber == -1)
            {
                Console.Write("Choose a room (default = 0): ");
                string input = Console.ReadLine();
                int.TryParse(input, out roomNumber);
                if (roomNumber == -1 || roomNumber >= chats.Length)
                {
                    roomNumber = 0;
                }
            }
            Console.WriteLine("Joining room {0}", roomNumber);
            return chats[roomNumber];
        }

        // Checks received messages for old URLs and alerts the chat by sending a response message.
        static void OnMessageStatus(ChatMessage message, TChatMessageStatus status)
        {
            if (message.Chat.Name != chatRoom.Name)
                return;
            User sender = message.Sender;

            if (status == TChatMessageStatus.cmsReceived)
            {
                //Console.WriteLine("Handle: {0}, Display Name: {1}, Full Name: {2}, Body: {3}", sender.Handle, sender.DisplayName, sender.FullName, message.Body);
                string fullUrl;
                string url = GetUrl(message.Body, out fullUrl);
                string messageBody = message.Body;

                // Format quoted messages
                Match quote = Regex.Match(messageBody, @"(\[.*\d+:\d+:\d+ .M]) (.*?): (.*?)\r\n\r\n<<<", RegexOptions.Singleline);
                if (quote.Length > 0)
                {
                    string timestamp = quote.Groups[1].Value;
                    string quoteName = quote.Groups[2].Value;
                    string quoteBody = quote.Groups[3].Value;
                    string beforeQuote = messageBody.Substring(0, quote.Index);
                    if (beforeQuote.Length > 0)
                    {
                        beforeQuote += "<br>";
                    }
                    string afterQuote = messageBody.Substring(quote.Index + quote.Length);
                    if (afterQuote.Length > 0)
                    {
                        afterQuote = "<br>" + afterQuote;
                    }
                    messageBody = String.Format("{0}<i>&ldquo;{1}&rdquo; &mdash;{2}, {3}</i> {4}", beforeQuote, quoteBody, quoteName, timestamp, afterQuote);
                }
                else if (url.Length > 0)
                {
                    CheckHype(messageBody, message.Timestamp, sender.FullName, sender.Handle, url);
                    messageBody = messageBody.Replace(fullUrl, String.Format("<a href=\"{0}\">{1}</a>", fullUrl, url));
                }

                twitter.Tweet(messageBody);
                HipChat.SendHipMessage(sender.FullName, messageBody);
            }
        }

        public static void CheckHype(string messageBody, DateTime timestamp, string fullName, string handle, string url)
        {
            Hype oldHype = GetHype(url);
            if (oldHype == null)
            {
                AddHype(new Hype(handle, fullName, url, messageBody, timestamp));
                string title = String.Format("{0}: {1}", fullName, messageBody);
                RedditBot.Submit(title, url);
            }
            else
            {
                string errorMsg = String.Format("OLD HYPE DETECTED: Violator {0} ({1}) Old hype URL {2} first posted by {3} on {4}. Original message: \"{5}\"", fullName, handle, oldHype.url, oldHype.name, oldHype.date, oldHype.body);
                Console.WriteLine("\n" + errorMsg);
                SendMessage(errorMsg);
                HipChat.SendHipMessage(null, errorMsg);
            }
        }

        // Extracts the first URL found in the given message.
        public static string GetUrl(string message, out string fullUrl)
        {
            Regex regex = new Regex(@"(?i)\b((?:[a-z][\w-]+:(?:/{1,3}|[a-z0-9%])|www\d{0,3}[.]|[a-z0-9.\-]+[.][a-z]{2,4}/)(?:[^\s()<>]+|\(([^\s()<>]+|(\([^\s()<>]+\)))*\))+(?:\(([^\s()<>]+|(\([^\s()<>]+\)))*\)|[^\s`!()\[\]{};:'"".,<>?«»“”‘’]))");
            Match match = regex.Match(message);
            fullUrl = match.ToString();

            if (fullUrl.Length > 0)
            {
                regex = new Regex(@"(https?://)?(www.)?(.*)");
                Match simplifiedUrl = regex.Match(fullUrl);
                string simplified = simplifiedUrl.Groups[3].ToString();
                if (simplified.EndsWith("/"))
                    simplified = simplified.Substring(0, simplified.Length - 1);
                return simplified;
            }
            else
            {
                return fullUrl;
            }
        }

        public static void SendMessage(string message)
        {
            lock (skypeLock)
            {
                chatRoom.SendMessage(message);
            }
        }

        public static string EscapeStringValue(string value)
        {
            const char BACK_SLASH = '\\';
            const char SLASH = '/';
            const char DBL_QUOTE = '"';
            const char CARRIAGE_RETURN = '\r';
            const char LINE_FEED = '\n';

            var output = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (c)
                {
                    case SLASH:
                        output.AppendFormat("{0}{1}", BACK_SLASH, SLASH);
                        break;

                    case BACK_SLASH:
                        output.AppendFormat("{0}{0}", BACK_SLASH);
                        break;

                    case DBL_QUOTE:
                        output.AppendFormat("{0}{1}", BACK_SLASH, DBL_QUOTE);
                        break;
                    case CARRIAGE_RETURN:
                        //output.Append("\\r");
                        break;
                    case LINE_FEED:
                        output.Append("<br>");
                        break;

                    default:
                        output.Append(c);
                        break;
                }
            }

            return output.ToString();
        }
    }
}
