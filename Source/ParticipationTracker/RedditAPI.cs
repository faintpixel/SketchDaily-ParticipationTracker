using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Web;

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

        public RedditSession Login(string username, string password)
        {
            string url = @"http://www.reddit.com/api/login/" + username;
            string parameters = "user=" + username + "&passwd=" + password + "&api_type=json";

            WebResponse response = SendPostData(parameters, url, null);
            StreamReader str = new StreamReader(response.GetResponseStream());
            string jsonResponse = str.ReadToEnd();

            JObject o = JObject.Parse(jsonResponse);
          
            RedditSession session = new RedditSession();
            session.ModHash = (string)o["json"]["data"]["modhash"];
            session.CookieData = (string)o["json"]["data"]["cookie"];

            return session;
        }

        public bool SetFlair(string subreddit, string name, string flairText, string cssClass, RedditSession session)
        {
            string parameters = "r=" + subreddit + "&name=" + name + "&text=" + flairText + "&css_class=" + cssClass + "&uh=" + session.ModHash;
            string url = @"http://www.reddit.com/api/flair";
            WebResponse response = SendPostData(parameters, url, session.CookieData);

            StreamReader str = new StreamReader(response.GetResponseStream());
            string jsonResponse = str.ReadToEnd();

            JObject o = JObject.Parse(jsonResponse);

            return true;
        }

        public bool SetBatchFlair(string subreddit, List<Flair> flair, RedditSession session)
        {
            
            string url = @"http://www.reddit.com/api/flaircsv.json";

            List<string> flairParameters = new List<string>();
            string current = "";
            foreach (Flair userFlair in flair)
            {
                string p = userFlair.Username + "," + userFlair.Text + "," + userFlair.Css + "\n";
                if (current.Length + p.Length >= 100)
                {
                    flairParameters.Add(current);
                    current = "";
                }
                current += p;
            }
            flairParameters.Add(current);

            foreach (string flairCall in flairParameters)
            {
                string parameters = "r=" + subreddit + "&flair_csv=" + flairCall + "&uh=" + session.ModHash;

                WebResponse response = SendPostData(parameters, url, session.CookieData);

                StreamReader str = new StreamReader(response.GetResponseStream());
                string jsonResponse = str.ReadToEnd();

                //JObject o = JObject.Parse(jsonResponse);
            }

            return true;
        }


        private WebResponse SendPostData(string parameters, string url, string cookieData)
        {
            WebRequest request = WebRequest.Create(url);
            ((HttpWebRequest)request).UserAgent = "bot for /r/sketchdaily by /u/artomizer";

            if (string.IsNullOrEmpty(cookieData) == false)
            {
                string sessionId = cookieData.Split(',')[2];
                Cookie cookie = new Cookie("reddit_session", HttpUtility.UrlEncode(cookieData));
                cookie.Domain = "reddit.com";
                ((HttpWebRequest)request).CookieContainer = new CookieContainer();
                ((HttpWebRequest)request).CookieContainer.Add(cookie);
            }
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            Stream reqStream = request.GetRequestStream();

            byte[] postArray = Encoding.ASCII.GetBytes(parameters);
            reqStream.Write(postArray, 0, postArray.Length);
            reqStream.Close();

            WebResponse result;

            try
            {
                result = request.GetResponse();
            }
            catch (WebException ex)
            {
                result = ex.Response;
            }

            System.Threading.Thread.Sleep(3000); // can be 2000, but want to make sure not to overload it
            return result;
        }

    }
}
