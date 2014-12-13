using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;

namespace ARMClient.Authentication.AADAuthentication
{
    public class AuthHelper : BaseAuthHelper, IAuthHelper
    {
        public AuthHelper(AzureEnvironments azureEnvironment = AzureEnvironments.Prod)
            : base(azureEnvironment, new MemoryTokenStorage(), new MemoryTenantStorage(), new MemoryEnvironmentStorage()
                )
        {

        }
    }
}
