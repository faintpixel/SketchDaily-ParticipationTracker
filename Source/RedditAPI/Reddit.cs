using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedditAPI.Models;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.Xml;
using Newtonsoft.Json;

namespace RedditAPI
{
    public class Reddit
    {
        private readonly int API_DELAY;
        private readonly string CACHE_DIRECTORY;
        private HttpHelper _httpHelper;
        private DateTime? _timeOfLastAPIRequest;
        
        public Reddit()
        {
            _httpHelper = new HttpHelper();
            API_DELAY = int.Parse(ConfigurationManager.AppSettings["APIDelay"]);
            CACHE_DIRECTORY = ConfigurationManager.AppSettings["CacheDirectory"];
            _timeOfLastAPIRequest = null;
        }

        public Session Login(string username, string password)
        {
            string url = @"http://www.reddit.com/api/login/" + username;
            string parameters = "user=" + username + "&passwd=" + password + "&api_type=json";

            Wait();
            string response = _httpHelper.SendPost(url, parameters);

            JObject data = JObject.Parse(response);
            JArray errors = (JArray)data["json"]["errors"];
            if (errors.Count > 0)
                throw new Exception("Error logging in."); // should update this to show the actual error(s)
            else
            {
                Session session = new Session();
                session.ModHash = (string)data["json"]["data"]["modhash"];
                session.CookieData = (string)data["json"]["data"]["cookie"];

                return session;
            }
        }

        public List<string> SetFlairBatch(string subreddit, List<Flair> flair, Session session)
        {
            List<string> errors = new List<string>();
            string url = @"http://www.reddit.com/api/flaircsv.json";

            List<string> flairParameters = new List<string>();
            string current = "";
            foreach (Flair userFlair in flair)
            {
                string p = userFlair.Username + "," + userFlair.Text + "," + userFlair.Css + "\n";
                if (current.Length + p.Length >= 100) // stupid limit, but blame the reddit api
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

                Wait();
                string response = _httpHelper.SendPost(url, parameters, session);

                JArray results = JArray.Parse(response);
                foreach (JObject result in results)
                {
                    bool success = (bool)result["ok"];
                    if (success == false)
                        errors.Add((string)result["status"]); // couldn't make it error, so no idea if this actually works
                }

            }

            return errors;
        }

        public List<Flair> GetFlairForSubreddit(string subreddit, string after = null)
        {
            List<Flair> flairList = new List<Flair>();

            string url = "http://www.reddit.com/r/" +subreddit + "/api/flairlist.json?&limit=1000";
            if (string.IsNullOrEmpty(after) == false)
                url = url + "&after=" + after;

            Wait();
            string json = _httpHelper.SendGet(url);
            
            JObject data = JObject.Parse(json);
            JArray users = (JArray)data["users"];

            foreach (JObject user in users)
            {
                Flair flair = new Flair();
                flair.Css = (string)user["flair_css_class"];
                flair.Text = (string)user["flair_text"];
                flair.Username = (string)user["user"];

                flairList.Add(flair);
            }

            string next = (string)data["next"];

            if (string.IsNullOrEmpty(next) == false)
                flairList.AddRange(GetFlairForSubreddit(subreddit, next));

            return flairList;
        }

        public List<Post> GetAllPostsForSubreddit(string subreddit)
        {
            List<Post> posts = new List<Post>();

            string url = "http://www.reddit.com/r/" + subreddit + ".json?limit=100";

            posts = GetSubredditPosts("sketchdaily", null);

            posts = posts.OrderBy(p => p.CreationDate).ToList();
            posts.Reverse();

            return posts;
        }

        public List<XmlDocument> GetAllCommentsForPost(string postURL, bool useCache) // this still needs to be cleaned up. 
        {
            List<XmlDocument> documents = new List<XmlDocument>();

            XmlDocument mainContent = GetXML(postURL + ".json", useCache);

            documents.Add(mainContent);

            // check if it contains 'more' and if it does, loop through and merge them together so we have everything
            XmlNodeList missingNodes = mainContent.SelectNodes("//children[kind=\"more\"]/data/id");
            foreach (XmlNode missingComment in missingNodes)
            {
                string commentURL = postURL + missingComment.InnerText + "/";
                documents.AddRange(GetAllCommentsForPost(commentURL, useCache));
            }

            return documents;
        }

        private List<Post> GetSubredditPosts(string subreddit, string after)
        {
            List<Post> posts = new List<Post>();

            string url = "http://www.reddit.com/r/" + subreddit + "/.json?limit=100";
            if (string.IsNullOrEmpty(after) == false)
                url = url + "&after=" + after;

            Wait();
            string json = _httpHelper.SendGet(url);

            JObject data = JObject.Parse(json);
            JArray allPosts = (JArray)data["data"]["children"];

            foreach (JObject post in allPosts)
            {
                Post p = new Post();
                p.URL = "http://www.reddit.com" + (string)post["data"]["permalink"]; // doing it this way because if you use URL and it's not a self post you'll end up with the wrong link.
                float utc = (float)post["data"]["created"]; // this is awkward
                p.CreationDate = (long)utc;
                p.Author = (string)post["data"]["author"];
                p.Downvotes = (int)post["data"]["downs"];
                p.Id = (string)post["data"]["id"];
                p.Title = (string)post["data"]["title"];
                p.Upvotes = (int)post["data"]["ups"];

                posts.Add(p);
            }

            string next = (string)data["data"]["after"];

            if (string.IsNullOrEmpty(next) == false)
                posts.AddRange(GetSubredditPosts(subreddit, next));

            return posts;
        }
        

        private void Wait()
        {
            if (_timeOfLastAPIRequest.HasValue)
            {
                TimeSpan timeSinceLastAPICall = new TimeSpan(DateTime.Now.Ticks - _timeOfLastAPIRequest.Value.Ticks);

                if (timeSinceLastAPICall.Milliseconds < API_DELAY)
                {
                    int timeToWait = API_DELAY - timeSinceLastAPICall.Milliseconds;
                    Console.WriteLine("[WAIT] " + timeToWait + " ms");
                    
                    System.Threading.Thread.Sleep(timeToWait);
                }
            }

            _timeOfLastAPIRequest = DateTime.Now;
        }

        private XmlDocument GetCachedVersion(string postURL)
        {
            postURL = System.Web.HttpUtility.UrlEncode(postURL);
            XmlDocument xml = null;

            if (System.IO.File.Exists(CACHE_DIRECTORY + postURL))
            {
                Console.WriteLine("[CACHE READ] " + postURL);
                xml = new XmlDocument();
                xml.Load(CACHE_DIRECTORY + postURL);                
            }

            return xml;
        }

        private void SaveCachedVersion(string postURL, XmlDocument doc)
        {
            Console.WriteLine("[CACHE WRITE] " + postURL);
            postURL = System.Web.HttpUtility.UrlEncode(postURL);
            doc.Save(CACHE_DIRECTORY + postURL);
        }

        private XmlDocument GetXML(string postURL, bool useCache) // this should die in a fire
        {
            XmlDocument doc = null;

            if (useCache)
                doc = GetCachedVersion(postURL);

            if (doc == null)
            {
                Wait();
                string json = _httpHelper.SendGet(postURL);

                json = json.Replace("selftext", "body");
                json = json.Replace("created_utc", "created");

                doc = (XmlDocument)JsonConvert.DeserializeXmlNode("{\"root\":" + json + "}", "root");

                if (useCache)
                    SaveCachedVersion(postURL, doc);
            }

            return doc;
        }
       
    }
}
