using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using System.Configuration;
using RedditAPI;
using RedditAPI.Models;

namespace ParticipationTracker
{
    class Program
    {

        private static Reddit _reddit;
        private static string PARTICIPANTS_FILE = ConfigurationManager.AppSettings["ParticipantsFile"];
        private static string BLACKLIST_FILE = ConfigurationManager.AppSettings["BlackListFile"];

        static void Main(string[] args)
        {
            _reddit = new Reddit();

            List<Post> posts = _reddit.GetAllPostsForSubreddit("sketchdaily");
            ExportPostURLSToFile(posts, "FullPostList.txt");

            posts.RemoveAt(0); // remove the first one since the day is not over yet

            List<string> themeList = RemoveBlacklistedPosts(posts);

            Dictionary<string, UserParticipation> participation = new Dictionary<string, UserParticipation>();

            int themeNumber = 0;
            foreach (string theme in themeList)
            {
                themeNumber += 1;
                bool useCache = themeNumber > 30;

                List<Comment> allCommentsForTheme = _reddit.GetComments(theme, useCache);

                foreach (Comment comment in allCommentsForTheme)
                {
                    if (participation.ContainsKey(comment.Author) == false)
                        participation.Add(comment.Author, new UserParticipation(comment.Author));
                    
                    UserParticipation p = participation[comment.Author];
                    p.TotalComments += 1;

                    if (comment.BodyHTML.Contains("&lt;a") && comment.BodyHTML.Contains("&gt;")) // this is a pretty crappy check.
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

            List<UserParticipation> participationList = participation.Values.ToList();

            SetStreakInfo(ref participationList, themeList);
            ExportResultsToFile(participationList, @"Results.txt");

            Dictionary<string, Flair> currentFlair = CreateFlairDictionary();
            SetFlair(participationList, currentFlair);
            Console.WriteLine("Done.");
        }

        private static Dictionary<string, UserFlair> LoadParticipatingUsers()
        {
            Dictionary<string, UserFlair> users = new Dictionary<string, UserFlair>();

            StreamReader reader = File.OpenText(PARTICIPANTS_FILE);
            string line = reader.ReadLine();
            while (line != null)
            {
                UserFlair user = new UserFlair();
                string[] flairInfo = line.Split(',');
                user.Username = flairInfo[0];
                user.DefaultFlair = flairInfo[2];
                user.Webpage = flairInfo[1];

                users.Add(user.Username, user);
                line = reader.ReadLine();
            }
            reader.Close();

            return users;
        }

        private static void SetFlair(List<UserParticipation> participation, Dictionary<string, Flair> currentFlair)
        {
            Dictionary<string, UserFlair> participatingUsers = LoadParticipatingUsers();

            string username = ConfigurationManager.AppSettings["Username"];
            string password = ConfigurationManager.AppSettings["Password"];

            Session session = _reddit.Login(username, password);
            List<Flair> updatedFlair = new List<Flair>();

            foreach (UserParticipation user in participation)
            {
                Flair userFlair = new Flair();
                userFlair.Username = user.Username;
                if (user.CurrentStreak == 0)
                    userFlair.Css = "default";
                else if (user.CurrentStreak <= 10)
                    userFlair.Css = "streak" + user.CurrentStreak;
                else if (user.CurrentStreak < 20)
                    userFlair.Css = "streak10plus";
                else if (user.CurrentStreak < 30)
                    userFlair.Css = "streak20plus";
                else if (user.CurrentStreak < 40)
                    userFlair.Css = "streak30plus";
                else if (user.CurrentStreak < 50)
                    userFlair.Css = "streak40plus";
                else if (user.CurrentStreak < 60)
                    userFlair.Css = "streak50plus";
                else if (user.CurrentStreak < 70)
                    userFlair.Css = "streak60plus";
                else if (user.CurrentStreak < 80)
                    userFlair.Css = "streak70plus";
                else if (user.CurrentStreak < 90)
                    userFlair.Css = "streak80plus";
                else if (user.CurrentStreak < 100)
                    userFlair.Css = "streak90plus";
                else if (user.CurrentStreak < 110)
                    userFlair.Css = "streak100plus";
                else if (user.CurrentStreak < 120)
                    userFlair.Css = "streak110plus";
                else if (user.CurrentStreak < 130)
                    userFlair.Css = "streak120plus";
                else if (user.CurrentStreak < 140)
                    userFlair.Css = "streak130plus";
                else if (user.CurrentStreak < 150)
                    userFlair.Css = "streak140plus";
                else
                    userFlair.Css = "streak150plus";

                string webpage = "";
                if (participatingUsers.ContainsKey(user.Username))
                {
                    if (user.CurrentStreak == 0)
                    {
                        userFlair.Css = participatingUsers[user.Username].DefaultFlair;
                        if (string.IsNullOrEmpty(userFlair.Css))
                            userFlair.Css = "default";
                    }

                    webpage = participatingUsers[user.Username].Webpage;
                }

                userFlair.Text = "(" + user.CurrentStreak + ") " + webpage;

                if (user.CurrentStreak == 0 && string.IsNullOrEmpty(webpage) && userFlair.Css == "default")
                {
                    userFlair.Text = "";
                    userFlair.Css = "";
                }

                if (user.CurrentStreak != 0 || currentFlair.ContainsKey(user.Username))
                {
                    Console.WriteLine("Setting flair for " + user.Username + " - " + userFlair.Css + " - " + userFlair.Text);
                    updatedFlair.Add(userFlair);
                }
                else
                {
                    //Console.WriteLine("Ignoring flair for user " + user.Username);
                }

               
            }

            _reddit.SetFlairBatch("sketchdaily", updatedFlair, session);
        }

        private static List<string> RemoveBlacklistedPosts(List<Post> posts)
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
                    c.BodyHTML = comment.SelectSingleNode("data/body_html").InnerText;
                    c.CreatedUTC = comment.SelectSingleNode("data/created").InnerText;
                    c.Downs = int.Parse(comment.SelectSingleNode("data/downs").InnerText);
                    c.Ups = int.Parse(comment.SelectSingleNode("data/ups").InnerText);
                    c.Flair = comment.SelectSingleNode("data/author_flair_css_class").InnerText;

                    results.Add(c);
                }
            }

            return results;
        }

        private static Dictionary<string, Flair> CreateFlairDictionary()
        {
            Dictionary<string, Flair> flairDictionary = new Dictionary<string, Flair>();
            List<Flair> flair = _reddit.GetFlairForSubreddit("sketchdaily");

            foreach (Flair f in flair)
            {
                flairDictionary.Add(f.Username, f);
            }

            return flairDictionary;
        }


    }
}
