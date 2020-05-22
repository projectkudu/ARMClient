using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication
{
    public interface IAuthHelper
    {
        void SetAzureEnvironment(string env);
        Task AcquireTokens();
        Task AzLogin();
        Task<TokenCacheInfo> GetToken(string id, string resource);
        Task<TokenCacheInfo> GetTokenBySpn(string tenantId, string appId, string appKey, string resource);
        Task<TokenCacheInfo> GetTokenByUpn(string username, string password);
        bool IsCacheValid();
        void ClearTokenCache();
        IEnumerable<string> DumpTokenCache();
    }
}
