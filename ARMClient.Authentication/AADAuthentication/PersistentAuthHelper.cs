using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;

namespace ARMClient.Authentication.AADAuthentication
{
    public class PersistentAuthHelper : BaseAuthHelper, IAuthHelper
    {
        public PersistentAuthHelper()
            : base(new FileTokenStorage(), new FileTenantStorage(), new FileEnvironmentStorage())
        {
        }
    }
}
