using System;
using System.IO;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.TokenStorage
{
    internal class FileTokenStorage : ITokenStorage
    {
        private const string _cacheFileName = "cache_tokens.dat";

        public CustomTokenCache GetCache()
        {
            var file = ProtectedFile.GetCacheFile(_cacheFileName);
            if (!File.Exists(file))
            {
                return new CustomTokenCache();
            }

            var state = ProtectedFile.ReadAllText(file);
            return new CustomTokenCache(state);
        }

        public void SaveCache(CustomTokenCache cache)
        {
            var state = cache.GetState();
            ProtectedFile.WriteAllText(ProtectedFile.GetCacheFile(_cacheFileName), state);
        }

        public TokenCacheInfo GetRecentToken(string resource)
        {
            var file = ProtectedFile.GetCacheFile(GetRecentTokenFileName(resource));
            if (!File.Exists(file))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<TokenCacheInfo>(ProtectedFile.ReadAllText(file));
        }

        public void SaveRecentToken(TokenCacheInfo cacheInfo, string resource)
        {
            var file = ProtectedFile.GetCacheFile(GetRecentTokenFileName(resource));
            var json = JObject.FromObject(cacheInfo);
            ProtectedFile.WriteAllText(ProtectedFile.GetCacheFile(file), json.ToString());
        }

        public bool IsCacheValid()
        {
            var file = ProtectedFile.GetCacheFile(_cacheFileName);
            return File.Exists(file);
        }

        public void ClearCache()
        {
            foreach (var filePath in Directory.GetFiles(Utils.GetDefaultCachePath(), "*token*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(filePath);
            }
        }

        private string GetRecentTokenFileName(string resource)
        {
            var uri = new Uri(resource);
            return String.Format("recent_token_{0}.dat", uri.Host);
        }
    }
}
