using System.Collections.Generic;
using ARMClient.Authentication.Contracts;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.TokenStorage
{
    public interface ITokenStorage
    {
        CustomTokenCache GetCache();
        void SaveCache(CustomTokenCache tokenCache);

        TokenCacheInfo GetRecentToken(string resource);
        void SaveRecentToken(TokenCacheInfo cacheInfo, string resource);

        bool IsCacheValid();
        void ClearCache();
    }
}
