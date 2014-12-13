using System.Collections.Generic;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.TenantStorage
{
    public interface ITenantStorage
    {
        void SaveCache(Dictionary<string, TenantCacheInfo> tenants);
        Dictionary<string, TenantCacheInfo> GetCache();
        bool IsCacheValid();
        void ClearCache();
    }
}
