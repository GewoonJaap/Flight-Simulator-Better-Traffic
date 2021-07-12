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
        public static string MatchModel(Aircraft aircraft, List<Addon> addons = null)
        {
            List<Addon> installedAddons = addons ?? AddonScanner.ScanAddons();

            Log.Information($"Model matching: {aircraft.model} with airline: {aircraft.airline} and modelCode {aircraft.modelCode}");
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            if (models.GetValue(aircraft.model) == null) Log.Information($"Failed to model match: {aircraft.model}");
            string matchedModel = (string)models.GetValue(aircraft.model) ?? (string)models.GetValue(aircraft.modelCode) ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode)?.Title?.Replace("Asobo", "")?.Trim() ?? (string)models.GetValue("Default Aircraft") ?? "Airbus A320 Neo";

            if (installedAddons.FirstOrDefault(addon => addon.Title == matchedModel) == null && installedAddons.FirstOrDefault(addon => addon.Title == matchedModel + " Asobo") == null)
            {
                Log.Information($"Failed to model match: {matchedModel} not installed!");
                matchedModel = installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode)?.Title ?? "Airbus A320 Neo Asobo";
            }

            if (TryFindAircraft(models, installedAddons, aircraft, matchedModel) != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, aircraft, matchedModel);
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Log.Information($"Failed to model match: {matchedModel} Default");
                matchedModel = (string)models.GetValue($"{matchedModel} Default") ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode)?.Title ?? "Airbus A320 Neo Asobo";
            }
            Log.Information($"Model matched model: {aircraft.modelCode}, airline: {aircraft.airline} with: {matchedModel}");
            return matchedModel;
        }

        private static string TryFindAircraft(JObject models, List<Addon> installedAddons, Aircraft aircraft, string matchedModel)
        {
            string foundAircraft = null;

            if (models.GetValue($"{matchedModel} {aircraft.airline}") != null)
            {
                foundAircraft = (string)models.GetValue($"{matchedModel} {aircraft.airline}");
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith($"{matchedModel} {aircraft.airline} AI")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith($"{matchedModel} {aircraft.airline} AI")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith($"{matchedModel} {aircraft.airline}")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith($"{matchedModel} {aircraft.airline}")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith(matchedModel) && addon.Icao_Airline == aircraft.icaoAirline) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.StartsWith(matchedModel) && addon.Icao_Airline == aircraft.icaoAirline).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => (addon.Title.StartsWith(aircraft.modelCode) || addon.Title.StartsWith(matchedModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))) != null)
            {
                foundAircraft = installedAddons.First(addon => (addon.Title.StartsWith(aircraft.modelCode) || addon.Title.StartsWith(matchedModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.ModelCode == aircraft.modelCode && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))).Title;
            }

            return foundAircraft;
        }
    }
}
