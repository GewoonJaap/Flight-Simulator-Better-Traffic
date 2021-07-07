using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using Simvars.Model;

namespace Simvars.Util
{
    public static class ModelMatching
    {
        public static string MatchModel(string modelCode, string model, string airline, string icaoAirline, List<Addon> addons = null)
        {
            List<Addon> installedAddons = addons ?? AddonScanner.ScanAddons();

            Log.Information($"Model matching: {model} with airline: {airline} and modelCode {modelCode}");
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(model) == null) Log.Information($"Failed to model match: {model}");
            string matchedModel = (string)models.GetValue(model) ?? (string)models.GetValue(modelCode) ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == modelCode)?.Title?.Replace("Asobo", "")?.Trim() ?? (string)models.GetValue("Default Aircraft") ?? "Airbus A320 Neo";

            if (installedAddons.FirstOrDefault(addon => addon.Title == matchedModel) == null && installedAddons.FirstOrDefault(addon => addon.Title == matchedModel + " Asobo") == null)
            {
                Log.Information($"Failed to model match: {matchedModel} not installed!");
                matchedModel = installedAddons.FirstOrDefault(addon => addon.ModelCode == modelCode)?.Title ?? "Airbus A320 Neo Asobo";
            }

            if (TryFindAircraft(models, installedAddons, matchedModel, airline, icaoAirline) != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, matchedModel, airline, icaoAirline);
            }
            else if (airline.Contains("(") && TryFindAircraft(models, installedAddons, matchedModel, airline.Split('(')[0].Trim(), icaoAirline) != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, matchedModel, airline.Split('(')[0].Trim(), icaoAirline);
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Log.Information($"Failed to model match: {matchedModel} Default");
                matchedModel = (string)models.GetValue($"{matchedModel} Default") ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == modelCode)?.Title ?? "Airbus A320 Neo Asobo";
            }
            Log.Information($"Model matched {model} {airline} with: {matchedModel}");
            return matchedModel;
        }

        private static string TryFindAircraft(JObject models, List<Addon> installedAddons, string model, string airline, string icao)
        {
            string foundAircraft = null;

            if (models.GetValue($"{model} {airline}") != null)
            {
                foundAircraft = (string)models.GetValue($"{model} {airline}");
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith($"{model} {airline} AI")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith($"{model} {airline} AI")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith($"{model} {airline}")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith($"{model} {airline}")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith(model) && addon.Icao_Airline == icao) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith(model) && addon.Icao_Airline == icao).Title;
            }

            return foundAircraft;
        }
    }
}
