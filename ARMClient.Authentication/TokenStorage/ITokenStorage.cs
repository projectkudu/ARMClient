using ARMClient.Authentication.Contracts;

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
