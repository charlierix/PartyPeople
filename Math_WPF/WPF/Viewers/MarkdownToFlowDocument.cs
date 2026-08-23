using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Game.Math_WPF.WPF.Viewers
{
    /// <summary>
    /// Lightweight markdown-to-FlowDocument converter with zero external dependencies.
    /// Supports: headers, bold, italic, strikethrough, inline code, fenced code blocks,
    /// bullet lists, numbered lists, blockquotes, horizontal rules, and links.
    /// </summary>
    public static class MarkdownToFlowDocument
    {
        private static readonly Regex _headerRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex _hrRegex = new(@"^(-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.Compiled);
        private static readonly Regex _bulletRegex = new(@"^\s*[-*+]\s+", RegexOptions.Compiled);
        private static readonly Regex _numberedRegex = new(@"^\s*\d+\.\s+", RegexOptions.Compiled);

        // Order matters: code first (so ** inside `code` isn't parsed), then bold, italic, strike, link
        private static readonly Regex _inlineRegex = new(
            @"(`[^`]+`)" +               // `code`
            @"|(\*\*[^*]+\*\*)" +      // **bold**
            @"|(__[^_]+__)" +             // __bold__
            @"|(\*[^*]+\*)" +           // *italic*
            @"|(~~[^~]+~~)" +            // ~~strike~~
            @"|(\[[^\]]+\]\([^)]+\))",  // [text](url)
            RegexOptions.Compiled);

        private static readonly string _monoFont = "Cascadia Mono, Consolas, Courier New";
        private static readonly string _bodyFont = "Segoe UI";

        private static readonly double[] _headerSizes = { 28, 24, 20, 18, 16, 14 };

        public static FlowDocument Convert(string markdown)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(8),
                FontFamily = new FontFamily(_bodyFont),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32))
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            markdown = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = markdown.Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                string trimmed = line.TrimStart();

                // Fenced code block
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    string fence = trimmed.Substring(0, 3);
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length && !lines[i].TrimStart().StartsWith(fence))
                    {
                        codeLines.Add(lines[i]);
                        i++;
                    }
                    i++; // skip closing fence
                    doc.Blocks.Add(MakeCodeBlock(codeLines));
                    continue;
                }

                // Header
                var headerMatch = _headerRegex.Match(line);
                if (headerMatch.Success)
                {
                    int level = headerMatch.Groups[1].Length;
                    string text = headerMatch.Groups[2].Value.Trim().TrimEnd('#').Trim();
                    doc.Blocks.Add(MakeHeader(text, level));
                    i++;
                    continue;
                }

                // Horizontal rule
                if (_hrRegex.IsMatch(line))
                {
                    doc.Blocks.Add(MakeHorizontalRule());
                    i++;
                    continue;
                }

                // Blockquote
                if (trimmed.StartsWith(">"))
                {
                    var quoteLines = new List<string>();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                    {
                        string q = lines[i].TrimStart().Substring(1);
                        quoteLines.Add(q.TrimStart());
                        i++;
                    }
                    doc.Blocks.Add(MakeBlockquote(quoteLines));
                    continue;
                }

                // Bullet list
                if (_bulletRegex.IsMatch(line))
                {
                    var items = new List<string>();
                    while (i < lines.Length && _bulletRegex.IsMatch(lines[i]))
                    {
                        items.Add(_bulletRegex.Replace(lines[i], "", 1));
                        i++;
                    }
                    doc.Blocks.Add(MakeList(items, TextMarkerStyle.Disc));
                    continue;
                }

                // Numbered list
                if (_numberedRegex.IsMatch(line))
                {
                    var items = new List<string>();
                    while (i < lines.Length && _numberedRegex.IsMatch(lines[i]))
                    {
                        items.Add(_numberedRegex.Replace(lines[i], "", 1));
                        i++;
                    }
                    doc.Blocks.Add(MakeList(items, TextMarkerStyle.Decimal));
                    continue;
                }

                // Paragraph — collect consecutive non-empty, non-special lines
                var paraLines = new List<string>();
                while (i < lines.Length &&
                       !string.IsNullOrWhiteSpace(lines[i]) &&
                       !IsBlockStart(lines[i]))
                {
                    paraLines.Add(lines[i].Trim());
                    i++;
                }
                if (paraLines.Count > 0)
                    doc.Blocks.Add(MakeParagraph(string.Join(" ", paraLines)));
            }

            return doc;
        }

        private static bool IsBlockStart(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("```") ||
                   trimmed.StartsWith("~~~") ||
                   trimmed.StartsWith(">") ||
                   _headerRegex.IsMatch(line) ||
                   _hrRegex.IsMatch(line) ||
                   _bulletRegex.IsMatch(line) ||
                   _numberedRegex.IsMatch(line);
        }

        // ── Block factories ──

        private static Paragraph MakeHeader(string text, int level)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 12, 0, 6)
            };
            var run = new Run(text)
            {
                FontWeight = FontWeights.Bold,
                FontSize = _headerSizes[Math.Min(level, 6) - 1]
            };
            para.Inlines.Add(run);
            return para;
        }

        private static Paragraph MakeParagraph(string text)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 4)
            };
            AddInlines(para, text);
            return para;
        }

        private static Paragraph MakeCodeBlock(List<string> lines)
        {
            var para = new Paragraph
            {
                FontFamily = new FontFamily(_monoFont),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 4, 0, 4)
            };
            para.Inlines.Add(new Run(string.Join("\n", lines)));
            return para;
        }

        private static Paragraph MakeBlockquote(List<string> lines)
        {
            var para = new Paragraph
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(12, 4, 0, 4),
                Margin = new Thickness(0, 4, 0, 4),
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            for (int j = 0; j < lines.Count; j++)
            {
                AddInlines(para, lines[j]);
                if (j < lines.Count - 1)
                    para.Inlines.Add(new LineBreak());
            }
            return para;
        }

        private static List MakeList(List<string> items, TextMarkerStyle marker)
        {
            var list = new List
            {
                MarkerStyle = marker,
                Margin = new Thickness(20, 4, 0, 4)
            };
            foreach (var item in items)
            {
                var para = new Paragraph();
                AddInlines(para, item);
                list.ListItems.Add(new ListItem(para));
            }
            return list;
        }

        private static Paragraph MakeHorizontalRule()
        {
            var para = new Paragraph
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 8, 0, 8)
            };
            para.Inlines.Add(new Run(" "));
            return para;
        }

        // ── Inline parsing ──

        private static void AddInlines(Paragraph para, string text)
        {
            int pos = 0;
            foreach (Match m in _inlineRegex.Matches(text))
            {
                if (m.Index > pos)
                    para.Inlines.Add(new Run(text.Substring(pos, m.Index - pos)));

                string match = m.Value;

                if (match.StartsWith("`"))
                {
                    para.Inlines.Add(new Run(match.Substring(1, match.Length - 2))
                    {
                        FontFamily = new FontFamily(_monoFont),
                        Background = new SolidColorBrush(Color.FromRgb(238, 238, 238))
                    });
                }
                else if (match.StartsWith("**"))
                {
                    para.Inlines.Add(new Bold(new Run(match.Substring(2, match.Length - 4))));
                }
                else if (match.StartsWith("__"))
                {
                    para.Inlines.Add(new Bold(new Run(match.Substring(2, match.Length - 4))));
                }
                else if (match.StartsWith("*"))
                {
                    para.Inlines.Add(new Italic(new Run(match.Substring(1, match.Length - 2))));
                }
                else if (match.StartsWith("~~"))
                {
                    para.Inlines.Add(new Run(match.Substring(2, match.Length - 4))
                    {
                        TextDecorations = TextDecorations.Strikethrough
                    });
                }
                else if (match.StartsWith("["))
                {
                    var linkMatch = Regex.Match(match, @"\[([^\]]+)\]\(([^)]+)\)");
                    if (linkMatch.Success && Uri.TryCreate(linkMatch.Groups[2].Value, UriKind.Absolute, out var uri))
                    {
                        var link = new Hyperlink(new Run(linkMatch.Groups[1].Value))
                        {
                            NavigateUri = uri
                        };
                        link.RequestNavigate += (s, e) =>
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(
                                    new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                                    { UseShellExecute = true });
                            }
                            catch { }
                        };
                        para.Inlines.Add(link);
                    }
                    else
                    {
                        para.Inlines.Add(new Run(match));
                    }
                }

                pos = m.Index + match.Length;
            }

            if (pos < text.Length)
                para.Inlines.Add(new Run(text.Substring(pos)));
        }
    }
}