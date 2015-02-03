using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using System.Collections.Generic;

namespace ArmGuiClient
{
    public class GuiPersistentAuthHelper : PersistentAuthHelper
    {
        public GuiPersistentAuthHelper()
        {
        }

        public Dictionary<string, TenantCacheInfo> GetTenants()
        {
            return this.TenantStorage.GetCache();
        }
    }
}
