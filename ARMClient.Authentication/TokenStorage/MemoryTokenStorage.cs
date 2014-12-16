using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.TokenStorage
{
    internal class MemoryTokenStorage : ITokenStorage
    {
        private Dictionary<TokenCacheKey, string> _cache;
        private AuthenticationResult _recentToken;
        public Dictionary<TokenCacheKey, string> GetCache()
        {
            return this._cache ?? new Dictionary<TokenCacheKey, string>();
        }

        public void SaveCache(Dictionary<TokenCacheKey, string> cache)
        {
            this._cache = cache;
        }

        public AuthenticationResult GetRecentToken()
        {
            return this._recentToken;
        }

        public void SaveRecentToken(AuthenticationResult authResult)
        {
            this._recentToken = authResult;
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
