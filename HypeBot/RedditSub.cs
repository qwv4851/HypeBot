using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HypeBot
{
    class RedditBot
    {
        private static string username = Program.GetConfig("redditUser");
        private static string password = Program.GetConfig("redditPass");
        private static string subreddit = Program.GetConfig("subreddit");
        public static void Submit(string title, string url)
        { 
            try
            {
                var reddit = new Reddit();
                var user = reddit.LogIn(username, password);
                var sub = reddit.GetSubreddit(subreddit);
                sub.SubmitPost(title, url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
