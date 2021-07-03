using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Sentry;

namespace Simvars.Util
{
    public static class AddonScanner
    {
        public static List<string> ScanAddons()
        {
            List<string> result = new List<string>();
            try
            {
                string communityFolder = GetCommunityFolder();
                result = GetInstalledLiveries(communityFolder);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            return result;
        }

        private static List<string> GetInstalledLiveries(string communityFolder)
        {
            List<string> liveries = new List<string>();
            string[] addonDirectories = Directory.GetDirectories(communityFolder);
            foreach (string addonDirectory in addonDirectories)
            {
                string finalDirectory = addonDirectory;
                JObject manifest = JObject.Parse(File.ReadAllText(addonDirectory + "\\manifest.json"));
                if (((string)manifest["content_type"])?.ToLower() != "aircraft") continue;
                finalDirectory += "\\SimObjects\\Airplanes";
                string[] airplaneDirectories = Directory.GetDirectories(finalDirectory);
                finalDirectory = airplaneDirectories[0] + "\\aircraft.cfg";

                liveries.AddRange(ParseCfg(finalDirectory));
            }
            return liveries;
        }

        private static List<string> ParseCfg(string cfgPath)
        {
            List<string> liveryNames = new List<string>();
            string[] lines = System.IO.File.ReadAllLines(cfgPath);
            foreach (string line in lines)
            {
                // Use a tab to indent each line of the file.
                if (!line.StartsWith("title")) continue;

                string liveryName = line.Split('=')[1].Trim();
                if (liveryName.StartsWith("\""))
                {
                    liveryName = liveryName.Split('"')[1].Trim();
                    liveryName = liveryName.Split('"')[0].Trim();
                }
                else if (liveryName.EndsWith(" "))
                {
                    liveryName = liveryName.Split(' ')[0].Trim();
                }
                else if (liveryName.EndsWith(";"))
                {
                    liveryName = liveryName.Split(';')[0].Trim();
                }
                liveryNames.Add(liveryName.Trim());
            }

            return liveryNames;
        }

        private static string GetCommunityFolder()
        {
            string addonPath = "";
            string msfsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Packages\\Microsoft.FlightSimulator_8wekyb3d8bbwe\\LocalCache\\";
            string msfsDirectorySteam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft Flight Simulator\\";

            string packagePath = Directory.Exists(msfsDirectorySteam) ? msfsDirectorySteam : msfsDirectory;

            string[] lines = System.IO.File.ReadAllLines(packagePath + "UserCfg.opt");
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
            Console.WriteLine(addonPath);
            return addonPath;
        }
    }
}
