using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AADHelpers
{
    static class TokenCache
    {
        public static Dictionary<TokenCacheKey, string> GetCache()
        {
            var file = GetCacheFile();
            if (!File.Exists(file))
            {
                return new Dictionary<TokenCacheKey, string>();
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, TokenCacheKey>>(ProtectedFile.ReadAllText(file));
            return dict.ToDictionary(p => p.Value, p => p.Key);
        }

        public static void SaveCache(Dictionary<TokenCacheKey, string> cache)
        {
            var file = GetCacheFile();
            var dict = cache.ToDictionary(p => p.Value, p => p.Key);
            var json = JObject.FromObject(dict);
            ProtectedFile.WriteAllText(file, json.ToString());
        }

        private static string GetCacheFile()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, "cache_tokens.dat");
        }
    }
}