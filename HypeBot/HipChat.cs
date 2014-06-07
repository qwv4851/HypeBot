using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;

namespace HypeBot
{
    class HipChat
    {
        private static string hipAuth = Program.GetConfig("hipAuth");
        private static string hipRoom = Program.GetConfig("hipRoom");

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

            listener.Start();
            Console.WriteLine("Started listener");

            while (true)
            {
                Console.WriteLine("Listening...");
                // Note: The GetContext method blocks while waiting for a request. 
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    Console.WriteLine("Message received");
                    HttpListenerRequest request = context.Request;
                    byte[] inputBuffer = new byte[request.ContentLength64];
                    request.InputStream.Read(inputBuffer, 0, inputBuffer.Length);
                    string json = Encoding.UTF8.GetString(inputBuffer);
                    XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                    XmlNode messageData = doc["root"]["item"]["message"];
                    string sender = messageData["from"]["name"].InnerText;
                    string message = messageData["message"].InnerText;
                    string skypeMessage = String.Format("{0}: {1}", sender, message);
                    Console.WriteLine("Sending skype message:" + skypeMessage);
                    Program.SendMessage(skypeMessage);
                    Console.WriteLine("Message sent");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }
        }
        public static void SendHipMessage(string sender, string message)
        {
            WebRequest request = WebRequest.Create(String.Format("https://api.hipchat.com/v2/room/{0}/notification?auth_token={1}", hipRoom, hipAuth));
            request.Method = "POST";
            string json = String.Format("{{ \"color\":\"purple\",\"message\": \"{0}: {1}\"}}", sender, message);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            WebResponse response = request.GetResponse();
            dataStream.Close();
            response.Close();
        }
    }
}
