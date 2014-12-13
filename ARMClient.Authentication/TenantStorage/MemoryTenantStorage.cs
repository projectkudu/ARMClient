using System.Collections.Generic;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.TenantStorage
{
    class MemoryTenantStorage : ITenantStorage
    {
        private Dictionary<string, TenantCacheInfo> _cache;
        public void SaveCache(Dictionary<string, TenantCacheInfo> tenants)
        {
            this._cache = tenants;
        }

        public Dictionary<string, TenantCacheInfo> GetCache()
        {
            return this._cache ?? new Dictionary<string, TenantCacheInfo>();
        }

        public bool IsCacheValid()
        {
            return this._cache != null && this._cache.Count > 0;
        }

        public void ClearCache()
        {
            if (this._cache != null)
                this._cache.Clear();
        }
    }
}
