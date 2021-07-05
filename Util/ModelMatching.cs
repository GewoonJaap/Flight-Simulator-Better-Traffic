using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simvars.Model;

namespace Simvars.Util
{
    public static class ModelMatching
    {
        public static string MatchModel(string modelCode, string model, string airline, List<Addon> addons = null)
        {
            List<Addon> installedAddons = addons ?? AddonScanner.ScanAddons();

            Console.WriteLine($"Model matching: {model} with airline: {airline} and modelCode {modelCode}");
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(model) == null) Console.WriteLine($"Failed to model match: {model}");
            string matchedModel = (string)models.GetValue(model) ?? (string)models.GetValue(modelCode) ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == modelCode)?.Title ?? (string)models.GetValue("Default Aircraft") ?? "Airbus A320 Neo";

            if (TryFindAircraft(models, installedAddons, $"{matchedModel} {airline}") != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, $"{matchedModel} {airline}");
            }
            else if (airline.Contains("(") && TryFindAircraft(models, installedAddons, $"{matchedModel} {airline.Split('(')[0].Trim()}") != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, $"{matchedModel} {airline.Split('(')[0].Trim()}");
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Console.WriteLine($"Failed to model match: {matchedModel} Default");
                matchedModel = (string)models.GetValue($"{matchedModel} Default") ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == modelCode)?.Title ?? "Airbus A320 Neo Asobo";
            }
            Console.WriteLine($"Model matched {model} {airline} with: {matchedModel}");
            return matchedModel;
        }

        private static string TryFindAircraft(JObject models, List<Addon> installedAddons, string fullName)
        {
            string foundAircraft = null;

            if (models.GetValue(fullName) != null)
            {
                foundAircraft = (string)models.GetValue(fullName);
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith($"{fullName} AI")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith($"{fullName} AI")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith(fullName)) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith(fullName)).Title;
            }

            return foundAircraft;
        }
    }
}
