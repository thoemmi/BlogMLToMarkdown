using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
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
  - {5}/
---

{3}
";
        private const string blogMLNamespace = "http://www.blogml.com/2006/09/BlogML";

        private class Post {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime CreatedAt { get; set; }
            public string LegacyUrl { get; set; }
            public string Name { get; set; }
            public string[] Categories { get; set; }
        }

        private static void Main(string[] args) {
            string documentContent;
            using (var sr = new StreamReader(args[0])) {
                documentContent = sr.ReadToEnd();
            }

            var document = XDocument.Load(XmlReader.Create(new StringReader(documentContent), new XmlReaderSettings {
                IgnoreComments = true,
                CheckCharacters = false,
            }));

            var allCategories = document.Root
                .Elements(XName.Get("categories", blogMLNamespace))
                .Elements()
                .ToDictionary(c => c.Attribute(XName.Get("id")).Value, c => c.Elements().First().Value);

            var outputPath = args.Length > 1 ? args[1] : "output";
            if (!Directory.Exists(outputPath)) {
                Directory.CreateDirectory(outputPath);
            }

            var posts = new List<Post>();
            foreach (var post in document.Root.Elements(XName.Get("posts", blogMLNamespace)).Elements()) {
                var url = post.Attribute("post-url").Value;
                var title = post.Descendants(XName.Get("title", blogMLNamespace)).First().Value;
                var p = new Post {
                    Id = post.Attribute("id").Value,
                    CreatedAt = DateTime.Parse(post.Attribute("date-created").Value),
                    Title = title,
                    Content = post.Descendants(XName.Get("content", blogMLNamespace)).First().Value,
                    LegacyUrl = url,
                    Name = SlugConverter.TitleToSlug(title),
                    Categories = post
                        .Descendants(XName.Get("category", blogMLNamespace))
                        .Select(c1 => c1.Attribute("ref").Value)
                        .Select(catId => allCategories[catId])
                        .ToArray()
                };
                posts.Add(p);
            }

            Parallel.ForEach(posts, post => {
                var content = post.Content;
                content = content.Replace("<br>", "<br />");
                content = ReplaceTags(content);
                var markdown = FormatCode(ConvertHtmlToMarkdown(content));

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

        private static readonly Regex _tagRegex1 = new Regex(@"\<(pre|PRE) class=""?(?<language>.*?)""?\>(?<code>.*?)\</(pre|PRE)\>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _tagRegex2 = new Regex(@"\[code language=(?<language>.*?)\](?<code>.*?)\[/code\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static string ReplaceTags(string content) {
            content = _tagRegex1.Replace(content, match => {
                var language = match.Groups["language"].Value;
                var code = match.Groups["code"].Value;
                return "<pre class=" + GetLanguage(language, code) + ">" + code + "</pre>";
            });
            content = _tagRegex2.Replace(content, match => {
                var language = match.Groups["language"].Value;
                var code = match.Groups["code"].Value;
                return "<pre class=" + GetLanguage(language, code) + ">" + code + "</pre>";
            });
            return content;
        }

        private static string ConvertHtmlToMarkdown(string source) {
            string args = String.Format(@"-r html -t markdown_github+pipe_tables-multiline_tables");

            var startInfo = new ProcessStartInfo("pandoc.exe", args) {
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            var process = new Process { StartInfo = startInfo };
            process.Start();

            var inputBuffer = Encoding.UTF8.GetBytes(source);
            process.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
            process.StandardInput.Close();

            process.WaitForExit(2000);
            using (var sr = new StreamReader(process.StandardOutput.BaseStream)) {
                return sr.ReadToEnd();
            }
        }

        private static readonly Regex _codeRegex = new Regex(@"~~~~ \{(?<language>.*?)\}(?<code>.*?)~~~~",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static string FormatCode(string content) {
            return _codeRegex.Replace(content, match => {
                var language = match.Groups["language"].Value;
                var code = match.Groups["code"].Value;
                return "```" + GetLanguage(language, code) + code + "```";
            });
        }

        private static string GetLanguage(string language, string code) {
            language = language.Trim().ToLowerInvariant();
            if (language.Contains("csharp") || language == "cs" || language == "c#") {
                return "csharp";
            } else if (language.Contains("aspx")) {
                return "aspx-cs";
            } else if (language.Contains("xml")) {
                return code.Contains("runat=\"server\"") ? "aspx-cs" : "xml";
            }

            var trimmedCode = code.Trim();
            if (trimmedCode.Contains("<%= ") || trimmedCode.Contains("<%: ")) return "aspx-cs";
            if (trimmedCode.StartsWith("<script") || trimmedCode.StartsWith("<table")) return "html";
            return String.Empty;
        }
    }
}
