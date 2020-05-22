using System;
using System.IO;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class FileEnvironmentStorage : IEnvironmentStorage
    {
        private const string _fileName = "recent_env.txt";

        public void SaveEnvironment(string env)
        {
            var json = new JObject();
            json["ver"] = Constants.FileVersion.Value;
            json["env"] = env;
            File.WriteAllText(ProtectedFile.GetCacheFile(_fileName), json.ToString());
        }

        public string GetSavedEnvironment()
        {
            var file = ProtectedFile.GetCacheFile(_fileName);
            if (File.Exists(file))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                return json.Value<string>("env");
            }

            return Constants.ARMProdEnv;
        }

        public bool IsCacheValid()
        {
            var file = ProtectedFile.GetCacheFile(_fileName);
            if (!File.Exists(file))
            {
                return false;
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText(file));
                if (Constants.FileVersion.Value != json.Value<string>("ver"))
                {
                    ClearAll();
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                ClearAll();
                return false;
            }
        }

        public void ClearSavedEnvironment()
        {
            ClearAll();
        }

        private void ClearAll()
        {
            foreach (var filePath in Directory.GetFiles(Utils.GetDefaultCachePath(), "*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(filePath);
            }
        }
    }
}
