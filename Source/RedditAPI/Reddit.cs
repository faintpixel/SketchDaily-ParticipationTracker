using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RedditAPI.Models;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.Xml;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

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
            Console.WriteLine("LOGIN RESPONSE: " + response);

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
                Console.WriteLine("RESPONSE:");
                Console.WriteLine(response);
                Console.WriteLine();

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

            string url = "http://www.reddit.com/r/" + subreddit + "/new/.json?sort=new&limit=100";

            posts = GetSubredditPosts("sketchdaily", null);

            posts = posts.OrderBy(p => p.CreationDate).ToList();
            posts.Reverse();

            return posts;
        }

        private List<Post> GetSubredditPosts(string subreddit, string after)
        {
            List<Post> posts = new List<Post>();

            string url = "http://www.reddit.com/r/" + subreddit + "/new/.json?sort=new&limit=100";
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
                string utc = post["data"]["created"].ToString(); // this is awkward
                p.CreationDate = long.Parse(utc);
                p.Author = (string)post["data"]["author"];
                p.Downvotes = (int)post["data"]["downs"];
                p.Id = (string)post["data"]["id"];
                p.Title = (string)post["data"]["title"];
                p.Upvotes = (int)post["data"]["ups"];
                string selfText = (string)post["data"]["selftext"];

                if (selfText.Contains(@"/meta") == false)
                    posts.Add(p);
                else
                    Console.WriteLine("Ignoring post " + p.URL + " because of tagging.");
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
                    int timeToWait = (int)(API_DELAY - timeSinceLastAPICall.TotalMilliseconds);                    
                    if (timeToWait > 0)
                    {
                        Console.WriteLine("[WAIT] " + timeToWait + " ms");
                        System.Threading.Thread.Sleep(timeToWait);
                    }
                }
            }

            _timeOfLastAPIRequest = DateTime.Now;
        }

        private string GetCachedVersion(string postURL)
        {

            postURL = ConvertURLToSafeFileName(postURL);

            string doc = null;

            if (System.IO.File.Exists(CACHE_DIRECTORY + postURL))
            {
                Console.WriteLine("[CACHE READ] " + postURL);
                StreamReader reader = File.OpenText(CACHE_DIRECTORY + postURL);
                string line = reader.ReadLine();
                while (line != null)
                {
                    doc += line;
                    line = reader.ReadLine();
                }
                reader.Close();
            }

            return doc;
        }

        private void SaveCachedVersion(string postURL, string doc)
        {
            Console.WriteLine("[CACHE WRITE] " + postURL);
            string filePath = CACHE_DIRECTORY + ConvertURLToSafeFileName(postURL);
            StreamWriter writer = File.CreateText(filePath);
            writer.WriteLine(doc);
            writer.Close();
        }

        private string ConvertURLToSafeFileName(string url)
        {
            return HttpUtility.UrlEncode(url);
        }

        public List<Comment> GetComments(string postUrl, bool useCache)
        {
            List<Comment> results = new List<Comment>();

            string response = null;
            if (useCache)
                response = GetCachedVersion(postUrl);

            if (useCache == false || string.IsNullOrEmpty(response))
            {
                Wait();
                response = _httpHelper.SendGet(postUrl + ".json");
                if (useCache)
                    SaveCachedVersion(postUrl, response);
            }

            JArray dataArray = JArray.Parse(response);

            if (dataArray.Count != 2)
                throw new Exception("Arto has no idea what he's doing.");

            string after = (string)dataArray[1]["data"]["after"]; // this is pointless

            JArray comments = (JArray)dataArray[1]["data"]["children"];

            foreach (JObject comment in comments)
            {
                results.AddRange(ParseComment(comment, postUrl, useCache));
            }

            return results;
        }

        private DateTime UnixTimeStampToDateTime( double unixTimeStamp )
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970,1,1,0,0,0,0);
            dtDateTime = dtDateTime.AddSeconds( unixTimeStamp ).ToLocalTime();
            return dtDateTime;
        }

        private List<Comment> ParseComment(JObject data, string postUrl, bool useCache)
        {
            List<Comment> comments = new List<Comment>();

            string kind = (string)data["kind"];

            if (kind == "more")
            {
                // get them
                string id = (string)data["data"]["id"];
                comments.AddRange(GetComments(postUrl + id + "/", useCache));
            }
            else
            {
                Comment comment = new Comment();
                comment.Author = (string)data["data"]["author"];
                comment.Body = (string)data["data"]["body"];
                comment.BodyHTML = (string)data["data"]["body_html"];
                comment.CreatedUTC = ((float)data["data"]["created"]).ToString();
                comment.CreatedDate = UnixTimeStampToDateTime(data["data"]["created"].Value<double>());
                comment.Downs = (int)data["data"]["downs"];
                comment.Flair = (string)data["data"]["author_flair_css_class"];
                comment.Ups = (int)data["data"]["ups"];
                comment.Link = postUrl + (string)data["data"]["id"];

                if (data["data"]["replies"].Type == JTokenType.Object)
                {
                    JArray children = (JArray)data["data"]["replies"]["data"]["children"];

                    foreach (JObject child in children)
                        comments.AddRange(ParseComment(child, postUrl, useCache));
                }

                comments.Add(comment);
            }

            return comments;
        }

       
    }
}
