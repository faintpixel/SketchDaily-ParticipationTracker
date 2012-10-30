using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

namespace ParticipationTracker
{
    class Program
    {

        private static RedditAPI _reddit;

        static void Main(string[] args)
        {
            _reddit = new RedditAPI();

            List<Post> posts = _reddit.GetAllPostsForSubreddit(@"http://www.reddit.com/r/sketchdaily/");
            ExportPostURLSToFile(posts, @"c:\tmp\skdPosts.txt");

            //List<string> postList = GetOrderedListOfThemes();

            posts.RemoveAt(0); // remove the first one since the day is not over yet

            List<string> themeList = RemoveBlacklistedPosts(posts);

            Dictionary<string, UserParticipation> participation = new Dictionary<string, UserParticipation>();

            foreach (string theme in themeList)
            {
                List<XmlDocument> allCommentsForTheme = _reddit.GetAllCommentsForPost(theme);

                foreach (XmlDocument xml in allCommentsForTheme)
                {

                    XmlNodeList comments = xml.SelectNodes("//children");

                    List<Comment> c = ParseComments(comments);

                    foreach (Comment comment in c)
                    {
                        if (participation.ContainsKey(comment.Author) == false)
                            participation.Add(comment.Author, new UserParticipation(comment.Author));

                        UserParticipation p = participation[comment.Author];
                        p.TotalComments += 1;
                        if (comment.Body.Contains("[") && comment.Body.Contains("]")) // this is a pretty crappy check.
                        {
                            p.TotalLinks += 1;
                            if (p.DaysPostedLinks.Contains(theme) == false)
                                p.DaysPostedLinks.Add(theme);
                        }
                        p.Upvotes += comment.Ups;
                        p.Downvotes += comment.Downs;

                        participation[comment.Author] = p;
                    }
                }
            }

            List<UserParticipation> participationList = participation.Values.ToList();

            SetStreakInfo(ref participationList, themeList);
            DisplayResults(participationList);
            ExportResultsToFile(participationList, @"c:\tmp\skd.txt");
        }

        private static List<string> RemoveBlacklistedPosts(List<Post> posts)
        {
            List<string> filteredList = new List<string>();
            StreamReader reader = File.OpenText(@"c:\src\sketchdailyBlackList.txt");
            List<string> blacklist = new List<string>();

            string line = reader.ReadLine();
            while (line != null)
            {
                blacklist.Add(line);
                line = reader.ReadLine();
            }
            reader.Close();

            foreach (Post post in posts)
                if (blacklist.Contains(post.URL) == false)
                    filteredList.Add(post.URL);

            return filteredList;
        }

        private static void SetStreakInfo(ref List<UserParticipation> participation, List<string> themes)
        {
            Console.WriteLine("Calculating Streaks");
            foreach (UserParticipation user in participation)
            {
                string streak = "";
                foreach (string theme in themes)
                    if (user.DaysPostedLinks.Contains(theme))
                        streak = streak + "1";
                    else
                        streak = streak + "0";

                string longest = streak.Split('0').OrderByDescending(s => s.Length).First();

                user.LongestStreak = longest.Count();
                user.CurrentStreak = streak.Split('0')[0].Length;
            }
        }

        private static List<string> GetOrderedListOfThemes()
        {
            List<string> themes = new List<string>();

            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11y3j3/october_23rd_southern/.json");
            themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11w4qj/october_22nd_eastern/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11uf50/october_21st_western/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11spe5/october_20th_tim_burton/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11qpwj/october_19th_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11owk5/october_18th_autumn_landscape/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11mbe8/october_17th_the_moon/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11kj7w/october_16th_crime_scene/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11ih3v/october_15th_modern_cave_paintings/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11gq8f/october_14th_50s_space_tv_show/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11eyoz/october_13th_babies/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11d4w1/october_12_favorite_comic_characters_and_new_york/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11b3xg/october_11th_the_perfect_world_for_girls_tumblr/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/119383/october_10th_a_heroic_return/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/116zuq/october_9th_interior_crocodile_alligator/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/1154f1/october_8th_bubbles/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/11387c/october_7th_tea_time/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/111moz/october_6th_cats/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10zjmg/october_5th_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10xhrm/october_4th_noses/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10vcxz/october_3rd_beautiful_on_the_inside/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10t7p1/october_2nd_world_war_iii/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10r8iq/october_1st_funday_monday_plus_special_suprise/.json");

            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10pljp/september_30th_purple/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10o0g3/september_29th_aliens/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10m4oq/september_28th_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10k6yv/september_27th_random_character_creation/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10id9h/september_26th_gradients/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10g7aj/september_25th_organicification/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10dzw9/september_24th_i_get_by_with_a_little_help_from/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10cgs2/september_23rd_mascots/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/10aq9d/september_22_tower_time/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/108xdy/september_21st_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/106y4n/september_20th_super_special_theme_draw_the_mods/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/104vnt/september_19th_dogs/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/102yuz/september_18th_idioms/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/100sxy/september_17th_1000_freaks_under_the_sea/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zz1ll/september_16th_yourself_as_a_mythological_creature/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zxdiy/september_15th_birds_of_a_feather/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zvkgk/september_14th_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/ztgm6/september_13th_dr_sigfreids_thermatological/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zri70/setpember_12th_six_word_sketches/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zph3z/september_11th_dubstep/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/znjwz/september_10th_ultimate_showdown/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zll4o/september_9th_rodents/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zk37n/september_8th_daily_annoyances_trees_and_branches/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zi2pq/september_7th_free_draw_friday/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zg4sx/september_6th_living_in_space/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/ze67b/september_5th_a_ridiculous_deity/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/zc4th/september_4th_biggest_fear_with_a_twist/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/za9pk/september_3rd_favorite_drink/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/z8c99/september_2nd_babies/.json");
            //themes.Add(@"http://www.reddit.com/r/SketchDaily/comments/z6rr5/september_1st_dinosaurs/.json");




            return themes;
        }

        private static void DisplayResults(List<UserParticipation> participation)
        {
            foreach (UserParticipation p in participation)
            {
                Console.WriteLine(p.Username);
                Console.WriteLine("  current streak: " + p.CurrentStreak);
                Console.WriteLine("  longest streak: " + p.LongestStreak);
                Console.WriteLine("  karma: " + p.Karma);
                Console.WriteLine("  total comments: " + p.TotalComments);
                Console.WriteLine("  total links: " + p.TotalLinks);
            }
        }

        private static void ExportResultsToFile(List<UserParticipation> participation, string file)
        {
            StreamWriter writer = File.CreateText(file);
            writer.Write("User,");
            writer.Write("Current Streak,");
            writer.Write("Longest Streak,");
            writer.Write("Karma,");
            writer.Write("Upvotes,");
            writer.Write("Downvotes,");
            writer.Write("Total Comments,");
            writer.Write("Total Links");
            writer.WriteLine();            

            foreach (UserParticipation p in participation)
            {
                writer.Write(p.Username + ",");
                writer.Write(p.CurrentStreak + ",");
                writer.Write(p.LongestStreak + ",");
                writer.Write(p.Karma + ",");
                writer.Write(p.Upvotes + ",");
                writer.Write(p.Downvotes + ",");
                writer.Write(p.TotalComments + ",");
                writer.Write(p.TotalLinks);
                writer.WriteLine();
            }

            writer.Close();
        }

        private static void ExportPostURLSToFile(List<Post> posts, string file)
        {
            StreamWriter writer = File.CreateText(file);

            foreach (Post post in posts)
            {
                writer.WriteLine(post.URL);
            }
            writer.Close();
        }

        private static List<Comment> ParseComments(XmlNodeList comments)
        {
            List<Comment> results = new List<Comment>();

            foreach (XmlNode comment in comments)
            {
                Comment c = new Comment();

                if (comment.SelectSingleNode("data") != null && comment.SelectSingleNode("kind").InnerText != "more" && comment.SelectSingleNode("kind").InnerText != "t3")
                {
                    c.Author = comment.SelectSingleNode("data/author").InnerText;
                    c.Body = comment.SelectSingleNode("data/body").InnerText;
                    c.CreatedUTC = comment.SelectSingleNode("data/created").InnerText;
                    c.Downs = int.Parse(comment.SelectSingleNode("data/downs").InnerText);
                    c.Ups = int.Parse(comment.SelectSingleNode("data/ups").InnerText);
                    c.Flair = comment.SelectSingleNode("data/author_flair_css_class").InnerText;

                    results.Add(c);
                }
            }

            return results;
        }


    }
}
