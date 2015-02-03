using System;
using System.Diagnostics;
using System.IO;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class FileEnvironmentStorage : IEnvironmentStorage
    {
        private const string _fileName = "recent_env.txt";

        public void SaveEnvironment(AzureEnvironments azureEnvironment)
        {
            var json = new JObject();
            json["ver"] = Constants.FileVersion.Value;
            json["env"] = azureEnvironment.ToString();
            File.WriteAllText(ProtectedFile.GetCacheFile(_fileName), json.ToString());
        }

        public AzureEnvironments GetSavedEnvironment()
        {
            var file = ProtectedFile.GetCacheFile(_fileName);
            if (File.Exists(file))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                return (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), json.Value<string>("env"));
            }

            return AzureEnvironments.Prod;
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

                AzureEnvironments unused;
                return Enum.TryParse<AzureEnvironments>(json.Value<string>("env"), out unused);
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
            foreach (var filePath in Directory.GetFiles(ProtectedFile.GetCachePath(), "*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(filePath);
            }
        }
    }
}
