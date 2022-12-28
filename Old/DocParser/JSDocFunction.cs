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
using System.Diagnostics;
using System.Linq;

namespace DocParser
{
    public class JSDocFunction : JSDocNode
    {
        public List<JSDocParam> Parameters { get; set; } = new List<JSDocParam>();
        public JSDocParam Returns { get; set; }

        internal override void Parse(JSDocItems items)
        {
            base.Parse(items);

            foreach (var item in items.List)
            {
                switch (item.Key)
                {
                    case "param":
                        {
                            this.Parameters.Add(JSDocParam.Parse(item.Value));
                            break;
                        }

                    case "function":
                    case "method":
                        {
                            if (!string.IsNullOrWhiteSpace(item.Value))
                            {
                                this.Method = item.Value;
                            }

                            break;
                        }
                    case "func":
                    case "alias":
                        {
                            if (!string.IsNullOrWhiteSpace(item.Value))
                            {
                                this.Name = item.Value;
                            }

                            break;
                        }
                    case "returns":
                    case "return":
                        {
                            this.Returns = JSDocParam.Parse(item.Value);

                            break;
                        }
                }
            }

            if (string.IsNullOrWhiteSpace(this.Name))
            {
                var fnLine = (items.PreviousLine != null && items.PreviousLine.Contains("function")) ? items.PreviousLine :
                    (items.NextLine != null && items.NextLine.Contains("function")) ? items.NextLine : string.Empty;

                if (!string.IsNullOrWhiteSpace(fnLine))
                {
                    var index = fnLine.IndexOf("function");
                    if (index > 0)
                    {
                        var before = fnLine.Substring(0, index).Trim();
                        if (!string.IsNullOrWhiteSpace(before))
                        {
                            var iEquals = before.IndexOf("=");
                            var beforeEquals = before.Substring(0, iEquals).Trim();
                            var lastWord = beforeEquals.Split(new[] { ' ' }).LastOrDefault();

                            this.Name = lastWord;
                        }
                    }
                    else
                    {
                        var words = fnLine.Substring(index + 8).Trim().Split(new[] { ' ', '(' });
                        if (words.Length > 0)
                        {
                            this.Name = words[0];
                        }
                    }

                    if (fnLine.Contains("("))
                    {
                        var parmset = JSDocHelper.Split(fnLine, "(", ")");
                        if (parmset.Length > 0)
                        {
                            var parms = parmset[0].Split(new[] { ',' });
                            foreach (var parm in parms)
                            {
                                var segments = parm.Split(new[] { '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 1)
                                {
                                    var subparm = this.Parameters.FirstOrDefault(p => p.Name.Equals(segments[0]));
                                    if (subparm != null)
                                    {
                                        subparm.Default = segments[1].Trim();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}