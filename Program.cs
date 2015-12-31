using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BlogMLToMarkdown {
    internal class Program {
        private const string postFormat = @"---
layout: post
title: '{0}'
date: {1}
comments: true
disqus_identifier: {4}
categories: [{2}]
redirect_from:
  - {5}
---

{3}
";

        public static Uri BaseUri = new Uri("http://thomasfreudenberg.com");
        public static List<Post> Posts;
        public static string BasePath;
        public static string PostPath;
        public static string BinariesPath;

        private static void Main(string[] args) {
            BasePath = args.Length > 1 ? args[1] : "output";
            PostPath = Path.Combine(BasePath, "_posts", "archive");
            if (!Directory.Exists(PostPath)) {
                Directory.CreateDirectory(PostPath);
            }
            BinariesPath = Path.Combine(BasePath, "files", "archive");
            if (!Directory.Exists(BinariesPath)) {
                Directory.CreateDirectory(BinariesPath);
            }

            Posts = BlogMLReader.ReadPosts(args[0]).ToList();
            Parallel.ForEach(Posts, post => {
                post.NewUrl = String.Format("/archive/{0:yyyy}/{0:MM}/{0:dd}/{1}/", post.CreatedAt, post.Name);
            });

            //ProcessPost(Posts.First(p => p.Id == "13387"));

            Task.WaitAll(Posts.Select(ProcessPost).ToArray());
            ExportForDisqus(Posts);

            Console.WriteLine("Press any key");
            Console.ReadKey();
        }

        private static void ExportForDisqus(List<Post> posts) {
            XNamespace nsContent = "http://purl.org/rss/1.0/modules/content/";
            XNamespace nsDsq = "http://www.disqus.com/";
            XNamespace nsDc = "http://purl.org/dc/elements/1.1/";
            XNamespace nsWp = "http://wordpress.org/export/1.0/";

            var root = new XElement("rss",
                new XAttribute(XNamespace.Xmlns + "content", nsContent.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dsq", nsDsq.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", nsDc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "wp", nsWp.NamespaceName),
                new XElement("channel",
                    posts.Select(p => new XElement("item",
                        new XElement("title", p.Title),
                        new XElement("link", "http://thomasfreudenberg.com" + p.NewUrl),
                        new XElement(nsDsq + "thread_identifier", p.Id),
                        new XElement(nsWp + "post_date_gmt", p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                        new XElement(nsWp + "comment_status", "open"),
                        p.Comments.Select(c => new XElement(nsWp + "comment",
                            new XElement(nsWp + "comment_id", c.Id),
                            new XElement(nsWp + "comment_author", c.Author),
                            new XElement(nsWp + "comment_author_email", c.AuthorEmail),
                            new XElement(nsWp + "comment_author_url", c.AuthorUrl),
                            new XElement(nsWp + "comment_author_IP"),
                            new XElement(nsWp + "comment_date_gmt", c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                            new XElement(nsWp + "comment_content", c.Content),
                            new XElement(nsWp + "comment_approved", 1)
                            ))))
                    ));

            var filename = Path.Combine(BasePath, "disqus.wxr");
            using (var sw = new StreamWriter(filename)) {
                root.Save(sw, SaveOptions.None);
            }
        }

        private static async Task ProcessPost(Post post) {
            var markdown = await ContentConverter.GetMarkdownContent(post);

            var title = post.Title.Replace(":", "&#58;").Replace("'", "''");
            var blog = string.Format(postFormat, title, post.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zz"), string.Join(", ", post.Categories),
                markdown, post.Id, post.LegacyUrl);

            var path = Path.Combine(PostPath, post.CreatedAt.Year.ToString());
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            using (var sw = File.CreateText(Path.Combine(path, post.CreatedAt.ToString("yyyy-MM-dd") + "-" + post.Name + ".md"))) {
                sw.Write(blog);
            }
        }
    }
}
