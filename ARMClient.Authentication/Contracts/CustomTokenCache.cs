using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.Contracts
{
    public class CustomTokenCache : TokenCache
    {
        private Dictionary<string, TokenCacheInfo> _caches;

        public CustomTokenCache(string state = null)
        {
            if (state == null)
            {
                _caches = new Dictionary<string, TokenCacheInfo>();
            }
            else
            {
                _caches = JsonConvert.DeserializeObject<Dictionary<string, TokenCacheInfo>>(state);
            }
        }

        public IEnumerable<TokenCacheInfo> GetValues(string resource)
        {
            return _caches.Values.Where(c => c.Resource == resource);
        }

        public string GetState()
        {
            return JObject.FromObject(_caches).ToString();
        }

        public bool TryGetValue(string tenantId, string resource, out TokenCacheInfo cacheInfo)
        {
            return _caches.TryGetValue(GetKey(tenantId, resource), out cacheInfo);
        }

        public TokenCacheInfo Get(string tenantId, string resource)
        {
            return _caches[GetKey(tenantId, resource)];
        }

        public void Add(TokenCacheInfo cacheInfo)
        {
            _caches[GetKey(cacheInfo.TenantId, cacheInfo.Resource)] = cacheInfo;
        }

        public void Clone(CustomTokenCache tokenCache)
        {
            _caches = tokenCache._caches;
        }

        private string GetKey(string tenantId, string resource)
        {
            return String.Format("{0}::{1}", tenantId, resource);
        }
    }
}