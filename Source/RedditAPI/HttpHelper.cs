using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using RedditAPI.Models;
using System.Web;
using System.IO;
using System.Collections;

namespace RedditAPI
{
    public class HttpHelper
    {
        public string SendGet(string url, string cookies, int attemptNumber = 1, string authToken = null)
        {
            try
            {
                Log("[GET] " + url);
                WebClient client = new WebClient();
                client.Headers["User-Agent"] = "bot for /r/sketchdaily by /u/artomizer";
                if (authToken != null)
                    client.Headers.Add("Authorization: bearer " + authToken);

                if (string.IsNullOrEmpty(cookies) == false)
                    client.Headers.Add(HttpRequestHeader.Cookie, cookies);

                string result = client.DownloadString(url);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                if (attemptNumber <= 5)
                {
                    Console.WriteLine("Trying again in 2 seconds.");
                    System.Threading.Thread.Sleep(2000);
                    return SendGet(url, cookies, attemptNumber + 1, authToken);
                }
                else
                    throw;
            }
        }

        public string SendPost(string url, string parameters, string authToken = null, int attemptNumber = 1, string username = "", string password = "")
        {
            try
            {
                Log("[POST] " + url + " - parameters: " + parameters);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "bot for /r/sketchdaily by /u/artomizer";
                if(authToken != null)
                    request.Headers.Add("Authorization: bearer " + authToken);
                request.Method = "POST";
                if(string.IsNullOrEmpty(username) == false)
                    request.Credentials = new System.Net.NetworkCredential(username, password);

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

                if (responseString.Contains("<title>Ow!") && responseString.Contains("error code: ")) 
                    throw new Exception("Response was a reddit error page.");

                return responseString;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                if (attemptNumber <= 5)
                {
                    Console.WriteLine("Trying again in 2 seconds.");
                    System.Threading.Thread.Sleep(2000);
                    return SendPost(url, parameters, authToken, attemptNumber + 1);
                }
                else
                    throw;
            }
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
