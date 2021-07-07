using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Sentry;
using Serilog;
using Simvars.Model;

namespace Simvars.Util
{
    public static class AddonScanner
    {
        public static List<Addon> ScanAddons()
        {
            Log.Information("Searching for installed add-ons");
            List<Addon> result = new List<Addon>();
            try
            {
                string communityFolder = GetCommunityFolder();
                string officialFolder = communityFolder.Replace("Community", "Official");
                if (communityFolder != null) result = GetInstalledAddons(communityFolder);
                if (Directory.Exists(officialFolder))
                {
                    string[] officialDirectories = Directory.GetDirectories(officialFolder);
                    if (officialDirectories.Length == 1)
                    {
                        officialFolder = officialDirectories[0] + "\\";
                    }
                    result.AddRange(GetInstalledAddons(officialFolder));
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Log.Error(ex.Message);
            }

            result.ForEach(addon =>
            {
                Log.Information($"Found add-on with title: {addon.Title}, ICAO Airline: {addon.Icao_Airline}, model code: {addon.ModelCode}");
            });
            Log.Information($"Found {result.Count} installed add-ons in Official and Community folder");
            return result;
        }

        private static List<Addon> GetInstalledAddons(string communityFolder)
        {
            List<Addon> addons = new List<Addon>();
            string[] addonDirectories = Directory.GetDirectories(communityFolder);
            foreach (string addonDirectory in addonDirectories)
            {
                string finalDirectory = addonDirectory;
                Log.Information($"Found add-on directory: {addonDirectory}");
                try
                {
                    JObject manifest = JObject.Parse(File.ReadAllText(addonDirectory + "\\manifest.json"));
                    if (((string)manifest["content_type"])?.ToLower() != "aircraft") continue;
                }
                catch (Exception)
                {
                    Log.Information("Failed to read json, ignoring this.");
                }

                if (File.Exists(finalDirectory + "\\aircraft.cfg"))
                {
                    addons.AddRange(ParseCfg(finalDirectory + "\\aircraft.cfg"));
                }

                finalDirectory += "\\SimObjects\\Airplanes";

                if (!Directory.Exists(finalDirectory)) continue;

                string[] airplaneDirectories = Directory.GetDirectories(finalDirectory);

                if (airplaneDirectories.Length == 0) continue;

                foreach (string directory in airplaneDirectories)
                {
                    finalDirectory = directory + "\\aircraft.cfg";

                    if (!File.Exists(finalDirectory)) continue;

                    addons.AddRange(ParseCfg(finalDirectory));
                }
            }
            return addons;
        }

        private static List<Addon> ParseCfg(string cfgPath)
        {
            List<Addon> addons = new List<Addon>();
            string[] lines = System.IO.File.ReadAllLines(cfgPath);
            Addon curentAddon = null;

            string title = "";
            string modelCode = "";
            string icaoAirline = "";
            foreach (string line in lines)
            {
                // Use a tab to indent each line of the file.
                if (line.ToLower().Trim().StartsWith("[fltsim"))
                {
                    if (curentAddon != null && curentAddon.Title != String.Empty && !curentAddon.Title.Contains("AirTraffic"))
                    {
                        addons.Add(curentAddon);
                        curentAddon = null;
                        title = "";
                        icaoAirline = "";
                    }
                }
                if (!line.ToLower().StartsWith("title") && !line.ToLower().StartsWith("icao_type_designator") && !line.ToLower().StartsWith("icao_airline")) continue;

                string value = line.Split('=')[1].Trim();
                if (value.StartsWith("\""))
                {
                    value = value.Split('"')[1].Trim();
                    value = value.Split('"')[0].Trim();
                }
                else if (value.EndsWith(" "))
                {
                    value = value.Split(' ')[0].Trim();
                }
                else if (value.EndsWith(";") || value.Contains(";"))
                {
                    value = value.Split(';')[0].Trim();
                }

                if (line.ToLower().StartsWith("title"))
                {
                    title = value;
                }
                else if (line.ToLower().StartsWith("icao_type_designator"))
                {
                    modelCode = value;
                }
                else
                {
                    icaoAirline = value;
                }

                if (curentAddon == null)
                {
                    curentAddon = new Addon();
                }

                curentAddon.Title = title.Trim();
                curentAddon.ModelCode = modelCode.Trim();
                curentAddon.Icao_Airline = icaoAirline.Trim();
            }

            if (curentAddon != null && !curentAddon.Title.Contains("AirTraffic"))
            {
                addons.Add(curentAddon);
            }

            return addons;
        }

        private static string GetCommunityFolder()
        {
            string addonPath = "";
            string msfsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Packages\\Microsoft.FlightSimulator_8wekyb3d8bbwe\\LocalCache\\UserCfg.opt";
            string msfsDirectorySteam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft Flight Simulator\\UserCfg.opt";

            Settings settings = SettingsReader.FetchSettings();

            if (settings.CommunityFolderPath != "PATH_HERE" && Directory.Exists(settings.CommunityFolderPath))
            {
                return settings.CommunityFolderPath;
            }

            string packagePath = File.Exists(msfsDirectorySteam) ? msfsDirectorySteam : msfsDirectory;

            if (!File.Exists(packagePath))
            {
                MessageBox.Show("Failed to find your Community folder!\nPlease set the location to the Community folder in the 'Config/Settings.json' file and restart the application!\nIgnoring this error will make the livery matching unavailable", "Failed to find Community Folder");
                return null;
            }

            string[] lines = System.IO.File.ReadAllLines(packagePath);
            foreach (string line in lines)
            {
                // Use a tab to indent each line of the file.
                if (!line.StartsWith("InstalledPackagesPath")) continue;

                addonPath = line.Split('"')[1];
                addonPath = addonPath.Split('"')[0];
                addonPath += "\\Community\\";

                if (!Directory.Exists(addonPath))
                {
                    Directory.CreateDirectory(addonPath);
                }
            }
            Log.Information(addonPath);
            settings.CommunityFolderPath = addonPath;
            settings.Save();

            return addonPath;
        }
    }
}
