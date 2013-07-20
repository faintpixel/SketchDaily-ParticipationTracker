using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ParticipationTracker
{
    public class User
    {
        public string Username;
        public int TotalComments;
        public int TotalLinks;
        public int CurrentStreak;
        public int LongestStreak;
        public int Upvotes;
        public int Downvotes;
        public string Webpage;
        public List<string> DaysPostedLinks = new List<string>();
        public int Karma
        {
            get
            {
                return Upvotes - Downvotes;
            }
        }

        public User(string username)
        {
            Username = username;
        }

       
    }
}
