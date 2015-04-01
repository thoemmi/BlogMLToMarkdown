using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace BlogMLToMarkdown {
    public static class BlogMLReader {
        private const string blogMLNamespace = "http://www.blogml.com/2006/09/BlogML";

        public static IEnumerable<Post> ReadPosts(string path) {
            string documentContent;
            using (var sr = new StreamReader(path)) {
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
                yield return p;
            }
        }
    }
}