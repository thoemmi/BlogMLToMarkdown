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

        private static void Main(string[] args) {
            var outputPath = args.Length > 1 ? args[1] : "output";
            if (!Directory.Exists(outputPath)) {
                Directory.CreateDirectory(outputPath);
            }

            var posts = BlogMLReader.ReadPosts(args[0]).ToList();

            Parallel.ForEach(posts, post => {
                var markdown = ContentConverter.GetMarkdownContent(post);

                var title = post.Title.Replace(":", "&#58;").Replace("'", "''");
                var blog = string.Format(postFormat, title, post.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zz"), string.Join(", ", post.Categories),
                    markdown, post.Id, post.LegacyUrl);

                var path = Path.Combine(outputPath, post.CreatedAt.Year.ToString());
                if (!Directory.Exists(path)) {
                    Directory.CreateDirectory(path);
                }
                using (var sw = File.CreateText(Path.Combine(path, post.CreatedAt.ToString("yyyy-MM-dd") + "-" + post.Name + ".md"))) {
                    sw.Write(blog);
                }
            });
        }
    }
}
