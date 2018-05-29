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

using System.Collections.Generic;

namespace DocParser
{
    public class JSDocParsed
    {
        public List<JSDocFunction> Functions { get; set; } = new List<JSDocFunction>();
        public JSDocFile File { get; set; }

        public void Parse(string text, bool includeComments)
        {
            var blocks = JSDocHelper.SpecialSplit(text, "/**", "*/");
            foreach (var block in blocks)
            {
                var node = JSDocNode.Parse(block, includeComments);
                if (node is JSDocFunction && !string.IsNullOrWhiteSpace(node?.Name))
                {
                    Functions.Add(node as JSDocFunction);
                }

                if (node is JSDocFile)
                {
                    File = node as JSDocFile;
                }
            }
        }                    
    }
}