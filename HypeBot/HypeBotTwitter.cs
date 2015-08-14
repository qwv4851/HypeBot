using HypeBot;
using System;
using System.Text.RegularExpressions;
using Twitterizer;

namespace HypeBot
{
    class HypeBotTwitter
    {
        private OAuthTokens tokens;

        public HypeBotTwitter()
        {
            tokens = new OAuthTokens();

            tokens.ConsumerKey = Program.GetConfig("consumerKey");
            tokens.ConsumerSecret = Program.GetConfig("consumerSecret");
            tokens.AccessToken = Program.GetConfig("accessToken");
            tokens.AccessTokenSecret = Program.GetConfig("accessTokenSecret");
        }

        public void Tweet(string tweet)
        {
            string pattern = @"(?:(?<=\s)|^)#(\w*[A-Za-z_\-]+\w*)";

            Match match = Regex.Match(tweet, pattern);

            if (match.Success)
            {
                tweet = Program.EscapeStringValue(tweet);
                var options = new StatusUpdateOptions() { APIBaseAddress = "https://api.twitter.com/1.1/" };
                Console.WriteLine("Sending tweet: {0}", tweet);
                var response = TwitterStatus.Update(tokens, tweet, options);
                if (response.ErrorMessage != "Unable to parse JSON")
                {
                    Console.WriteLine(response.ErrorMessage);
                }
                else
                {
                    Console.WriteLine("Tweet sent");
                }
            }     
        }        
    }
}
