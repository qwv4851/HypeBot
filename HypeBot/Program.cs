using System;
using System.Collections.Generic;
using System.Linq;
using SKYPE4COMLib;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using System.Data;
using System.IO;
using System.Configuration;

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
        
        // Initializes the bot and waits for a key press to quit.

        static void Main(string[] args)
        {
            Console.WriteLine("Initializing Skype...");
            Console.WriteLine("Note: You may have to accept a request from the Skype client.");
            InitSkype();
            Console.WriteLine("Finding largest chat room...");
            chatRoom = FindLargestChatRoom();
            InitDatabase();
            skype.MessageStatus += OnMessageStatus;
            Console.WriteLine("Hype Bot Initialized.\n");
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
                    default:
                        Console.WriteLine(String.Format("Error: Invalid command '{0}'. Type 'help' for a list of valid commands.", input));
                        break;
                }
            }
        }

        private static string GetConfig(string key)
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
            ShowCommand("help", "Displays a list of commands.");
            ShowCommand("reset", "Reset the database and parse the chat room for URLs.");
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
                    string url = GetUrl(message.Body);
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

        // Finds the chat room with the highest number of users.

        static Chat FindLargestChatRoom()
        {
            var chats = skype.Chats.Cast<Chat>().OrderByDescending(chat => chat.Members.Count);
            Chat largestChat = chats.First();
            Console.WriteLine("Chat Room: {0}, Members: {1}", largestChat.Name, largestChat.Members.Count);
            return largestChat;
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
                string url = GetUrl(message.Body);
                if (url.Length > 0)
                {
                    //Console.WriteLine("Url detected: " + url);
                    Hype oldHype = GetHype(url);
                    if (oldHype == null)
                    {
                        AddHype(new Hype(sender.Handle, sender.FullName, url, message.Body, message.Timestamp));
                    }
                    else
                    {
                        string errorMsg = String.Format("OLD HYPE DETECTED: Violator {0} ({1}) Old hype URL {2} first posted by {3} on {4}. Original message: \"{5}\"", sender.FullName, sender.Handle, oldHype.url, oldHype.name, oldHype.date, oldHype.body);
                        Console.WriteLine("\n" + errorMsg );
                        chatRoom.SendMessage(errorMsg);
                    }
                }
            }
        }

        // Extracts the first URL found in the given message.

        public static string GetUrl(string message)
        {
            Regex regex = new Regex(@"(?i)\b((?:[a-z][\w-]+:(?:/{1,3}|[a-z0-9%])|www\d{0,3}[.]|[a-z0-9.\-]+[.][a-z]{2,4}/)(?:[^\s()<>]+|\(([^\s()<>]+|(\([^\s()<>]+\)))*\))+(?:\(([^\s()<>]+|(\([^\s()<>]+\)))*\)|[^\s`!()\[\]{};:'"".,<>?«»“”‘’]))");
            Match match = regex.Match(message);
            string fullUrl = match.ToString();
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
    }
}
