using System;
using Newtonsoft.Json;
using Sentry;
using Serilog;

namespace Simvars.Model
{
    public class Settings
    {
        public string CommunityFolderPath;
        public string AdditionalFolderPath;
        public int MaximumAmountOfPlanes;

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this);

                //write string to file
                System.IO.File.WriteAllText(@".\Config\Settings.json", json);
            }
            catch (Exception ex)
            {
                _ = SentrySdk.CaptureException(ex);
                Log.Error(ex.Message);
            }
        }
    }
}
