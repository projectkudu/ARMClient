using System;

namespace ARMClient.Authentication.Contracts
{
    public class AzAccessToken
    {
        public string accessToken { get; set; }
        public DateTimeOffset expiresOn { get; set; }
        public string subscription { get; set; }
        public string tenant { get; set; }
        public string tokentype { get; set; }
    }
}
