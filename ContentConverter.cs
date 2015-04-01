using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogMLToMarkdown {
    public static class ContentConverter {
        public static string GetMarkdownContent(Post post) {
            var content = post.Content;
            content = content.Replace("<br>", "<br />");
            content = FixSyntaxHighlighting(content);
            return FormatCode(ConvertHtmlToMarkdown(content));

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