using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace BlogMLToMarkdown {
    public static class ContentConverter {
        public static async Task<string> GetMarkdownContent(Post post) {
            var content = await FixInternalLinks(post.Content);
            content = content.Replace("<br>", "<br />");
            content = FixSyntaxHighlighting(content);
            return FormatCode(ConvertHtmlToMarkdown(content));
        }

        private static async Task<string> FixInternalLinks(string content) {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var htmlNodeCollection = doc.DocumentNode.SelectNodes("//a[@href]");
            if (htmlNodeCollection != null) {
                foreach (var link in htmlNodeCollection) {
                    await FixPostLink(link);
                }
            }
            htmlNodeCollection = doc.DocumentNode.SelectNodes("//img[@src]");
            if (htmlNodeCollection != null) {
                foreach (var link in htmlNodeCollection) {
                    await FixImageLink(link);
                }
            }

            using (var textWriter = new StringWriter()) {
                doc.Save(textWriter);
                return textWriter.ToString();
            }
        }

        private static async Task FixPostLink(HtmlNode link) {
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
            if (linkedPost == null) {
                var match = Regex.Match(url.AbsolutePath, @"/blog/posts/(\d+)\.aspx");
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
                var filename = await DownloadFile(link, url);
                link.Attributes["href"].Value = filename;
            }
        }

        private static async Task FixImageLink(HtmlNode link) {
            var att = link.Attributes["src"];
            var url = new Uri(Program.BaseUri, att.Value);

            if (url.Host != "thomasfreudenberg.com") {
                return;
            }

            var fileName = await DownloadFile(link, url);

            link.Attributes["src"].Value = fileName;
        }

        private static async Task<string> DownloadFile(HtmlNode link, Uri url) {
            WebClient wc = new WebClient();
            var bytes = await wc.DownloadDataTaskAsync(url);
            string filename = GetFilenameFromHeaderCollection(wc.ResponseHeaders);
            if (filename == null) {
                if (String.Equals(Path.GetExtension(url.ToString()), ".aspx", StringComparison.OrdinalIgnoreCase)) {
                    var relAttr = link.Attributes["alt"];
                    if (relAttr != null && !String.IsNullOrEmpty(relAttr.Value)) {
                        // create a filename from image's alt= attribute.
                        filename = SlugConverter.TitleToSlug(relAttr.Value);
                    } else {
                        // use url for filename
                        filename = Path.GetFileNameWithoutExtension(url.ToString());
                    }
                    // add extension based on response's Content-Type
                    filename += GetFileExtensionByContentType(wc.ResponseHeaders["Content-Type"]);
                } else {
                    filename = Path.GetFileName(url.ToString());
                }
            }
            while (filename.StartsWith(".")) {
                filename = filename.Substring(1);
            }

            Console.WriteLine("Found picture {0}, new filename {1}", url.AbsolutePath, filename);

            var path = Path.Combine(Program.BinariesPath, filename);

            if (!File.Exists(path)) {
                File.WriteAllBytes(path, bytes);

                DateTime lastModified;
                if (!string.IsNullOrEmpty(wc.ResponseHeaders[HttpResponseHeader.LastModified]) &&
                    DateTime.TryParse(wc.ResponseHeaders[HttpResponseHeader.LastModified], out lastModified)) {
                    File.SetLastWriteTime(path, lastModified);
                }
            }
            return "/files/archive/" + filename;
        }

        private static string GetFilenameFromHeaderCollection(WebHeaderCollection headers) {
            string fileName = null;
            var contentDisposition = headers["Content-disposition"];
            if (contentDisposition != null) {
                int pos;
                if ((pos = contentDisposition.IndexOf("filename=", StringComparison.OrdinalIgnoreCase)) > 0) {
                    fileName = contentDisposition.Substring(pos + "filename=".Length);
                    fileName = fileName.Replace("\"", "");
                }
            }
            if (string.IsNullOrEmpty(fileName)) {
                fileName = headers["Location"] != null ? Path.GetFileName(headers["Location"]) : null;
            }
            return fileName;
        }

        private static string GetFileExtensionByContentType(string contentType) {
            switch (contentType) {
                case "image/jpeg":
                    return ".jpeg";
                default:
                    return ".bin";
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