using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
  - {5}/
---

{3}
";

        public static Uri BaseUri = new Uri("http://thomasfreudenberg.com");
        public static List<Post> Posts;
        public static string PostPath;
        public static string BinariesPath;

        private static void Main(string[] args) {
            var basePath = args.Length > 1 ? args[1] : "output";
            PostPath = Path.Combine(basePath, "_posts", "archive");
            if (!Directory.Exists(PostPath)) {
                Directory.CreateDirectory(PostPath);
            }
            BinariesPath = Path.Combine(basePath, "files", "archive");
            if (!Directory.Exists(BinariesPath)) {
                Directory.CreateDirectory(BinariesPath);
            }

            Posts = BlogMLReader.ReadPosts(args[0]).ToList();
            Parallel.ForEach(Posts, post => {
                post.NewUrl = String.Format("/archive/{0:yyyy}/{0:MM}/{0:dd}/{1}/", post.CreatedAt, post.Name);
            });

            //ProcessPost(Posts.First(p => p.Id == "13387"));

            Task.WaitAll(Posts.Select(ProcessPost).ToArray());

            Console.WriteLine("Press any key");
            Console.ReadKey();
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
