using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;

namespace ARMClient.Authentication.AADAuthentication
{
    public class PersistentAuthHelper : BaseAuthHelper, IAuthHelper
    {
        public PersistentAuthHelper(AzureEnvironments azureEnvironment = AzureEnvironments.Prod)
            : base(azureEnvironment, new FileTokenStorage(), new FileTenantStorage(), new FileEnvironmentStorage())
        {
        }
    }
}
