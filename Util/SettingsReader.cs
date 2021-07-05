using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Sentry;
using Simvars.Model;

namespace Simvars.Util
{
    public static class SettingsReader
    {
        public static Settings FetchSettings()
        {
            Settings settings;
            try
            {
                JObject json = JObject.Parse(File.ReadAllText(@".\Config\Settings.json"));
                settings = json.ToObject<Settings>();
            }
            catch (Exception ex)
            {
                _ = SentrySdk.CaptureException(ex);
                settings = new Settings()
                {
                    CommunityFolderPath = "PATH_HERE",
                    MaximumAmountOfPlanes = 40
                };
                settings.Save();
            }

            return settings;
        }
    }
}
