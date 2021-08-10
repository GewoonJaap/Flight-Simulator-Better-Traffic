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

            Log.Information($"Model matching: {aircraft.model} with airline: {aircraft.airline}, airline ICAO Code: {aircraft.icaoAirline} and modelCode {aircraft.modelCode}");
            JObject models = JObject.Parse(File.ReadAllText(@".\Config\ModelMatching.json"));
            string matchedModel = (string)models.GetValue(aircraft.model) ?? (string)models.GetValue(aircraft.modelCode) ?? installedAddons.FirstOrDefault(addon => ((addon.ModelCode == aircraft.modelCode || addon.Title.Contains(aircraft.shortModel) || addon.Title.Contains(aircraft.shorterModelCode)) && addon.Icao_Airline == "") && addon.BaseAircraft)?.Title ?? installedAddons.FirstOrDefault(addon => (addon.ModelCode == aircraft.modelCode || addon.Title.Contains(aircraft.shortModel) || addon.Title.Contains(aircraft.shorterModelCode)) && addon.Icao_Airline == "")?.Title ?? installedAddons.FirstOrDefault(addon => addon.ModelCode.Contains(aircraft.shorterModelCode) || addon.Title.Contains(aircraft.shorterModelCode))?.Title ?? (string)models.GetValue("Default Aircraft") ?? "Airbus A320 Neo";

            matchedModel = matchedModel.Replace("Asobo", "")?.Trim();

            var test = installedAddons.FirstOrDefault(addon => addon.Title.Contains(aircraft.shortModel));

            if (installedAddons.FirstOrDefault(addon => addon.Title.StartsWith(matchedModel)) == null && installedAddons.FirstOrDefault(addon => addon.Title == matchedModel + " Asobo") == null)
            {
                Log.Information($"Failed to model match: {matchedModel} not installed!");
                if (installedAddons.FirstOrDefault(addon =>
                    addon.Title == (string)models.GetValue($"{matchedModel} Default")) != null)
                {
                    matchedModel = (string)models.GetValue($"{matchedModel} Default");
                }
                else
                {
                    matchedModel = installedAddons.FirstOrDefault(addon =>
                                       addon.ModelCode == aircraft.modelCode || addon.Title.Contains(aircraft.shortModel))
                                   ?.Title ??
                                   "Airbus A320 Neo";
                }
            }

            if (TryFindAircraft(models, installedAddons, aircraft, matchedModel) != null)
            {
                matchedModel = TryFindAircraft(models, installedAddons, aircraft, matchedModel);
            }
            else
            {
                if (models.GetValue(matchedModel + " Default") == null) Log.Information($"Failed to model match: {matchedModel} Default");
                matchedModel = (string)models.GetValue($"{matchedModel} Default") ?? installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode || addon.Title == matchedModel)?.Title ?? "Airbus A320 Neo Asobo";
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
            else if (installedAddons.FirstOrDefault(addon => addon.Title.Contains($"{matchedModel} {aircraft.airline}")) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.Contains($"{matchedModel} {aircraft.airline}")).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.Title.Contains(matchedModel) && addon.Icao_Airline == aircraft.icaoAirline) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.Title.Contains(matchedModel) && addon.Icao_Airline == aircraft.icaoAirline).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => (addon.Title.Contains(aircraft.modelCode) || addon.Title.Contains(matchedModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))) != null)
            {
                foundAircraft = installedAddons.First(addon => (addon.Title.Contains(aircraft.modelCode) || addon.Title.Contains(matchedModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => addon.ModelCode == aircraft.modelCode && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))) != null)
            {
                foundAircraft = installedAddons.First(addon => addon.ModelCode == aircraft.modelCode && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))).Title;
            }
            else if (installedAddons.FirstOrDefault(addon => (addon.ModelCode.Contains(aircraft.shorterModelCode) || addon.Title.Contains(aircraft.shorterModelCode) || addon.Title.Contains(aircraft.shortModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))) != null)
            {
                foundAircraft = installedAddons.First(addon => (addon.ModelCode.Contains(aircraft.shorterModelCode) || addon.Title.Contains(aircraft.shorterModelCode) || addon.Title.Contains(aircraft.shortModel)) && (addon.Title.Contains(aircraft.icaoAirline) || addon.Title.Contains(aircraft.airline))).Title;
            }

            return foundAircraft;
        }
    }
}
