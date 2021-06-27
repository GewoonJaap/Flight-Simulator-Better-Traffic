using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Simvars.Util
{
    public static class ModelMatching
    {
        public static string MatchModel(string model, string airline)
        {
            Console.WriteLine("Model matching:" + model + " with airline: " + airline);
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(model) == null) Console.WriteLine("Failed to model match: " + model);
            string matchedModel = (string)models.GetValue(model) ?? "Airbus A320 Neo Asobo";
            return matchedModel;
        }
    }
}
