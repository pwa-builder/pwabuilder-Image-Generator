using DocParser;
using SourceManager.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceManager
{
    public class SourceManager
    {
        public async Task<List<SourceSet>> GetCatalog(List<string> urls, bool includeComments = false)
        {
            var result = new List<SourceSet>();

            foreach (var url in urls)
            {
                var source = await Web.Get(url);
                var js = new JSDocParser();
                js.LoadText(source);
                var parsed = js.Parse(includeComments: includeComments);
                var set = new SourceSet()
                {
                    Source = new Source { Url = url, Parsed = parsed, Id = url.GetHashCode().ToString() },
                    Code = source
                };

                result.Add(set);
            }

            return result;
        }
    }

    public class Source
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public JSDocParsed Parsed { get; set; }
        public string Hash { get; set; }
    }

    public class SourceSet
    {
        public Source Source { get; set; }
        public string Code { get; set; }
    }
}
