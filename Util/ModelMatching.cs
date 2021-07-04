using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simvars.Util
{
    public static class ModelMatching
    {
        public static string MatchModel(string model, string airline, List<string> liveries = null)
        {
            List<string> installedLiveries = liveries ?? AddonScanner.ScanAddons();

            Console.WriteLine($"Model matching: {model} with airline: {airline}");
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(model) == null) Console.WriteLine($"Failed to model match: {model}");
            string matchedModel = (string)models.GetValue(model) ?? (string)models.GetValue("Default Aircraft") ?? "Airbus A320 Neo";

            if (TryFindAircraft(models, installedLiveries, $"{matchedModel} {airline}") != null)
            {
                matchedModel = TryFindAircraft(models, installedLiveries, $"{matchedModel} {airline}");
            }
            else if (airline.Contains("(") && TryFindAircraft(models, installedLiveries, $"{matchedModel} {airline.Split('(')[0].Trim()}") != null)
            {
                matchedModel = TryFindAircraft(models, installedLiveries, $"{matchedModel} {airline.Split('(')[0].Trim()}");
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Console.WriteLine($"Failed to model match: {matchedModel} Default");
                matchedModel = (string)models.GetValue($"{matchedModel} Default") ?? "Airbus A320 Neo Asobo";
            }
            Console.WriteLine($"Model matched {model} {airline} with: {matchedModel}");
            return matchedModel;
        }

        private static string TryFindAircraft(JObject models, List<string> installedLiveries, string fullName)
        {
            string foundAircraft = null;

            if (models.GetValue(fullName) != null)
            {
                foundAircraft = (string)models.GetValue(fullName);
            }
            else if (installedLiveries.Contains($"{fullName} AI"))
            {
                foundAircraft = $"{fullName} AI";
            }
            else if (installedLiveries.Contains(fullName))
            {
                foundAircraft = fullName;
            }

            return foundAircraft;
        }
    }
}
