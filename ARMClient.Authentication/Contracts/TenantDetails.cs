
namespace ARMClient.Authentication.Contracts
{
    public class TenantDetails
    {
        public string objectId { get; set; }
        public string displayName { get; set; }
        public VerifiedDomain[] verifiedDomains { get; set; }
    }
}
