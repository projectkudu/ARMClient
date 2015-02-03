using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;

namespace ARMClient.Authentication.AADAuthentication
{
    public class AuthHelper : BaseAuthHelper, IAuthHelper
    {
        public AuthHelper()
            : base(new MemoryTokenStorage(), new MemoryTenantStorage(), new MemoryEnvironmentStorage())
        {
        }
    }
}
