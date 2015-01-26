using System;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ARMClient.Authentication.Contracts
{
    public class TokenCacheInfo
    {
        public TokenCacheInfo()
        {
        }

        public TokenCacheInfo(string tenantId, string appId, string appKey, string resource, AuthenticationResult result)
            : this(resource, result)
        {
            AppId = appId;
            AppKey = appKey;
            TenantId = tenantId;
        }

        public TokenCacheInfo(string resource, AuthenticationResult result)
        {
            AccessToken = result.AccessToken;
            DisplayableId = result.UserInfo == null ? null : result.UserInfo.DisplayableId;
            ExpiresOn = result.ExpiresOn;
            RefreshToken = result.RefreshToken;
            Resource = resource;
            TenantId = result.TenantId;
        }

        public string AppId { get; set; }
        public string AppKey { get; set; }
        public string AccessToken { get; set; }
        public string DisplayableId { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
        public string RefreshToken { get; set; }
        public string Resource { get; set; }
        public string TenantId { get; set; }

        public string CreateAuthorizationHeader()
        {
            return String.Format("Bearer {0}", AccessToken);
        }
    }
}
