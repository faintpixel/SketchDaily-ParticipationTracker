using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedditAPI.Models
{
    public class Comment
    {
        public string Body;
        public string BodyHTML;
        public int Ups;
        public int Downs;
        public string Author;
        public string CreatedUTC;
        public string Flair;
    }
}
