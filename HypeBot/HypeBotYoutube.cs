using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

namespace HypeBot
{
    class HypeBotYoutube
    {
        private const string apiKey = "AIzaSyDeTj3OLckTP3y6V889Nz9Zw0eUBfOEKpY";

        public static String YoutubeTitle(string url)
        {
            if (!url.Contains("youtube.com") && !url.Contains("youtu.be")) {
                return null;
            }
            
            string title;
            string videoID = url.Substring(url.Length - 11);
          
            string requestUrl = String.Format("https://www.googleapis.com/youtube/v3/videos?id={0}&key={1}&fields=items(snippet(title%2CpublishedAt)%2Cstatistics)&part=snippet", videoID, apiKey);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            httpWebRequest.GetResponse();
            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string json = streamReader.ReadToEnd();
                XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                title = doc["root"]["items"]["snippet"]["title"].InnerText;
                title += " [" + doc["root"]["items"]["snippet"]["publishedAt"].InnerText.Substring(0, 4) + "]";
            }
            return title;
        }
    }
}
