using System;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

namespace HypeBot
{
    class HypeBotYoutube
    {
        private static string apiKey = Program.GetConfig("youtubeAuth");

        public static String YoutubeTitle(string url)
        {
            if (!url.Contains("youtube.com") && !url.Contains("youtu.be")) 
            {
                return null;
            }
            
            string videoID = url.Substring(url.Length - 11);
          
            string requestUrl = String.Format("https://www.googleapis.com/youtube/v3/videos?id={0}&key={1}&fields=items(snippet(title%2CpublishedAt)%2Cstatistics)&part=snippet", videoID, apiKey);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            try
            {
                httpWebRequest.GetResponse();
                HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                string title;
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    string json = streamReader.ReadToEnd();
                    XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                    XmlElement xmlSnippet = doc["root"]["items"]["snippet"];
                    title = xmlSnippet["title"].InnerText;
                    title += " [" + xmlSnippet["publishedAt"].InnerText.Substring(0, 4) + "]";
                }
                return title;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }
}
