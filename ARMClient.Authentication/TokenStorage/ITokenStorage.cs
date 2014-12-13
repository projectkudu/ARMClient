using System.Collections.Generic;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.TokenStorage
{
    public interface ITokenStorage
    {
        Dictionary<TokenCacheKey, string> GetCache();
        bool TryGetRecentToken(out AuthenticationResult recentToken);
        void SaveCache(Dictionary<TokenCacheKey, string> tokens);
        void SaveRecentToken(AuthenticationResult authResult);
        bool IsCacheValid();
        void ClearCache();
    }
}
