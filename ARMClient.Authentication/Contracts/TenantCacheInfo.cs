
namespace ARMClient.Authentication.Contracts
{
    public class TenantCacheInfo
    {
        public string tenantId { get; set; }
        public string displayName { get; set; }
        public string domain { get; set; }
        public SubscriptionCacheInfo[] subscriptions { get; set; }
    }
}
