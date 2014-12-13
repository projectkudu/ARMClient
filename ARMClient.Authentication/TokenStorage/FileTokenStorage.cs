using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ARMClient.Authentication.Utilities;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.TokenStorage
{
    internal class FileTokenStorage : ITokenStorage
    {
        public Dictionary<TokenCacheKey, string> GetCache()
        {
            var file = GetCacheFile();
            if (!File.Exists(file))
            {
                return new Dictionary<TokenCacheKey, string>();
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, TokenCacheKey>>(ProtectedFile.ReadAllText(file));
            return dict.ToDictionary(p => p.Value, p => p.Key);
        }

        public bool TryGetRecentToken(out AuthenticationResult recentToken)
        {
            try
            {
                var recentTokenFile = GetRecentTokenFile();
                var authResult = AuthenticationResult.Deserialize(ProtectedFile.ReadAllText(recentTokenFile));
                if (!String.IsNullOrEmpty(authResult.RefreshToken) && authResult.ExpiresOn <= DateTime.UtcNow)
                {
                    recentToken = null;
                    return false;
                }
                recentToken = authResult;
                return true;
            }
            catch
            {
                recentToken = null;
                return false;
            }
        }

        public void SaveCache(Dictionary<TokenCacheKey, string> tokens)
        {
            var file = GetCacheFile();
            var dict = tokens.ToDictionary(p => p.Value, p => p.Key);
            var json = JObject.FromObject(dict);
            ProtectedFile.WriteAllText(file, json.ToString());
        }

        public void SaveRecentToken(AuthenticationResult authResult)
        {
            ProtectedFile.WriteAllText(GetRecentTokenFile(), authResult.Serialize());
        }

        public bool IsCacheValid()
        {
            var cache = GetCache();
            return cache != null && cache.Count > 0;
        }

        public void ClearCache()
        {
            var filePaths = new[] { GetCacheFile(), GetRecentTokenFile() };
            foreach (var filePath in filePaths.Where(File.Exists))
            {
                Trace.WriteLine(string.Format("Deleting {0} ... ", filePath));
                File.Delete(filePath);
                Trace.WriteLine("Done!");
            }
        }

        private static string GetCacheFile()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, "cache_tokens.dat");
        }

        private static string GetRecentTokenFile()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".csm");
            Directory.CreateDirectory(path);
            return Path.Combine(path, "recent_token.dat");
        }
    }
}
