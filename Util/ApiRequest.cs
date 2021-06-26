using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Simvars.Util
{
    public static class ApiRequest
    {
        public static JObject GetAircraftData(string id)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                HttpResponseMessage response = client.GetAsync("https://data-live.flightradar24.com/clickhandler/?version=1.5&flight=" + id).Result;
                response.EnsureSuccessStatusCode();
                string result = response.Content.ReadAsStringAsync().Result;
                return JObject.Parse(result);
            }
        }
    }
}
