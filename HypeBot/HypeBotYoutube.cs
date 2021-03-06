﻿using System;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace HypeBot
{
    class HypeBotYoutube
    {
        private static string apiKey = Program.GetConfig("youtubeAuth");

        public static String YoutubeTitle(string url)
        {
            string videoID;
            string timestamp = null;
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

            Match timestampMatch = Regex.Match(url, @"[\?\&\#]t=(\d*h?\d*m?\d*s?)");
            if (timestampMatch.Groups.Count > 1)
            {
                timestamp = timestampMatch.Groups[1].Value;
            }
                 
            string requestUrl = String.Format("https://www.googleapis.com/youtube/v3/videos?id={0}&key={1}&fields=items(snippet(title%2CpublishedAt)%2Cstatistics)&part=snippet", videoID, apiKey);
            WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUrl);
            string json = String.Empty;

            try
            {
                httpWebRequest.GetResponse();
                HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                string title;

                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    json = streamReader.ReadToEnd();
                    XmlDocument doc = JsonConvert.DeserializeXmlNode(json, "root");
                    XmlElement xmlSnippet = doc["root"]["items"]["snippet"];
                    title = xmlSnippet["title"].InnerText;
                    if (timestamp != null)
                    {
                        title += " [" + url.Substring(url.IndexOf("=") + 1) + "]";
                    }
                    title += " [" + xmlSnippet["publishedAt"].InnerText.Substring(0, 4) + "]";
                }
                return title;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine("JSON: {0}", json);
                return null;
            }
        }
    }
}
