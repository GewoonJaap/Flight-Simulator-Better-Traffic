using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Simvars.Util
{
    public static class ApiRequest
    {
        public static JObject MakeGetRequest(string url)
        {
            using (var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                HttpResponseMessage response = client.GetAsync(url).Result;
                JObject returnValue = new JObject { ["success"] = false };

                if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    returnValue["success"] = true;
                    returnValue["data"] = JObject.Parse(result);
                }
                return returnValue;
            }
        }
    }
}
