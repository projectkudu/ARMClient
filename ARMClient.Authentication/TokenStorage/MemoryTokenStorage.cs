using System;
using System.Collections.Generic;
using ARMClient.Authentication.Contracts;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.TokenStorage
{
    internal class MemoryTokenStorage : ITokenStorage
    {
        private CustomTokenCache _cache;
        private TokenCacheInfo _recentToken;
        public CustomTokenCache GetCache()
        {
            return this._cache ?? new CustomTokenCache();
        }

        public void SaveCache(CustomTokenCache cache)
        {
            this._cache = cache;
        }

        public TokenCacheInfo GetRecentToken(string resource)
        {
            return this._recentToken;
        }

        public void SaveRecentToken(TokenCacheInfo cacheInfo, string resource)
        {
            this._recentToken = cacheInfo;
        }

        public bool IsCacheValid()
        {
            return this._cache != null && this._cache.Count > 0;
        }

        public void ClearCache()
        {
            if (this._cache != null)
                this._cache.Clear();
        }
    }
}
