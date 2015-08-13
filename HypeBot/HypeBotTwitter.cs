using System;
using System.Text.RegularExpressions;
using Twitterizer;

namespace HypeBotTwitter
{
    class HypeBotTwitter
    {
        private OAuthTokens tokens;

        public HypeBotTwitter()
        {
            tokens = new OAuthTokens();

            tokens.ConsumerKey = Program.GetConfig("ConsumerKey");
            tokens.ConsumerSecret = Program.GetConfig("ConsumerSecret");
            tokens.AccessToken = Program.GetConfig("AccessToken");
            tokens.AccessTokenSecret = Program.GetConfig("AccessTokenSecret");
        }

        public void Tweet(string msg)
        {
            string pattern = @"(?:(?<=\s)|^)#(\w*[A-Za-z_\-]+\w*)";
            string tweet;

            Match match = Regex.Match(msg, pattern);

            if (match.Success)
            {
                // only if you want to tweet hashtags
                // tweet = match.Groups[0].Value;

                // if you want to tweet entire message
                tweet = msg;
                var options = new StatusUpdateOptions() { APIBaseAddress = "https://api.twitter.com/1.1/" };

                var response = TwitterStatus.Update(tokens, tweet, options);

                if (response.ErrorMessage != "Unable to parse JSON")
                {
                    Console.WriteLine(response.ErrorMessage);
                }
            }     
        }        
    }
}
