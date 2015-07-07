using System;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace HypeBot
{
    class HypeBotYoutube
    {
        private const string apiKey = "AIzaSyDeTj3OLckTP3y6V889Nz9Zw0eUBfOEKpY";

        public static String YoutubeTitle(string url)
        {
            string videoID;
            bool timestamp = false;
            string sPattern = "^.*(youtu.be\\/|v\\/|u\\/\\w\\/|embed\\/|watch\\?v=|\\&v=)([^#\\&\\?]{11}).*";
            
            Match match = Regex.Match(url, sPattern);
            
            if (match.Success)
            {
                videoID = match.Groups[2].Value;
            }
            else
            {
                return null;
            }

            if (url.Contains("t="))
            {
                timestamp = true;
            }
                 
            string requestUrl = String.Format("https://www.googleapis.com/youtube/v3/videos?id={0}&key={1}&fields=items(snippet(title%2CpublishedAt)%2Cstatistics)&part=snippet", videoID, apiKey);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
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
                    if (timestamp)
                    {
                        title += " [" + url.Substring(url.LastIndexOf("t=") + 2) + "]";
                    }
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
