using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using Newtonsoft.Json;

namespace ParticipationTracker
{
    public class RedditAPI
    {
        public List<XmlDocument> GetAllCommentsForPost(string postURL)
        {
            List<XmlDocument> documents = new List<XmlDocument>();

            XmlDocument mainContent = GetXML(postURL + ".json");

            documents.Add(mainContent);

            // check if it contains 'more' and if it does, loop through and merge them together so we have everything
            XmlNodeList missingNodes = mainContent.SelectNodes("//children[kind=\"more\"]/data/id");
            foreach (XmlNode missingComment in missingNodes)
            {
                string commentURL = postURL + missingComment.InnerText + "/";
                documents.AddRange(GetAllCommentsForPost(commentURL));
            }

            return documents;
        }

        public List<Post> GetAllPostsForSubreddit(string subredditURL)
        {
            List<Post> posts = new List<Post>();

            subredditURL = subredditURL + ".json?limit=100";

            string nextPageURL = subredditURL;
            bool running = true;

            while (running)
            {
                XmlDocument pageXML = GetXML(nextPageURL);
                posts.AddRange(MapPosts(pageXML));
                string nextPage = pageXML.SelectSingleNode("//after").InnerText;
                if (string.IsNullOrEmpty(nextPage) == false)
                    nextPageURL = subredditURL + "&after=" + nextPage;
                else
                    running = false;
            }

            posts = posts.OrderBy(p => p.CreationDate).ToList();
            posts.Reverse();



            return posts;
        }

        private List<Post> MapPosts(XmlDocument xml)
        {
            List<Post> results = new List<Post>();

            XmlNodeList xmlPosts = xml.SelectNodes("//children");

            foreach (XmlNode postXML in xmlPosts)
            {
                Post post = new Post();

                post.URL = @"http://www.reddit.com" + postXML.SelectSingleNode("data/permalink").InnerText;
                string dateString = postXML.SelectSingleNode("data/created").InnerText;
                double ms = double.Parse(dateString);
                post.CreationDate = long.Parse(dateString);
                post.Utc = dateString;

                results.Add(post);
            }

            return results;
        }

        private XmlDocument GetXML(string postURL)
        {
            XmlDocument doc = GetCachedVersion(postURL);

            if (doc == null)
            {
                Console.WriteLine("Getting comments for " + postURL);
                WebClient client = new WebClient();
                client.Headers["User-Agent"] = "bot for /r/sketchdaily by /u/artomizer";

                string json = client.DownloadString(postURL);

                json = json.Replace("selftext", "body");
                json = json.Replace("created_utc", "created");

                doc = (XmlDocument)JsonConvert.DeserializeXmlNode("{\"root\":" + json + "}", "root");
                SaveCachedVersion(postURL, doc);

                System.Threading.Thread.Sleep(3000); // can be 2000, but want to make sure not to overload it
            }
            else
            {
                Console.WriteLine("Getting comments for " + postURL + " from cache");
            }

            return doc;
        }

        private XmlDocument GetCachedVersion(string postURL)
        {
            postURL = System.Web.HttpUtility.UrlEncode(postURL); 
            XmlDocument xml = null;

            if (System.IO.File.Exists(@"c:\skd\ParticipationTracker\Cache\" + postURL))
            {
                xml = new XmlDocument();
                xml.Load(@"c:\skd\ParticipationTracker\Cache\" + postURL);
            }

            return xml;
        }

        private void SaveCachedVersion(string postURL, XmlDocument doc)
        {
            postURL = System.Web.HttpUtility.UrlEncode(postURL); 
            doc.Save(@"c:\skd\ParticipationTracker\Cache\" + postURL);
        }
    }
}
