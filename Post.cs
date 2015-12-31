using System;

namespace BlogMLToMarkdown {
    public class Post {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string LegacyUrl { get; set; }
        public string Name { get; set; }
        public string[] Categories { get; set; }
        public string NewUrl { get; set; }
        public Comment[] Comments { get; set; }
    }

    public class Comment {
        public string Id { get; set; }
        public string Author { get; set; }
        public string AuthorUrl { get; set; }
        public string AuthorEmail { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}