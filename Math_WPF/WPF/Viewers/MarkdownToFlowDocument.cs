using System;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax.Inlines;
using Markdig.Syntax;
using Markdig.Extensions.Tables;
using Markdig;

namespace Game.Math_WPF.WPF.Viewers
{
    /// <summary>
    /// Converts markdown to a WPF FlowDocument by walking Markdig's parsed AST.
    /// Supports: headers, bold, italic, strikethrough, inline/fenced code,
    /// bullet/numbered lists, blockquotes, horizontal rules, links, tables, and paragraphs.
    /// </summary>
    public static class MarkdownToFlowDocument
    {
        private static readonly string _monoFont = "Cascadia Mono, Consolas, Courier New";
        private static readonly string _bodyFont = "Segoe UI";
        private static readonly double[] _headerSizes = { 28, 24, 20, 18, 16, 14 };

        // Cached pipeline with advanced extensions (tables, task lists, strikethrough, etc.)
        private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

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

            var parsed = Markdown.Parse(markdown, _pipeline);

            foreach (var block in parsed)
            {
                var flowBlock = BlockToFlow(block);
                if (flowBlock != null)
                    doc.Blocks.Add(flowBlock);
            }

            return doc;
        }

        // Block dispatch

        private static System.Windows.Documents.Block BlockToFlow(Markdig.Syntax.Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    return MakeHeader(heading);

                case ParagraphBlock paragraph:
                    return MakeParagraph(paragraph);

                case FencedCodeBlock:
                case CodeBlock:
                    return MakeCodeBlock((CodeBlock)block);

                case ListBlock list:
                    return MakeList(list);

                case QuoteBlock quote:
                    return MakeBlockquote(quote);

                case ThematicBreakBlock:
                    return MakeHorizontalRule();

                case Markdig.Extensions.Tables.Table table:
                    return MakeTable(table);

                default:
                    if (block is ContainerBlock container)
                    {
                        var para = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                        foreach (var child in container)
                        {
                            var childBlock = BlockToFlow(child);
                            if (childBlock is Paragraph childPara)
                                while (childPara.Inlines.Count > 0)
                                    para.Inlines.Add(childPara.Inlines.FirstInline);
                        }
                        return para.Inlines.Count > 0 ? para : null;
                    }
                    return null;
            }
        }

        // Block factories

        private static System.Windows.Documents.Block MakeHeader(HeadingBlock heading)
        {
            int level = Math.Min(heading.Level, 6);
            var para = new Paragraph
            {
                Margin = new Thickness(0, 12, 0, 6)
            };

            if (heading.Inline != null)
            {
                AddInlines(para, heading.Inline);
                foreach (var inline in para.Inlines.ToList())
                {
                    if (inline is Run run)
                    {
                        run.FontWeight = FontWeights.Bold;
                        run.FontSize = _headerSizes[level - 1];
                    }
                }
            }
            else
            {
                para.Inlines.Add(new Run(" ")
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = _headerSizes[level - 1]
                });
            }
            return para;
        }

        private static System.Windows.Documents.Block MakeParagraph(ParagraphBlock paragraph)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 4)
            };
            if (paragraph.Inline != null)
                AddInlines(para, paragraph.Inline);
            return para;
        }

        private static System.Windows.Documents.Block MakeCodeBlock(CodeBlock code)
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
            var sb = new System.Text.StringBuilder();
            foreach (var line in code.Lines)
                sb.AppendLine(line.ToString());
            para.Inlines.Add(new Run(sb.ToString().TrimEnd('\n', '\r')));
            return para;
        }

        private static System.Windows.Documents.Block MakeBlockquote(QuoteBlock quote)
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
            bool first = true;
            foreach (var child in quote)
            {
                if (!first)
                    para.Inlines.Add(new LineBreak());
                AddBlockInlines(para, child);
                first = false;
            }
            return para;
        }

        private static System.Windows.Documents.Block MakeList(ListBlock list)
        {
            var flowList = new List
            {
                MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                Margin = new Thickness(20, 4, 0, 4)
            };
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var para = new Paragraph();
                    foreach (var child in listItem)
                        AddBlockInlines(para, child);
                    flowList.ListItems.Add(new ListItem(para));
                }
            }
            return flowList;
        }

        private static System.Windows.Documents.Block MakeHorizontalRule()
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

        private static System.Windows.Documents.Block MakeTable(Markdig.Extensions.Tables.Table table)
        {
            var flowTable = new System.Windows.Documents.Table
            {
                Margin = new Thickness(0, 4, 0, 4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(1),
                CellSpacing = 0
            };

            int colCount = table.Count > 0 && table[0] is Markdig.Extensions.Tables.TableRow firstRow ? firstRow.Count : 1;
            for (int c = 0; c < colCount; c++)
                flowTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rowGroup = new TableRowGroup();
            bool isHeader = true;

            foreach (var row in table)
            {
                if (row is Markdig.Extensions.Tables.TableRow tableRow)
                {
                    var flowRow = new System.Windows.Documents.TableRow();

                    foreach (var cell in tableRow)
                    {
                        if (cell is Markdig.Extensions.Tables.TableCell tableCell)
                        {
                            var para = new Paragraph { Margin = new Thickness(2) };
                            foreach (var cellBlock in tableCell)
                                AddBlockInlines(para, cellBlock);

                            var tableCellElem = new System.Windows.Documents.TableCell(para)
                            {
                                BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(4)
                            };
                            if (isHeader)
                            {
                                tableCellElem.FontWeight = FontWeights.Bold;
                                tableCellElem.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                            }
                            flowRow.Cells.Add(tableCellElem);
                        }
                    }
                    rowGroup.Rows.Add(flowRow);
                    isHeader = false;
                }
            }
            flowTable.RowGroups.Add(rowGroup);
            return flowTable;
        }

        // Helper: extract inlines from a block into a paragraph

        private static void AddBlockInlines(Paragraph para, Markdig.Syntax.Block block)
        {
            if (block is ParagraphBlock paraBlock && paraBlock.Inline != null)
                AddInlines(para, paraBlock.Inline);
            else if (block is ContainerBlock container)
            {
                foreach (var child in container)
                    AddBlockInlines(para, child);
            }
        }

        // Inline parsing (walk Markdig AST)

        private static void AddInlines(Paragraph para, Markdig.Syntax.Inlines.Inline inline)
        {
            while (inline != null)
            {
                AddInline(para.Inlines, inline);
                inline = inline.NextSibling;
            }
        }

        private static void AddInlinesToSpan(Span span, Markdig.Syntax.Inlines.Inline inline)
        {
            while (inline != null)
            {
                AddInline(span.Inlines, inline);
                inline = inline.NextSibling;
            }
        }

        private static void AddInline(InlineCollection parent, Markdig.Syntax.Inlines.Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    parent.Add(new Run(literal.Content.ToString()));
                    break;

                case EmphasisInline emphasis:
                    if (emphasis.DelimiterChar == '~')
                    {
                        var strikeSpan = new Span
                        {
                            TextDecorations = TextDecorations.Strikethrough
                        };
                        AddInlinesToSpan(strikeSpan, emphasis.FirstChild);
                        parent.Add(strikeSpan);
                    }
                    else if (emphasis.DelimiterCount == 2)
                    {
                        var inner = new Span();
                        AddInlinesToSpan(inner, emphasis.FirstChild);
                        parent.Add(new Bold(inner));
                    }
                    else
                    {
                        var inner = new Span();
                        AddInlinesToSpan(inner, emphasis.FirstChild);
                        parent.Add(new Italic(inner));
                    }
                    break;

                case CodeInline code:
                    parent.Add(new Run(code.Content)
                    {
                        FontFamily = new FontFamily(_monoFont),
                        Background = new SolidColorBrush(Color.FromRgb(238, 238, 238))
                    });
                    break;

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                        parent.Add(new LineBreak());
                    else
                        parent.Add(new Run(" "));
                    break;

                case LinkInline link:
                    var linkSpan = new Span();
                    AddInlinesToSpan(linkSpan, link.FirstChild);
                    if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                    {
                        var hyperlink = new Hyperlink(linkSpan) { NavigateUri = uri };
                        hyperlink.RequestNavigate += OnRequestNavigate;
                        parent.Add(hyperlink);
                    }
                    else
                    {
                        parent.Add(linkSpan);
                    }
                    break;

                case AutolinkInline autolink:
                    if (Uri.TryCreate(autolink.Url, UriKind.Absolute, out var autoUri))
                    {
                        var hyperlink = new Hyperlink(new Run(autolink.Url)) { NavigateUri = autoUri };
                        hyperlink.RequestNavigate += OnRequestNavigate;
                        parent.Add(hyperlink);
                    }
                    else
                    {
                        parent.Add(new Run(autolink.Url));
                    }
                    break;

                case HtmlInline html:
                    parent.Add(new Run(html.Tag));
                    break;

                case HtmlEntityInline entity:
                    parent.Add(new Run(entity.Transcoded.ToString()));
                    break;

                case ContainerInline container:
                    var child = container.FirstChild;
                    while (child != null)
                    {
                        AddInline(parent, child);
                        child = child.NextSibling;
                    }
                    break;

                default:
                    if (inline is ContainerInline transitive)
                    {
                        var span = new Span();
                        AddInlinesToSpan(span, transitive.FirstChild);
                        parent.Add(span);
                    }
                    else
                    {
                        parent.Add(new Run(inline.ToString()));
                    }
                    break;
            }
        }

        private static void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                    { UseShellExecute = true });
            }
            catch { }
        }
    }
}
