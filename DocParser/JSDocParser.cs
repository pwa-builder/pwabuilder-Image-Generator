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

namespace DocParser
{
    public class JSDocParser
    {
        private string text;

        public void Load(string filename)
        {
            text = System.IO.File.ReadAllText(filename);
        }

        public void LoadText(string text)
        {
            this.text = text;
        }

        public JSDocParsed Parse(string filename = null, bool includeComments = false)
        {
            if (filename != null)
            {
                this.Load(filename);
            }

            var result = new JSDocParsed();
            result.Parse(text, includeComments);
            return result;
        }
    }
}