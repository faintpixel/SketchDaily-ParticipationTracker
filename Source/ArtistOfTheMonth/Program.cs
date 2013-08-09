using RedditAPI;
using RedditAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtistOfTheMonth
{
    class Program
    {
        private static Reddit _reddit;

        static void Main(string[] args)
        {
            _reddit = new Reddit();

            List<Post> posts = GetAllPosts();
            posts.RemoveRange(30, posts.Count - 30);
            
            Dictionary<string, User> username_user = new Dictionary<string, User>();

            foreach (Post post in posts)
            {
                List<Comment> allCommentsForPost = _reddit.GetComments(post.URL, post.URL, false);
                foreach (Comment comment in allCommentsForPost)
                {
                    string pattern = "&lt;a href=\"(.*?)\"&gt;";
                    Match match = Regex.Match(comment.BodyHTML, pattern);
                    if (match.Success)
                    {
                        if (username_user.ContainsKey(comment.Author) == false)
                            username_user.Add(comment.Author, new User { Username = comment.Author });
                        User user = username_user[comment.Author];
                        Link link = new Link();
                        link.Url = match.Groups[1].Value;
                        link.Theme = post.Title;
                        user.linksPosted.Add(link);

                        username_user[user.Username] = user;
                    }
                }
            }

            List<User> drawbotsFavoriteUsers = new List<User>();
            List<User> users = username_user.Values.ToList();
            foreach (User user in users)
            {
                if (user.linksPosted.Count >= 15)
                    drawbotsFavoriteUsers.Add(user);
            }
            string x = users[0].Username;

            Random rnd = new Random();
            int i = rnd.Next(drawbotsFavoriteUsers.Count);

            User artistOfTheMonth = drawbotsFavoriteUsers[i];


            StreamWriter writer = File.CreateText("artistOfTheMonth.txt");
            writer.WriteLine("beep boop it's time for drawbot's Artist of the Moment!");
            writer.WriteLine();
            writer.WriteLine(artistOfTheMonth.Username + " has been selected.");
            writer.WriteLine();
            
            foreach (Link link in artistOfTheMonth.linksPosted)
            {
                writer.WriteLine("[" + link.Theme + "](" + link.Url + ")");
                writer.WriteLine();
            }
            writer.Close();
        }

        public static List<Post> GetAllPosts()
        {
            List<Post> posts = _reddit.GetAllPostsForSubreddit("sketchdaily");

            posts.RemoveAt(0); 

            return posts;
        }
    }
}
