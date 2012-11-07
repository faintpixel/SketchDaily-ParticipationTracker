using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedditAPI.Models
{
    public class Post
    {
        public string URL;
        public long CreationDate;
        public string Author;
        public int Upvotes;
        public int Downvotes;
        public string Title;
        public string Id;
    }
}
