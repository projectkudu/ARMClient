using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AADHelpers
{
    static class TokenCache
    {
        public static Dictionary<TokenCacheKey, string> GetCache(AzureEnvs env)
        {
            var file = GetCacheFile(env);
            if (!File.Exists(file))
            {
                return new Dictionary<TokenCacheKey, string>();
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, TokenCacheKey>>(File.ReadAllText(file));
            return dict.ToDictionary(p => p.Value, p => p.Key);
        }

        public static void SaveCache(AzureEnvs env, Dictionary<TokenCacheKey, string> cache)
        {
            var file = GetCacheFile(env);
            var dict = cache.ToDictionary(p => p.Value, p => p.Key);
            var json = JObject.FromObject(dict);
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
            return Path.Combine(path, String.Format("tokens_{0}.json", env));
        }
    }
}
