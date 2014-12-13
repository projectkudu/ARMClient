
namespace ARMClient.Authentication.Contracts
{
    internal class TenantDetails
    {
        public string objectId { get; set; }
        public string displayName { get; set; }
        public VerifiedDomain[] verifiedDomains { get; set; }
    }
}
