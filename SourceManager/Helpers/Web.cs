using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SourceManager.Helpers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Web
    {
        public static async Task<string> Get(string url)
        {
            var uri = new Uri(url);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000 * 5);

            string result;

            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(uri, cts.Token))
                {
                    result = await response.Content.ReadAsStringAsync();
                }
            }

            return result;
        }
    }
}
