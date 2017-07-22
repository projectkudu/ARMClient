using System;
using System.Linq;

namespace ARMClient.Authentication.Contracts
{
    public class AzAccessToken
    {
        public AzAccessToken()
        {
        }

        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public DateTimeOffset ExpiresOn { get; set; }
        public string Resource { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string Oid { get; set; }
        public string UserId { get; set; }
        public bool isMRRT { get; set; }
        public string _ClientId { get; set; }
        public string _Authority { get; set; }

        public string CreateAuthorizationHeader()
        {
            return String.Format("{0} {0}", TokenType, AccessToken);
        }

        public TokenCacheInfo ToTokenCacheInfo()
        {
            return new TokenCacheInfo
            {
                AccessToken = AccessToken,
                DisplayableId = UserId,
                ExpiresOn = ExpiresOn,
                RefreshToken = RefreshToken,
                Resource = Resource,
                ClientId = _ClientId,
                TenantId = _Authority.Split('/').Last()
            };
        }
    }
}
