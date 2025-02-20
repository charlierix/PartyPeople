using Game.Math_WPF.Mathematics;
using Markdig.Syntax.Inlines;
using Markdig.Syntax;
using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// markdig license
// https://github.com/xoofx/markdig
// BSD-2-Clause license
/*

Copyright (c) 2018-2019, Alexandre Mutel
All rights reserved.

Redistribution and use in source and binary forms, with or without modification
, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this 
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, 
   this list of conditions and the following disclaimer in the documentation 
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER 
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

namespace Game.ML
{
    public static class MarkdownParser
    {
        /// <summary>
        /// Extracts bullet list items out of markdown, ignores any other text
        /// </summary>
        public static string[] ExtractBulletList(string text, int max_words_perline = 5)
        {
            var retVal = new List<(double score, string[] items)>();

            var parsed = Markdown.Parse(text);

            // PARAGRAPH ONLY
            //  list is plain text list without a prefix - * + ...

            // BULLET LIST ONLY
            //  bullet list is by itselft

            // PARAGRAPHS AROUND BULLET LIST
            //  there are paragraphs, also a bullet list

            // parse each section, rank them

            // lines in paragraphs that are single line and/or lots of words should be ignored

            foreach (var section in parsed)
            {
                if (section is ListBlock list)
                    retVal.Add(ExtractBulletList_Block(section, 12, max_words_perline));

                else
                    retVal.Add(ExtractBulletList_Block(section, 8, max_words_perline));
            }

            // Find the best one (if there is one)
            int best_index = GetBestIndex(retVal);

            if (best_index < 0)
                return null;

            return retVal[best_index].items;
        }

        public static string ExtractOnlyText(string text)
        {
            StringBuilder retVal = new StringBuilder();

            MarkdownDocument doc = Markdown.Parse(text);

            ExtractOnlyText_block(doc, retVal);

            return retVal.ToString();
        }

        #region Private Methods

        private static (double score, string[] lines) ExtractBulletList_Block(Block block, double max_score, int max_words)
        {
            const int COUNT_MAX_SCORE = 7;
            const double POW = 0.33333333;

            var sb = new StringBuilder();

            ExtractOnlyText_block(block, sb);

            string[] split = sb.ToString().
                Replace("\r\n", "\n").
                Split('\n');

            var lines = new List<string>();

            foreach (string line in split)
            {
                string text = line.Trim();
                if (text == "")
                    continue;

                // throw out lines with more than N words
                MatchCollection matches = Regex.Matches(text, @"\w+");
                if (matches.Count > max_words)
                    continue;

                if (matches.Count == 0)     // just special characters
                    continue;

                if (matches.Count == 1 && text.Length > 30)     // had a case where everything was on one line without spaces in between.  If that's the case, split based on capitalization (worst case, it doesn't split and there's just one element that is the original line)
                    lines.AddRange(Regex.Split(text, @"(?<=[a-z])(?=[A-Z])"));

                else
                    lines.Add(text);
            }

            if (lines.Count == 0)
                return (0, []);

            lines = lines.
                Distinct().
                ToList();

            // Generate a score based on the number of lines, more is better (up to COUNT_MAX_SCORE), normalized 0 to 1
            double score = 1;

            if (lines.Count < COUNT_MAX_SCORE)
                score = Math.Pow((double)lines.Count / (double)COUNT_MAX_SCORE, POW);

            return (score * max_score, lines.ToArray());
        }

        private static int GetBestIndex<T>(IList<(double score, T item)> items)
        {
            int best_index = -1;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].score.IsNearZero())
                    continue;

                if (best_index < 0)
                    best_index = i;

                else if (items[i].score > items[best_index].score)
                    best_index = i;
            }

            return best_index;
        }

        #endregion
        #region Private Methods - ExtractOnlyText

        private static void ExtractOnlyText_block(Block block, StringBuilder sb)
        {
            if (block is ContainerBlock container)
            {
                foreach (var childBlock in container)
                    ExtractOnlyText_block(childBlock, sb);
            }
            else if (block is LeafBlock leaf && leaf.Inline != null)
            {
                // Add a newline for each new list item or paragraph -- not necessary, the appendline was moved to inline extract function
                //if (sb.Length > 0 && (block is ListBlock || block is ParagraphBlock))
                //    sb.AppendLine();

                ExtractOnlyText_inline(leaf.Inline, sb);
            }
        }
        private static void ExtractOnlyText_inline(Inline inline, StringBuilder sb)
        {
            while (inline != null)
            {
                if (inline is LiteralInline literalInline)
                {
                    string text = literalInline.Content.ToString().Trim();
                    text = StripBulletSuffix(text, literalInline);

                    sb.AppendLine(text);        // the markup parser seems to eat newlines in paragraphs, instead making a list of literalinlines
                }
                else if (inline is ContainerInline containerInline)
                {
                    foreach (var childInline in containerInline)
                        ExtractOnlyText_inline(childInline, sb);
                }

                inline = inline.NextSibling;
            }
        }

        /// <summary>
        /// Had a case where where it was a list that looked like:
        ///  * hello *
        ///  * there *
        /// 
        /// This detects that and strips the delimiter from the right (and trims)
        /// </summary>
        private static string StripBulletSuffix(string text, LiteralInline fromInline)
        {
            char? list_bullet = GetListBullet_inline(fromInline);

            if (list_bullet != null && text.EndsWith(list_bullet.Value))
                text = text.Substring(0, text.Length - 1).Trim();

            return text;
        }

        /// <summary>
        /// This walks up the ancestery and if there is a ListBlock, it returns that listblock's delimiter.
        /// Otherwise returns null
        /// </summary>
        private static char? GetListBullet_inline(Inline inline)
        {
            if (inline is ContainerInline container)
                return GetListBullet_container(container);

            if (inline.Parent != null)
                return GetListBullet_container(inline.Parent);

            return null;
        }
        private static char? GetListBullet_container(ContainerInline container)
        {
            if (container.Parent != null)
            {
                char? retVal = GetListBullet_container(container.Parent);
                if (retVal != null)
                    return retVal;
            }

            if (container.ParentBlock != null)
            {
                char? retVal = GetListBullet_block(container.ParentBlock);
                if (retVal != null)
                    return retVal;
            }

            return null;
        }
        private static char? GetListBullet_block(Block block)
        {
            if (block is ListBlock list)
                return list.BulletType;

            if (block.Parent != null)
                return GetListBullet_block(block.Parent);

            return null;
        }

        #endregion
    }
}
