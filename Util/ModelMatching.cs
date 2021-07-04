using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace Simvars.Util
{
    public static class ModelMatching
    {
        public static string MatchModel(string model, string airline, List<string> liveries = null)
        {
            List<string> installedLiveries = liveries ?? AddonScanner.ScanAddons();

            Console.WriteLine("Model matching:" + model + " with airline: " + airline);
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(model) == null) Console.WriteLine("Failed to model match: " + model);

            string matchedModel = (string)models.GetValue(model) ?? "Airbus A320 Neo";
            if (installedLiveries.Contains(matchedModel + " " + airline))
            {
                matchedModel = matchedModel + " " + airline;
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Console.WriteLine("Failed to model match: " + matchedModel + " Default");
                matchedModel = (string)models.GetValue(matchedModel + " Default") ?? "Airbus A320 Neo Asobo";
            }
            return matchedModel;
        }
    }
}
