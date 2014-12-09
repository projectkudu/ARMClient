using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AADHelpers
{
    static class TenantCache
    {
        public static Dictionary<string, TenantCacheInfo> GetCache()
        {
            var file = GetCacheFile();
            if (!File.Exists(file))
            {
                return new Dictionary<string, TenantCacheInfo>();
            }

            return JsonConvert.DeserializeObject<Dictionary<string, TenantCacheInfo>>(ProtectedFile.ReadAllText(file));
        }

        public static void SaveCache(Dictionary<string, TenantCacheInfo> cache)
        {
            var file = GetCacheFile();
            var json = JObject.FromObject(cache);
            ProtectedFile.WriteAllText(file, json.ToString());
        }

        private static string GetCacheFile()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, "cache_tenants.dat");
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
