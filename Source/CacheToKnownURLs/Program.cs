using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;

namespace CacheToKnownURLs
{
    // Reddit only returns the most recent 1000 subreddit posts.
    // As a workaround for now, we can keep track of all the posts ourselves in a text file.
    // We have old posts cached, so this app will go through the cache and build the initial text file for us to use.
    // Should be a one time use kind of thing... but who knows.
    class Program
    {
        static void Main(string[] args)
        {
            string cache = @"C:\src\Backup\Cache"; 

            List<string> urls = GetCacheContents(cache);
            urls = CleanData(urls, cache);
            WriteKnownURLs(urls);

            Console.Write("Done.");
            Console.ReadLine();
        }

        private static List<string> CleanData(List<string> data, string cachePath)
        {
            List<string> urls = new List<string>();
            int removeEndIndex = cachePath.Length + 1;

            // TO DO: remove duplicates, invalid entries, and fix encoding
            foreach(string url in data)
            {
                string cleanedURL = url.Remove(0, removeEndIndex);
                cleanedURL = HttpUtility.UrlDecode(cleanedURL);

                int urlComponents = cleanedURL.Split('/').Count();
                if(urlComponents == 9)
                {
                    Console.WriteLine("[ADDED] " + cleanedURL);
                    urls.Add(cleanedURL);
                }
                else
                    Console.WriteLine("[SKIP] " + cleanedURL);                
            }

            return urls;
        }

        private static void WriteKnownURLs(List<string> urls)
        {
            File.WriteAllLines("knownURLs.txt", urls);
        }

        private static List<string> GetCacheContents(string path)
        {
            List<string> files;

            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path).OrderByDescending(d => new FileInfo(d).CreationTime).Reverse().ToList();
            }
            else
                throw new Exception("Invalid path: " + path);

            return files;
        }
    }
}
