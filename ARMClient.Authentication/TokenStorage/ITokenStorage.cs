using System.Collections.Generic;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.TokenStorage
{
    public interface ITokenStorage
    {
        Dictionary<TokenCacheKey, string> GetCache();
        void SaveCache(Dictionary<TokenCacheKey, string> tokens);
        AuthenticationResult GetRecentToken();
        void SaveRecentToken(AuthenticationResult authResult);
        bool IsCacheValid();
        void ClearCache();
    }
}
