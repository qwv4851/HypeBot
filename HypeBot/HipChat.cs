using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Globalization;

namespace HypeBot
{
    class HipChat
    {
        private static string hipAuth = Program.GetConfig("hipAuth");
        private static string hipRoom = Program.GetConfig("hipRoom");
        private static string adminAuth = Program.GetConfig("adminAuth");

        public static void InitHipChat()
        {
            // Create a listener.
            HttpListener listener = new HttpListener();
            var addresses = System.Net.Dns.GetHostEntry("").AddressList;

            foreach (var address in addresses)
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    string url = String.Format("http://{0}/", address);
                    Console.WriteLine("Adding prefix: " + url);
                    listener.Prefixes.Add(url);
                }
            }

            DeleteAllWebhooks();
            RegisterWebhook();

            StartListener(listener);
        }

        private static void DeleteAllWebhooks()
        {
            Console.WriteLine("Deleting all webhooks");
            string requestUrl = String.Format("https://api.hipchat.com/v2/room/{0}/webhook?auth_token={1}", hipRoom, adminAuth);
            Console.WriteLine("Request: {0}", requestUrl);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string json = streamReader.ReadToEnd();
                Console.WriteLine("Response:: {0}", json);
                XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                foreach (XmlNode node in doc["root"].ChildNodes)
                {
                    if (node["id"] != null)
                    {
                        string id = node["id"].InnerText;
                        DeleteWebhook(id);
                    }
                }
            }
        }

        private static void DeleteWebhook(string id)
        {
            Console.WriteLine("Deleting webhook {0}", id);
            string requestUrl = String.Format("https://api.hipchat.com/v2/room/{0}/webhook/{1}?auth_token={2}", hipRoom, id, adminAuth);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            httpWebRequest.Method = "DELETE";
            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string result = streamReader.ReadToEnd();
                Console.WriteLine(result);
            }
            Console.WriteLine("Webhook deleted");
        }

        private static void RegisterWebhook()
        {
            string requestUrl = String.Format("https://api.hipchat.com/v2/room/{0}/webhook?auth_token={1}", hipRoom, adminAuth);
            Console.WriteLine("Creating webhook request for {0}", requestUrl);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string ipAddr = GetPublicIP();
                string json = String.Format("{{\"url\":\"http://{0}\",\"event\":\"room_message\",\"name\":\"onMessage\"}}", ipAddr);

                Console.WriteLine("Registering webhook using json: {0}", json);
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string result = streamReader.ReadToEnd();
                Console.WriteLine(result);
            }
        }

        public static string GetPublicIP()
        {
            String direction = "";
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                direction = stream.ReadToEnd();
            }

            //Search for the ip in the html
            int first = direction.IndexOf("Address: ") + 9;
            int last = direction.LastIndexOf("</body>");
            direction = direction.Substring(first, last - first);

            return direction;
        }

        private static void StartListener(HttpListener listener)
        {
            listener.Start();
            Console.WriteLine("Started listener");

            while (true)
            {
                Console.WriteLine("Listening...");
                // Note: The GetContext method blocks while waiting for a request. 
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    if (context.Request.UserAgent == "HipChat.com" && context.Request.ContentType == "application/json")
                    {
                        Console.WriteLine("Message received");
                        byte[] inputBuffer = new byte[request.ContentLength64];
                        request.InputStream.Read(inputBuffer, 0, inputBuffer.Length);
                        string json = Encoding.UTF8.GetString(inputBuffer);
                        try
                        {
                            XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                            if (doc["root"]["event"] != null)
                            {
                                if (doc["root"]["event"].InnerText == "room_message")
                                {
                                    XmlNode messageData = doc["root"]["item"]["message"];
                                    string sender = messageData["from"]["name"].InnerText;
                                    string handle = messageData["from"]["mention_name"].InnerText;
                                    string message = messageData["message"].InnerText;
                                    string dateStr = messageData["date"].InnerText;
                                    DateTime date = DateTime.Parse(dateStr, null, DateTimeStyles.RoundtripKind).ToLocalTime();
                                    string skypeMessage = String.Format("{0}: {1}", sender, message);
                                    Console.WriteLine("Sending skype message:" + skypeMessage);
                                    Program.SendMessage(skypeMessage);

                                    string fullUrl;
                                    string url = Program.GetUrl(message, out fullUrl);
                                    if (url.Length > 0)
                                    {
                                        Program.CheckHype(message, date, sender, handle, url);
                                    }
                               }
                                else
                                {
                                    Console.WriteLine("Notification type: {0}", doc["root"]["event"].InnerText);
                                }
                            }
                           
                        }
                        catch (NullReferenceException nullRefEx)
                        {
                            Console.WriteLine(nullRefEx);
                            Console.WriteLine("Original JSON: " + json);
                        }
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }
        }
        public static void SendHipMessage(string sender, string message)
        {
            message = EscapeStringValue(message);
            if (sender != null)
            {
                sender = EscapeStringValue(sender);
            }
            WebRequest request = WebRequest.Create(String.Format("https://api.hipchat.com/v2/room/{0}/notification?auth_token={1}", hipRoom, hipAuth));
            request.Method = "POST";
            string json;
            if (sender == null)
            {
                json = String.Format("{{ \"color\":\"purple\",\"message\": \"<b>{0}</b>\"}}", message);
            }
            else
            {
                json = String.Format("{{ \"color\":\"purple\",\"message\": \"<b>{0}: </b>{1}\"}}", sender, message);
            }
            
            Console.WriteLine("Sending hip message json: {0}", json);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);

            WebResponse response = request.GetResponse();
            using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
            {
                string result = streamReader.ReadToEnd();
                Console.WriteLine(result);
            }
            
            dataStream.Close();
            response.Close();
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
