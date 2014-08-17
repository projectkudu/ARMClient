using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AADHelpers
{
    static class TenantCache
    {
        public static Dictionary<string, TenantCacheInfo> GetCache(AzureEnvs env)
        {
            var file = GetCacheFile(env);
            if (!File.Exists(file))
            {
                return new Dictionary<string, TenantCacheInfo>();
            }

            return JsonConvert.DeserializeObject<Dictionary<string, TenantCacheInfo>>(File.ReadAllText(file));
        }

        public static void SaveCache(AzureEnvs env, Dictionary<string, TenantCacheInfo> cache)
        {
            var file = GetCacheFile(env);
            var json = JObject.FromObject(cache);
            File.WriteAllText(file, json.ToString());
        }

        public static void ClearCache(AzureEnvs env)
        {
            var file = GetCacheFile(env);
            Console.Write("Deleting {0} ... ", file);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            Console.WriteLine("Done!");
        }

        private static string GetCacheFile(AzureEnvs env)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, String.Format("tenants_{0}.json", env));
        }
    }

    public class TenantCacheInfo
    {
        public string tenantId { get; set; }
        public string displayName { get; set; }
        public string domain { get; set; }
        public SubscriptionCacheInfo[] subscriptions { get; set; }
    }

    public class SubscriptionCacheInfo
    {
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
    }
}
