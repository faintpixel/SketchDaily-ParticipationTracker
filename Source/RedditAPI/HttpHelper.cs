using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using RedditAPI.Models;
using System.Web;
using System.IO;

namespace RedditAPI
{
    public class HttpHelper
    {
        public string SendGet(string url)
        {
            Log("[GET] " + url);
            WebClient client = new WebClient();
            client.Headers["User-Agent"] = "bot for /r/sketchdaily by /u/artomizer";

            string result = client.DownloadString(url);

            return result;
        }

        public string SendPost(string url, string parameters, Session session = null)
        {
            Log("[POST] " + url + " - parameters: " + parameters);
            WebRequest request = WebRequest.Create(url);
            ((HttpWebRequest)request).UserAgent = "bot for /r/sketchdaily by /u/artomizer";

            if (session != null)
            {
                Cookie cookie = new Cookie("reddit_session", HttpUtility.UrlEncode(session.CookieData));
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

            StreamReader str = new StreamReader(result.GetResponseStream());
            string responseString = str.ReadToEnd();

            return responseString;
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
