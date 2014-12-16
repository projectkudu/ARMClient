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
        private const string _cacheFileName = "cache_tokens.dat";
        private const string _recentFileName = "recent_token.dat";

        public Dictionary<TokenCacheKey, string> GetCache()
        {
            var file = ProtectedFile.GetCacheFile(_cacheFileName);
            if (!File.Exists(file))
            {
                return new Dictionary<TokenCacheKey, string>();
            }

            var dict = JsonConvert.DeserializeObject<Dictionary<string, TokenCacheKey>>(ProtectedFile.ReadAllText(file));
            return dict.ToDictionary(p => p.Value, p => p.Key);
        }

        public void SaveCache(Dictionary<TokenCacheKey, string> tokens)
        {
            var dict = tokens.ToDictionary(p => p.Value, p => p.Key);
            var json = JObject.FromObject(dict);
            ProtectedFile.WriteAllText(ProtectedFile.GetCacheFile(_cacheFileName), json.ToString());
        }

        public AuthenticationResult GetRecentToken()
        {
            return AuthenticationResult.Deserialize(ProtectedFile.ReadAllText(ProtectedFile.GetCacheFile(_recentFileName)));
        }

        public void SaveRecentToken(AuthenticationResult authResult)
        {
            ProtectedFile.WriteAllText(ProtectedFile.GetCacheFile(_recentFileName), authResult.Serialize());
        }

        public bool IsCacheValid()
        {
            var cache = GetCache();
            return cache != null && cache.Count > 0;
        }

        public void ClearCache()
        {
            var filePaths = new[] { ProtectedFile.GetCacheFile(_cacheFileName), ProtectedFile.GetCacheFile(_recentFileName) };
            foreach (var filePath in filePaths.Where(File.Exists))
            {
                File.Delete(filePath);
            }
        }
    }
}
