using RedditAPI;
using RedditAPI.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ParticipationTracker
{
    public class ParticipationTracker
    {
        private Reddit _reddit;
        private static string BLACKLIST_FILE = ConfigurationManager.AppSettings["BlackListFile"];
        
        public ParticipationTracker()
        {
            _reddit = new Reddit();
        }

        public List<string> GetRelevantPostURLs()
        {
            List<Post> posts = _reddit.GetAllPostsForSubreddit("sketchdaily");
            LogPostURLSToFile(posts, "FullPostList.txt");

            posts.RemoveAt(0); // remove the first one since the day is not over yet

            List<string> themeList = RemoveBlacklistedPosts(posts);

            return themeList;
        }

        private List<string> RemoveBlacklistedPosts(List<Post> posts)
        {
            List<string> filteredList = new List<string>();
            StreamReader reader = File.OpenText(BLACKLIST_FILE);
            List<string> blacklist = new List<string>();

            string line = reader.ReadLine();
            while (line != null)
            {
                blacklist.Add(line);
                line = reader.ReadLine();
            }
            reader.Close();

            foreach (Post post in posts)
                if (blacklist.Contains(post.URL) == false)
                    filteredList.Add(post.URL);

            return filteredList;
        }

        private void LogPostURLSToFile(List<Post> posts, string file)
        {
            StreamWriter writer = File.CreateText(file);

            foreach (Post post in posts)
            {
                writer.WriteLine("(" + post.CreationDate + ")" + post.URL);
            }
            writer.Close();
        }

        public void ParseComment(string themeURL, Comment comment, ref User user)
        {
            user.TotalComments += 1;

            //check if they posted a link
            if (comment.BodyHTML.Contains("&lt;a") && comment.BodyHTML.Contains("&gt;")) 
            {
                user.TotalLinks += 1;
                if (user.DaysPostedLinks.Contains(themeURL) == false)
                    user.DaysPostedLinks.Add(themeURL);
            }

            // check for webpage link
            if (comment.BodyHTML.ToLower().Contains("my webpage"))
            {
                string pattern = "&lt;a href=\\\"(.*?)\\\"&gt;my webpage&lt;/a&gt;";
                Match match = Regex.Match(comment.BodyHTML.ToLower(), pattern);
                if(match.Success)
                    user.Webpage = match.Groups[1].Value;
            }
            user.Upvotes += comment.Ups;
            user.Downvotes += comment.Downs;
        }
    }
}
