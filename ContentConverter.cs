using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace BlogMLToMarkdown {
    public static class ContentConverter {
        public static string GetMarkdownContent(Post post) {
            var content = FixInternalLinks(post.Content);
            content = content.Replace("<br>", "<br />");
            content = FixSyntaxHighlighting(content);
            return FormatCode(ConvertHtmlToMarkdown(content));
        }

        private static string FixInternalLinks(string content) {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var htmlNodeCollection = doc.DocumentNode.SelectNodes("//a[@href]");
            if (htmlNodeCollection != null) {
                foreach (var link in htmlNodeCollection) {
                    FixPostLink(link);
                }
            }

            using (var textWriter = new StringWriter()) {
                doc.Save(textWriter);
                return textWriter.ToString();
            }
        }

        private static void FixPostLink(HtmlNode link) {
            var att = link.Attributes["href"];
            var url = new Uri(Program.BaseUri, att.Value);

            if (url.Host != "thomasfreudenberg.com") {
                return;
            }

            var linkedPost = Program.Posts.FirstOrDefault(p => String.Equals(p.LegacyUrl, url.AbsolutePath, StringComparison.OrdinalIgnoreCase));
            if (linkedPost == null) {
                var match = Regex.Match(url.AbsolutePath, @"/blog/archive/\d{4}/\d{2}/\d{2}/(\d+)\.aspx");
                if (match.Success) {
                    var legaceId = match.Groups[1].Value;
                    linkedPost = Program.Posts.FirstOrDefault(p => String.Equals(p.Id, legaceId, StringComparison.OrdinalIgnoreCase));
                }
            }
            if (linkedPost != null) {
                Console.WriteLine("Found internal link to {0}, replacing with {1}", url.AbsolutePath, linkedPost.NewUrl);
                link.Attributes["href"].Value = linkedPost.NewUrl;
            } else if (url.AbsolutePath == "/") {
                // nothing to do
            } else if (url.AbsolutePath == "/blog" || url.AbsolutePath == "/blog/") {
                Console.WriteLine("Found internal link to {0}, replacing with /", url.AbsolutePath);
                link.Attributes["href"].Value = "/";
            } else if (url.AbsolutePath == "/utility/Redirect.aspx") {
                var unescapedQuery = url.GetComponents(UriComponents.Query, UriFormat.Unescaped).Substring(2);
                Console.WriteLine("Found redirection to {0}", unescapedQuery);
                link.Attributes["href"].Value = url.Query;
            } else {
                Console.WriteLine("Found internal link {0}", url.AbsolutePath);
            }
        }

        private static readonly Regex _tagRegex1 = new Regex(@"\<(pre|PRE) class=""?(?<language>.*?)""?\>(?<code>.*?)\</(pre|PRE)\>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _tagRegex2 = new Regex(@"\[code language=(?<language>.*?)\](?<code>.*?)\[/code\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static string FixSyntaxHighlighting(string content) {
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
            var args = @"-r html -t markdown_github+pipe_tables-multiline_tables";

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