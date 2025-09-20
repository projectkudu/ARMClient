
namespace ARMClient.Authentication.Contracts
{
    public class TenantDetails
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public VerifiedDomain[] verifiedDomains { get; set; }
    }
}
