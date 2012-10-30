using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

namespace ParticipationTracker
{
    class Program
    {

        private static RedditAPI _reddit;

        static void Main(string[] args)
        {
            _reddit = new RedditAPI();

            List<Post> posts = _reddit.GetAllPostsForSubreddit(@"http://www.reddit.com/r/sketchdaily/");
            ExportPostURLSToFile(posts, @"c:\skd\ParticipationTracker\FullPostList.txt");

            //List<string> postList = GetOrderedListOfThemes();

            posts.RemoveAt(0); // remove the first one since the day is not over yet

            List<string> themeList = RemoveBlacklistedPosts(posts);

            Dictionary<string, UserParticipation> participation = new Dictionary<string, UserParticipation>();

            foreach (string theme in themeList)
            {
                List<XmlDocument> allCommentsForTheme = _reddit.GetAllCommentsForPost(theme);

                foreach (XmlDocument xml in allCommentsForTheme)
                {

                    XmlNodeList comments = xml.SelectNodes("//children");

                    List<Comment> c = ParseComments(comments);

                    foreach (Comment comment in c)
                    {
                        if (participation.ContainsKey(comment.Author) == false)
                            participation.Add(comment.Author, new UserParticipation(comment.Author));

                        UserParticipation p = participation[comment.Author];
                        p.TotalComments += 1;
                        if (comment.Body.Contains("[") && comment.Body.Contains("]")) // this is a pretty crappy check.
                        {
                            p.TotalLinks += 1;
                            if (p.DaysPostedLinks.Contains(theme) == false)
                                p.DaysPostedLinks.Add(theme);
                        }
                        p.Upvotes += comment.Ups;
                        p.Downvotes += comment.Downs;

                        participation[comment.Author] = p;
                    }
                }
            }

            List<UserParticipation> participationList = participation.Values.ToList();

            SetStreakInfo(ref participationList, themeList);
            DisplayResults(participationList);
            ExportResultsToFile(participationList, @"c:\skd\ParticipationTracker\Results.txt");
        }

        private static List<string> RemoveBlacklistedPosts(List<Post> posts)
        {
            List<string> filteredList = new List<string>();
            StreamReader reader = File.OpenText(@"c:\skd\ParticipationTracker\BlackList.txt");
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

        private static void SetStreakInfo(ref List<UserParticipation> participation, List<string> themes)
        {
            Console.WriteLine("Calculating Streaks");
            foreach (UserParticipation user in participation)
            {
                string streak = "";
                foreach (string theme in themes)
                    if (user.DaysPostedLinks.Contains(theme))
                        streak = streak + "1";
                    else
                        streak = streak + "0";

                string longest = streak.Split('0').OrderByDescending(s => s.Length).First();

                user.LongestStreak = longest.Count();
                user.CurrentStreak = streak.Split('0')[0].Length;
            }
        }

        private static void DisplayResults(List<UserParticipation> participation)
        {
            foreach (UserParticipation p in participation)
            {
                Console.WriteLine(p.Username);
                Console.WriteLine("  current streak: " + p.CurrentStreak);
                Console.WriteLine("  longest streak: " + p.LongestStreak);
                Console.WriteLine("  karma: " + p.Karma);
                Console.WriteLine("  total comments: " + p.TotalComments);
                Console.WriteLine("  total links: " + p.TotalLinks);
            }
        }

        private static void ExportResultsToFile(List<UserParticipation> participation, string file)
        {
            StreamWriter writer = File.CreateText(file);
            writer.Write("User,");
            writer.Write("Current Streak,");
            writer.Write("Longest Streak,");
            writer.Write("Karma,");
            writer.Write("Upvotes,");
            writer.Write("Downvotes,");
            writer.Write("Total Comments,");
            writer.Write("Total Links");
            writer.WriteLine();            

            foreach (UserParticipation p in participation)
            {
                writer.Write(p.Username + ",");
                writer.Write(p.CurrentStreak + ",");
                writer.Write(p.LongestStreak + ",");
                writer.Write(p.Karma + ",");
                writer.Write(p.Upvotes + ",");
                writer.Write(p.Downvotes + ",");
                writer.Write(p.TotalComments + ",");
                writer.Write(p.TotalLinks);
                writer.WriteLine();
            }

            writer.Close();
        }

        private static void ExportPostURLSToFile(List<Post> posts, string file)
        {
            StreamWriter writer = File.CreateText(file);

            foreach (Post post in posts)
            {
                writer.WriteLine(post.URL);
            }
            writer.Close();
        }

        private static List<Comment> ParseComments(XmlNodeList comments)
        {
            List<Comment> results = new List<Comment>();

            foreach (XmlNode comment in comments)
            {
                Comment c = new Comment();

                if (comment.SelectSingleNode("data") != null && comment.SelectSingleNode("kind").InnerText != "more" && comment.SelectSingleNode("kind").InnerText != "t3")
                {
                    c.Author = comment.SelectSingleNode("data/author").InnerText;
                    c.Body = comment.SelectSingleNode("data/body").InnerText;
                    c.CreatedUTC = comment.SelectSingleNode("data/created").InnerText;
                    c.Downs = int.Parse(comment.SelectSingleNode("data/downs").InnerText);
                    c.Ups = int.Parse(comment.SelectSingleNode("data/ups").InnerText);
                    c.Flair = comment.SelectSingleNode("data/author_flair_css_class").InnerText;

                    results.Add(c);
                }
            }

            return results;
        }


    }
}
