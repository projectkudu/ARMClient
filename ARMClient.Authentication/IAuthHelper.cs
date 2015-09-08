using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication
{
    public interface IAuthHelper
    {
        AzureEnvironments AzureEnvironments { get; set; }
        Task AcquireTokens();
        Task<TokenCacheInfo> GetToken(string id);
        Task<TokenCacheInfo> GetTokenBySpn(string tenantId, string appId, string appKey);
        Task<TokenCacheInfo> GetTokenByUpn(string username, string password);
        bool IsCacheValid();
        void ClearTokenCache();
        IEnumerable<string> DumpTokenCache();
    }
}
