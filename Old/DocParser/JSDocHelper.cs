//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Text;

namespace DocParser
{
    public static class JSDocHelper
    {
        public static JSDocItems GetItems(JSDocSplitSet set)
        {
            var results = new JSDocItems();

            var sb = new StringBuilder();
            var key = string.Empty;

            var lines = GetLines(set.Text);
            foreach (var line in lines)
            {
                if (line.StartsWith("@"))
                {
                    if (!string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(sb.ToString()))
                    {
                        results.List.Add(new KeyValuePair<string, string>(key, sb.ToString().Trim()));
                    }

                    sb.Clear();

                    var index = line.IndexOf(' ');
                    if (index > -1)
                    {
                        key = line.Substring(1, index - 1);
                        sb.Append(line.Substring(index + 1) + ' ');
                    }
                    else
                    {
                        // issue: if an @indicator is alone on a line, followed by the actual text, this will miss it.
                        key = line.Substring(1).Trim();
                        results.List.Add(new KeyValuePair<string, string>(key, sb.ToString().Trim()));
                        key = string.Empty;
                    }
                }
                else
                {
                    sb.Append(line + ' ');
                }
            }

            if (!string.IsNullOrWhiteSpace(key) || sb.Length > 0)
            {
                results.List.Add(new KeyValuePair<string, string>(key, sb.ToString().Trim()));
            }

            results.PreviousLine = set.PreviousLine;
            results.NextLine = set.NextLine;

            return results;
        }

        // Assumes text contains /** ... */ commenting. 'text' to contain a single comment block.
        // Returns lines without comments
        public static List<string> GetLines(string text)
        {
            var results = new List<string>();

            var lines = text.Split(new[] { '\n' });
            foreach (var fullline in lines)
            {
                var line = fullline.Trim();
                var index = line.IndexOf('*');
                if (index > -1)
                {
                    line = line.Substring(index + 1).Trim();
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                results.Add(line);
            }

            return results;
        }

        public static string[] SplitFrom(string text, string from, string between)
        {
            var index = text.IndexOf(from);
            if (index>-1)
            {
                text = text.Substring(index + from.Length);
            }

            return text.Split(new string[] { between }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string[] Split(string text, string left, string right)
        {
            var list = new List<string>();
            var indexLeft = -1;
            indexLeft = text.IndexOf(left, indexLeft+1);
            while (indexLeft>-1)
            {
                var indexRight = text.IndexOf(right, indexLeft+left.Length);
                if (indexRight>-1)
                {
                    list.Add(text.Substring(indexLeft+left.Length, indexRight - indexLeft - left.Length));
                    indexLeft = indexRight;
                }

                indexLeft = text.IndexOf(left, indexLeft + 1);
            }

            return list.ToArray();
        }

        public static JSDocSplitSet[] SpecialSplit(string text, string left, string right)
        {
            var list = new List<JSDocSplitSet>();
            var indexLeft = -1;
            indexLeft = text.IndexOf(left, indexLeft + 1);
            while (indexLeft > -1)
            {
                var indexRight = text.IndexOf(right, indexLeft + left.Length);
                if (indexRight > -1)
                {
                    var result = new JSDocSplitSet();

                    if (indexLeft > 0)
                    {
                        var before = JSDocHelper.PreviousLine(text, indexLeft);
                        if (before.ToLowerInvariant().Contains("function"))
                        {
                            result.PreviousLine = before;
                        }
                    }

                    if (indexRight > -1) { 
                        var after = JSDocHelper.NextLine(text, indexRight);
                        if (after.ToLowerInvariant().Contains("function"))
                        {
                            result.NextLine = after;
                        }
                    }

                    result.Text = text.Substring(indexLeft + left.Length, indexRight - indexLeft - left.Length);

                    list.Add(result);
                    indexLeft = indexRight;
                }

                indexLeft = text.IndexOf(left, indexLeft + 1);
            }

            return list.ToArray();
        }

        public static string PreviousLine(string text, int index)
        {
            var indexLeft = text.LastIndexOf("\n", index);
            if (indexLeft == -1)
            {
                return string.Empty;
            }

            var line = string.Empty;
            var valid = false;
            while (!valid)
            {
                var indexRight = text.LastIndexOf("\n", indexLeft - 1);
                while (indexRight > -1 && indexLeft - indexRight == 1)
                {
                    indexLeft = indexRight;
                    indexRight = text.LastIndexOf("\n", indexLeft - 1);
                }

                if (indexRight > -1)
                {
                    line = text.Substring(indexRight + 1, indexLeft - indexRight).Trim();
                    valid = !string.IsNullOrWhiteSpace(line);
                }
                else
                {
                    break;
                }

                indexLeft = indexRight;
                indexRight = text.LastIndexOf("\n", indexLeft - 1);
            }

            return line;
        }

        public static string NextLine(string text, int index)
        {
            var indexLeft = text.IndexOf("\n", index);
            if (indexLeft == -1)
            {
                return text;
            }

            var line = string.Empty;
            var valid = false;
            while (!valid)
            {
                var indexRight = text.IndexOf("\n", indexLeft + 1);
                while (indexRight - indexLeft == 1)
                {
                    indexLeft = indexRight;
                    indexRight = text.IndexOf("\n", indexLeft + 1);
                }

                if (indexRight > -1)
                {
                    line = text.Substring(indexLeft + 1, indexRight - indexLeft).Trim();
                    valid = !string.IsNullOrWhiteSpace(line);
                }
                else
                {
                    line = text.Substring(indexLeft + 1).Trim();
                    valid = !string.IsNullOrWhiteSpace(line);
                    break;
                }

                indexLeft = indexRight;
                indexRight = text.IndexOf("\n", indexLeft + 1);
            }

            return line;
        }

        public static string After(string text, string after, bool wholeIfMissing = true)
        {
            var index = text.IndexOf(after);
            return index == -1 ? ((wholeIfMissing) ? text : string.Empty) : text.Substring(index + after.Length);
        }

        public static string Before(string text, string before, bool wholeIfMissing = true)
        {
            var index = text.IndexOf(before);
            return index == -1 ? ((wholeIfMissing) ? text : string.Empty) : text.Substring(0, index);
        }
    }
}