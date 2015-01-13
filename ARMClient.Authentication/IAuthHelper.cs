using System.Collections.Generic;
using System.Threading.Tasks;
using ARMClient.Authentication.Contracts;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication
{
    public interface IAuthHelper
    {
        AzureEnvironments AzureEnvironments { get; set; }
        Task AcquireTokens();
        Task<AuthenticationResult> GetTokenByTenant(string tenantId);
        Task<AuthenticationResult> GetTokenBySubscription(string subscriptionId);
        Task<AuthenticationResult> GetTokenBySpn(string tenantId, string appId, string appKey);
        Task<AuthenticationResult> GetRecentToken();
        Task<string> GetAuthorizationHeader(string subscriptionId);
        bool IsCacheValid();
        void ClearTokenCache();
        IEnumerable<string> DumpTokenCache();
    }
}
