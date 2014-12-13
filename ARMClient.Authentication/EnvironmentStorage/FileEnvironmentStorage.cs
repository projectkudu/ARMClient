using System;
using System.Diagnostics;
using System.IO;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class FileEnvironmentStorage : IEnvironmentStorage
    {
        public void SaveEnvironment(AzureEnvironments azureEnvironment)
        {
            File.WriteAllText(GetFilePath(), azureEnvironment.ToString());
        }

        public AzureEnvironments GetSavedEnvironment()
        {
            try
            {
                var file = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm"), "recent_env.txt");
                return (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), File.ReadAllText(file));
            }
            catch
            {
                return AzureEnvironments.Prod;
            }
        }

        public bool IsCacheValid()
        {
            return true;
        }

        public void ClearSavedEnvironment()
        {
            var filePath = GetFilePath();
            if (File.Exists(filePath))
            {
                Trace.WriteLine(string.Format("Deleting {0} ... ", filePath));
                File.Delete(filePath);
                Trace.WriteLine("Done!");
            }
        }

        private static string GetFilePath()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, "recent_env.txt");
        }
    }
}
