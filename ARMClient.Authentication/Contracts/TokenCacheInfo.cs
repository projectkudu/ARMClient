using System;
using System.Linq;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.Contracts
{
    public class TokenCacheInfo
    {
        public TokenCacheInfo()
        {
        }

        public TokenCacheInfo(string token)
        {
            AccessToken = token;
            var json = JwtHelper.Parse(token);
            DisplayableId = new[] { "upn", "unique_name", "name" }
                .Select(json.Value<string>)
                .Where(v => !string.IsNullOrEmpty(v))
                .FirstOrDefault();
            Resource = json.Value<string>("aud");
            TenantId = json.Value<string>("tid");
            ExpiresOn = DateTimeOffset.FromUnixTimeSeconds(json.Value<long>("exp"));
            if (json.Value<string>("idtyp") == "app")
            {
                AppId = json.Value<string>("appid");
            }
        }

        public string AppId { get; set; }
        public string AppKey { get; set; }
        public string AccessToken { get; set; }
        public string DisplayableId { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
        public string Resource { get; set; }
        public string TenantId { get; set; }

        public string CreateAuthorizationHeader() => $"Bearer {AccessToken}";
    }
}
