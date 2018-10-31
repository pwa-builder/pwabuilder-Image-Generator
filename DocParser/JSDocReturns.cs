using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocParser
{
    /// <summary>
    /// Parses usejsdoc.org/tags-returns.html
    /// </summary>
    public class JSDocReturns
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; }

        public static JSDocReturns Parse(string text)
        {
            var result = new JSDocReturns();

            var types = JSDocHelper.Split(text, "{", "}");
            if (types.Length > 0)
            {
                result.Type = types[0];
            }

            result.Description = JSDocHelper.After(text, "}").Trim();

            return result;
        }
    }
}
