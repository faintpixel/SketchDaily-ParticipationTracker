using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
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
        private static int CACHE_AFTER_THIS_MANY_THEMES = 30;

        static void Main(string[] args)
        {
            // TO DO:
            // make sure api is returning themes ordered by date
            // hopefully api is returning comments by date also
            // GetFlairForSubreddit make sure it gets all the flair for real

            ParticipationTracker participationTracker = new ParticipationTracker();

            _reddit = new Reddit();

            List<string> postURLs = participationTracker.GetRelevantPostURLs();

            Dictionary<string, User> userDictionary = new Dictionary<string, User>();
            //List<string> postURLs = new List<string>();
            //postURLs.Add(@"http://www.reddit.com/r/SketchDaily/comments/1i7mja/july_13th_serious_saturday_upside_down/");

            int themeNumber = 0;
            foreach (string postURL in postURLs)
            {
                themeNumber += 1;
                bool useCache = themeNumber > CACHE_AFTER_THIS_MANY_THEMES;

                List<Comment> allCommentsForTheme = _reddit.GetComments(postURL, useCache);

                foreach (Comment comment in allCommentsForTheme)
                {
                    if (userDictionary.ContainsKey(comment.Author) == false)
                        userDictionary.Add(comment.Author, new User(comment.Author));
                    User user = userDictionary[comment.Author];

                    participationTracker.ParseComment(postURL, comment, ref user);
                    
                    userDictionary[comment.Author] = user;
                }
            }

            List<User> users = userDictionary.Values.ToList();

            CalculateStreaks(ref users, postURLs);
            ExportStatisticsToFile(users, @"Results.txt");

            Dictionary<string, Flair> currentFlair = GetCurrentUserFlairDictionary();
            SetFlair(users, currentFlair);
            Console.WriteLine("Done.");
        }

        private static void SetFlair(List<User> users, Dictionary<string, Flair> currentFlair)
        {
            string username = ConfigurationManager.AppSettings["Username"];
            string password = ConfigurationManager.AppSettings["Password"];

            Session session = _reddit.Login(username, password);
            List<Flair> updatedFlair = new List<Flair>();

            foreach (User user in users)
            {
                Flair userFlair = new Flair();
                userFlair.Username = user.Username;
                if (user.CurrentStreak == 0)
                    userFlair.Css = "default";
                else if (user.CurrentStreak <= 10)
                    userFlair.Css = "one";
                else if (user.CurrentStreak < 50)
                    userFlair.Css = "ten";
                else if (user.CurrentStreak < 100)
                    userFlair.Css = "fifty";
                else if (user.CurrentStreak < 150)
                    userFlair.Css = "hundred";
                else if (user.CurrentStreak < 200)
                    userFlair.Css = "hundredfifty";
                else if (user.CurrentStreak < 300)
                    userFlair.Css = "twohundred";
                else if (user.CurrentStreak < 365)
                    userFlair.Css = "threehundred";
                else
                    userFlair.Css = "oneyear";

                userFlair.Text = user.CurrentStreak + " " + user.Webpage;

                if (user.CurrentStreak == 0 && string.IsNullOrEmpty(user.Webpage) && userFlair.Css == "default")
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

            _reddit.SetFlairBatch("sketchdaily", updatedFlair, session); // this is important and should not really be commented out.
        }

        

        private static void CalculateStreaks(ref List<User> users, List<string> themes)
        {
            Console.WriteLine("Calculating Streaks");
            foreach (User user in users)
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

        private static void DisplayResults(List<User> users)
        {
            foreach (User user in users)
            {
                Console.WriteLine(user.Username);
                Console.WriteLine("  current streak: " + user.CurrentStreak);
                Console.WriteLine("  longest streak: " + user.LongestStreak);
                Console.WriteLine("  karma: " + user.Karma);
                Console.WriteLine("  total comments: " + user.TotalComments);
                Console.WriteLine("  total links: " + user.TotalLinks);
                Console.WriteLine("  webpage: " + user.Webpage);
            }
        }

        private static void ExportStatisticsToFile(List<User> users, string file)
        {
            StreamWriter writer = File.CreateText(file);
            writer.Write("User,");
            writer.Write("Current Streak,");
            writer.Write("Longest Streak,");
            writer.Write("Karma,");
            writer.Write("Upvotes,");
            writer.Write("Downvotes,");
            writer.Write("Total Comments,");
            writer.Write("Total Links,");
            writer.Write("Webpage");
            writer.WriteLine();            

            foreach (User user in users)
            {
                writer.Write(user.Username + ",");
                writer.Write(user.CurrentStreak + ",");
                writer.Write(user.LongestStreak + ",");
                writer.Write(user.Karma + ",");
                writer.Write(user.Upvotes + ",");
                writer.Write(user.Downvotes + ",");
                writer.Write(user.TotalComments + ",");
                writer.Write(user.TotalLinks + ",");
                writer.Write(user.Webpage);
                writer.WriteLine();
            }

            writer.Close();
        }

        private static void ExportStatisticsToJson(List<User> users, string file)
        {
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

        private static Dictionary<string, Flair> GetCurrentUserFlairDictionary()
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
