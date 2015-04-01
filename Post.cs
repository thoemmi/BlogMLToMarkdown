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
    }
}