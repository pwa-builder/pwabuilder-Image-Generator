using SourceManager;
using SourceManager.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Mvc;

namespace RenoService.Controllers
{
    public class WinrtController : ApiController
    {

        private async Task<List<string>> SourceList()
        {
            var url = ConfigurationManager.AppSettings["consumableURL"];
            var source = await Web.Get(url);
            var list = source.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return list.ToList();
        }

        //https://raw.githubusercontent.com/JimGaleForce/Windows-universal-js-samples/master/win10/tile.js

        // GET api/values
        [System.Web.Mvc.Route("api/source")]
        public async Task<SourceData> Get()
        {
            var list = await SourceList();
            //TODO: Get list of known file of options/source.
            var mgr = new SourceManager.SourceManager();
            var catalog = await mgr.GetCatalog(list, true);

            var sourcePath = Path.GetTempPath() + "\\original";
            if (!Directory.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }

            sourcePath += "\\";

            for (int i = 0; i < catalog.Count; i++)
            {
                var sourceSet = catalog[i];

                var url = sourceSet.Source.Url;
                var index = url.LastIndexOf("/");
                var hash = sourceSet.Code.GetHashCode();
                var name = url.Substring(index + 1);
                var period = name.IndexOf(".");
                var newName = name.Substring(0, period) + "-" + hash + name.Substring(period);

                sourceSet.Source.Hash = hash.ToString();


                if (!File.Exists(sourcePath + newName))
                {
                    try
                    {
                        File.WriteAllText(sourcePath + newName, sourceSet.Code);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
            }

            var source = new SourceData();
            source.Sources = catalog.Select(s => s.Source).ToList();
            var doc = new Documentation();

            for (int i = 0; i < source.Sources.Count; i++)
            {
                var cat = catalog[i];
                var src = source.Sources[i];
                //source.Sources[0].Parsed.Functions
                for (int j = 0; j < src.Parsed.Functions.Count; j++)
                {
                    var fn = src.Parsed.Functions[j];
                    var item = new RenoItem();
                    item.url = src.Url;
                    item.id = src.Id + "." + fn.Name;
                    item.image = fn.Image;
                    item.parms = new List<RenoParm>();
                    for (int k = 0; k < fn.Parameters.Count; k++)
                    {
                        var parm = fn.Parameters[k];
                        item.parms.Add(new RenoParm { id = item.id + "." + parm.Name, defaultData = parm.Default });
                    }

                    doc.Add(item, cat, true);
                    fn.Snippet = doc.GetDocumentation();
                    doc.Clear();
                }
            }

            return source;
        }

        private Random random = new Random();

        [System.Web.Mvc.HttpPost]
        // POST api/values
        public async Task<HttpResponseMessage> Generate([FromBody] RenoData renoData)
        {
            var files = new List<string>();

            var sourcePath = Path.GetTempPath() + "\\original\\";

            var rnd = random.Next(1000000, 9999999);
            var path = Path.GetTempPath();
            var newPath = path + "\\reno-" + rnd;
            var folder = Directory.CreateDirectory(newPath);

            var list = await SourceList();
            var mgr = new SourceManager.SourceManager();
            var catalog = await mgr.GetCatalog(list, true);

            var copied = false;
            var doc = new Documentation();

            for (int i = 0; i < renoData.Controls.Count; i++)
            {
                var item = renoData.Controls[i];
                var url = item.url;
                var hash = item.hash;

                var index = url.LastIndexOf("/");
                var name = url.Substring(index + 1);
                var period = name.IndexOf(".");
                var newName = name.Substring(0, period) + "-" + hash + name.Substring(period);

                if (File.Exists(sourcePath + newName) && !File.Exists(newPath + "\\" + newName))
                {
                    //copy to directory
                    File.Copy(sourcePath + newName, newPath + "\\" + newName);
                    files.Add(newName);

                    var catItem = catalog.FirstOrDefault(c => c.Source.Url == item.url);

                    //create documentation here
                    doc.Add(item, catItem, false);

                    copied = true;
                }
            }

            if (copied)
            {
                files.Add("winmain.js");
                File.WriteAllBytes(newPath + "\\winmain.js", Encoding.ASCII.GetBytes(doc.GetDocumentation()));

                var sb = new StringBuilder();
                sb.Append("<html>\n<head>\n</head>\n<body>\n");
                foreach (var item in files)
                {
                    sb.Append(string.Format("<script src=\"{0}\"></script>", item) + "\n");
                }

                sb.Append("</body>\n</html>");

                File.WriteAllBytes(newPath + "\\include.html", Encoding.ASCII.GetBytes(sb.ToString()));

                ZipFile.CreateFromDirectory(newPath, newPath + ".zip");

                var path2 = newPath + ".zip";
                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                var stream = new FileStream(path2, FileMode.Open, FileAccess.Read);
                result.Content = new StreamContent(stream);
                result.Content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/octet-stream");
                return result;
            }

            return null;

            //return new FilePathResult(newPath + ".zip", "application/zip");

            //return zip of collected sources, info on how to use (events?)
        }
    }

    public class RenoParm
    {
        public string id { get; set; }
        public string defaultData { get; set; }
    }

    public class RenoItem
    {
        public string id { get; set; }
        public string url { get; set; }
        public string hash { get; set; }
        public List<RenoParm> parms { get; set; }
        public string image { get; set; }
    }

    public class RenoData
    {
        public List<RenoItem> Controls { get; set; } = new List<RenoItem>();
    }

    public class SourceData
    {
        public List<Source> Sources { get; set; }
    }

    public class Documentation
    {
        StringBuilder sb = new StringBuilder();

        public void Add(RenoItem item, SourceSet set, bool asTemplate)
        {
            var function = set.Source.Parsed.Functions.ToArray().FirstOrDefault(f => item.id.EndsWith("." + f.Name));
            if (function != null)
            {
                sb.Append("/**" + function.Snippet.Replace("\n", "\r\n") +
                    "\r\n * @see " + item.url + "\r\n" +
                    "*/\r\n");

                var line = "";
                var preline = "";
                var allParameters = true;

                sb.Append("\r\n");

                if (function.Parameters.Count > 0)
                {
                    for (int i = 0; i < function.Parameters.Count; i++)
                    {
                        var parm = function.Parameters[i];

                        var actualValue = (item.parms[i].defaultData == "null"
                            ? "null"
                            : parm.Type == "string" ? ("\"" + item.parms[i].defaultData + "\"") : item.parms[i].defaultData);

                        //TODO: literal values vs. others (like document.title);

                        if (allParameters || (item.parms[i].defaultData != "" && item.parms[i].defaultData != "null"))
                        {
                            sb.Append("var " + parm.Name + " = " + actualValue + ";\r\n");
                            line += preline + parm.Name + ", ";
                            preline = "";
                        }
                        else
                        {
                            preline += actualValue + ", ";
                        }
                    }

                    line = (function.Returns != null ? ("var " + function.Returns.Type.ToLower() + " = ") : "") + function.Method + "(" +
                            line.Substring(0, line.Length - 2) + ");";
                }
                else
                {
                    line = (function.Returns != null ? ("var " + function.Returns.Type.ToLower() + " = ") : "") + function.Method + "();";
                }


                sb.Append("\r\n" + line + "\r\n\r\n");
            }
        }

        public string GetDocumentation()
        {
            var result = sb.ToString();
            return result;
        }

        public void Clear()
        {
            sb.Clear();
        }
    }
}