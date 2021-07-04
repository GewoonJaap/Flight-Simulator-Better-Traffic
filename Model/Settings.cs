using Newtonsoft.Json;

namespace Simvars.Model
{
    public class Settings
    {
        public string CommunityFolderPath;
        public int MaximumAmountOfPlanes;

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this);

            //write string to file
            System.IO.File.WriteAllText(@".\Config\Settings.json", json);
        }
    }
}
