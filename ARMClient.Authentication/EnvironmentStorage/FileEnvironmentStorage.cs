using System;
using System.Diagnostics;
using System.IO;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.Utilities;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class FileEnvironmentStorage : IEnvironmentStorage
    {
        private const string _fileName = "recent_env.txt";

        public void SaveEnvironment(AzureEnvironments azureEnvironment)
        {
            File.WriteAllText(ProtectedFile.GetCacheFile(_fileName), azureEnvironment.ToString());
        }

        public AzureEnvironments GetSavedEnvironment()
        {
            var file = ProtectedFile.GetCacheFile(_fileName);
            if (File.Exists(file))
            {
                return (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), File.ReadAllText(file));
            }

            return AzureEnvironments.Prod;
        }

        public bool IsCacheValid()
        {
            return true;
        }

        public void ClearSavedEnvironment()
        {
            var filePath = ProtectedFile.GetCacheFile(_fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
