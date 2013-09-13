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
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace ParticipationTracker
{
    class Program
    {
        private static Reddit _reddit;
        private static string PARTICIPANTS_FILE = ConfigurationManager.AppSettings["ParticipantsFile"];
        private static string BLACKLIST_FILE = ConfigurationManager.AppSettings["BlackListFile"];
        private static string STATS_FOLDER = ConfigurationManager.AppSettings["UserStatsDirectory"];
        private static string STATS_FTP_FOLDER = ConfigurationManager.AppSettings["FTPStatsDirectory"];
        private static int CACHE_AFTER_THIS_MANY_THEMES = 30;
        private static DateTime INACTIVE_CUTOFF_DATE = DateTime.Now.AddDays(-30);

        static void Main(string[] args)
        {          
            Console.WriteLine("Starting - " + DateTime.Now.ToString());
            ParticipationTracker participationTracker = new ParticipationTracker();
            _reddit = new Reddit();
            

            List<string> postURLs = participationTracker.GetRelevantPostURLs();

            Dictionary<string, User> userDictionary = new Dictionary<string, User>();

            int themeNumber = 0;
            foreach (string postURL in postURLs)
            {
                themeNumber += 1;
                bool useCache = themeNumber > CACHE_AFTER_THIS_MANY_THEMES;

                List<Comment> allCommentsForTheme = _reddit.GetComments(postURL, postURL, useCache);

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
            ExportStatisticsToFile(users, STATS_FOLDER + "Results.txt");
            ExportStatisticsToJson(users, STATS_FOLDER + "Results.json");            

            Dictionary<string, Flair> currentFlair = GetCurrentUserFlairDictionary();
            SetFlair(users, currentFlair);

            foreach (User user in users)
            {
                if (user.MostRecentPost >= INACTIVE_CUTOFF_DATE)
                {
                    string fileName = STATS_FOLDER + user.Username + ".json";
                    bool fileChanged = ExportUserStatisticsToFile(user, fileName);
                    if (fileChanged)
                        UploadResults(STATS_FOLDER + user.Username + ".json", STATS_FTP_FOLDER + user.Username + ".json");
                    else
                        Console.WriteLine("No change to file '" + fileName + "'. Ignoring.");
                }
            }

            UploadResults(STATS_FOLDER + "Results.json", "Results.json");
            Console.WriteLine("Done. " + DateTime.Now.ToString());
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

                userFlair.Text = user.CurrentStreak + " / " + user.DaysPostedLinks.Count;
                if(string.IsNullOrEmpty(user.Webpage) == false)
                    userFlair.Text = userFlair.Text + " - " + user.Webpage;

                if (user.MostRecentPost >= INACTIVE_CUTOFF_DATE) 
                {
                    if (currentFlair.ContainsKey(user.Username))
                    {
                        if (currentFlair[user.Username].Text == userFlair.Text && currentFlair[user.Username].Css == userFlair.Css)
                        {
                            Console.WriteLine("     Ignoring flair for user " + user.Username + ". Text: " + userFlair.Text + ". CSS: " + userFlair.Css + ". Last post on " + user.MostRecentPost.ToString() + ". No change.");
                        }
                        else
                        {
                            Console.WriteLine("Updating flair for user " + user.Username + ". Flair CSS: " + userFlair.Css + ", Flair Text: " + userFlair.Text + ". Most recent post: " + user.MostRecentPost.ToString());
                            updatedFlair.Add(userFlair);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Inserting flair for user " + user.Username + ". Flair CSS: " + userFlair.Css + ", Flair Text: " + userFlair.Text + ". Most recent post: " + user.MostRecentPost.ToString());
                        updatedFlair.Add(userFlair);
                    }
                }
                else
                {
                    if(currentFlair.ContainsKey(user.Username))
                    {
                        userFlair.Css = "";
                        userFlair.Text = "";
                        Console.WriteLine("Clearing flair for user due to inactivity " + user.Username + ". Flair CSS: " + userFlair.Css + ", Flair Text: " + userFlair.Text + ". Most recent post: " + user.MostRecentPost.ToString());
                        updatedFlair.Add(userFlair);
                    }
                    else
                    {
                        Console.WriteLine("     Skipping flair for " + user.Username + " due to inactivity. Most recent post: " + user.MostRecentPost.ToString() + " and no existing flair found.");
                    }
                }
            }

            Console.WriteLine(DateTime.Now.ToString() + " - Setting flair batches");

            _reddit.SetFlairBatch("sketchdaily", updatedFlair, session); 
        }

        private static void CalculateStreaks(ref List<User> users, List<string> themes)
        {
            Console.WriteLine("Calculating Streaks - " + DateTime.Now.ToString());
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
            writer.Write("Number of themes with skips");
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
                writer.Write(user.Webpage + ",");
                writer.Write(user.ExcludedFromStreakLinks.Count);
                writer.WriteLine();
            }

            writer.Close();
        }

        private static void ExportStatisticsToJson(List<User> users, string file)
        {
            string lastUsername = users[users.Count - 1].Username;
            StreamWriter writer = File.CreateText(file);
            writer.WriteLine("{ \"aaData\":[");
            foreach (User user in users)
            {
                writer.WriteLine("  {");
                writer.WriteLine("      \"Username\": \"" + user.Username + "\",");
                writer.WriteLine("      \"CurrentStreak\": \"" + user.CurrentStreak + "\",");
                writer.WriteLine("      \"Downvotes\": \"" + user.Downvotes + "\",");
                writer.WriteLine("      \"LongestStreak\": \"" + user.LongestStreak + "\",");
                writer.WriteLine("      \"TotalComments\": \"" + user.TotalComments + "\",");
                writer.WriteLine("      \"TotalLinks\": \"" + user.TotalLinks + "\",");
                writer.WriteLine("      \"TotalThemesForStreak\": \"" + user.DaysPostedLinks.Count + "\",");
                writer.WriteLine("      \"TotalNoStreakComments\": \"" + user.ExcludedFromStreakLinks.Count + "\",");
                writer.WriteLine("      \"Upvotes\": \"" + user.Upvotes + "\",");
                writer.WriteLine("      \"Karma\": \"" + user.Karma + "\"");
                if(user.Username == lastUsername)
                    writer.WriteLine("  }");
                else
                    writer.WriteLine("  },");
            }
            writer.WriteLine("]}");
            writer.Close();
        }

        private static bool ExportUserStatisticsToFile(User user, string file)
        {
            Console.Write("Exporting user stats to file " + file + "... ");
            MD5 md5 = MD5.Create();
            string originalHash = "";

            if (File.Exists(file))
            {
                Console.Write("Found existing stats file. Opening for comparison... ");
                FileStream originalFileStream = File.OpenRead(file);
                originalHash = BitConverter.ToString(md5.ComputeHash(originalFileStream)).Replace("-", "").ToLower();
                originalFileStream.Close();                
            }

            StreamWriter writer = File.CreateText(file);

            writer.WriteLine("{");
            writer.WriteLine("  \"Username\": \"" + user.Username + "\",");
            writer.WriteLine("  \"CurrentStreak\": \"" + user.CurrentStreak + "\",");
            writer.WriteLine("  \"Downvotes\": \"" + user.Downvotes + "\",");
            writer.WriteLine("  \"LongestStreak\": \"" + user.LongestStreak + "\",");
            writer.WriteLine("  \"TotalComments\": \"" + user.TotalComments + "\",");
            writer.WriteLine("  \"TotalLinks\": \"" + user.TotalLinks + "\",");
            writer.WriteLine("  \"TotalThemesForStreak\": \"" + user.DaysPostedLinks.Count + "\",");
            writer.WriteLine("  \"TotalNoStreakComments\": \"" + user.ExcludedFromStreakLinks.Count + "\",");
            writer.WriteLine("  \"Upvotes\": \"" + user.Upvotes + "\",");
            writer.WriteLine("  \"Karma\": \"" + user.Karma + "\",");
            writer.WriteLine("  \"MostRecentUserActivity\": \"" + user.MostRecentPost.ToString()  + "\",");

            writer.WriteLine("  \"NostreakThemes\" : [ ");
            foreach(string url in user.ExcludedFromStreakLinks)
                writer.WriteLine("      {\"url\": \"" + url + "\"},"); 
            writer.WriteLine("  ],");

            writer.WriteLine("  \"ThemesPostedTo\" : [ ");
            foreach (string url in user.DaysPostedLinks)
                writer.WriteLine("      {\"url\": \"" + url + "\"},");
            writer.WriteLine("  ],");

            writer.WriteLine("}");

            writer.Close();

            Console.WriteLine("Done writing to file.");

            FileStream newFileStream = File.OpenRead(file);
            string newHash = BitConverter.ToString(md5.ComputeHash(newFileStream)).Replace("-", "").ToLower();
            newFileStream.Close();

            return originalHash != newHash;
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

        private static void UploadResults(string source, string destination)
        {
            Console.Write("Uploading file " + source + "... ");
            string username = ConfigurationManager.AppSettings["ftpUsername"];
            string password = ConfigurationManager.AppSettings["ftpPassword"];
            string ftpPath = ConfigurationManager.AppSettings["ftpPath"];

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpPath + destination);
            request.Method = WebRequestMethods.Ftp.UploadFile;

            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential(username, password);

            StreamReader sourceStream = new StreamReader(source);
            byte[] fileContents = Encoding.UTF8.GetBytes(sourceStream.ReadToEnd());
            sourceStream.Close();
            request.ContentLength = fileContents.Length;

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(fileContents, 0, fileContents.Length);
            requestStream.Close();

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            Console.WriteLine("Done. Status {0}", response.StatusDescription);

            response.Close();
        }


    }
}
