
namespace ARMClient.Authentication
{
    public static class Constants
    {
        public static string[] AADLoginUrls = new[]
        {
            "https://login.windows-ppe.net",
            "https://login.windows-ppe.net",
            "https://login.windows-ppe.net",
            "https://login.windows.net"
        };

        public static string[] AADGraphUrls = new[]
        {
            "https://graph.ppe.windows.net",
            "https://graph.ppe.windows.net",
            "https://graph.ppe.windows.net",
            "https://graph.windows.net"
        };

        public static string[] CSMUrls = new[]
        {
            "https://api-next.resources.windows-int.net",
            "https://api-current.resources.windows-int.net",
            "https://api-dogfood.resources.windows-int.net",
            "https://management.azure.com"
        };

        public static string[] InfrastructureTenantIds = new[]
        {
            "ea8a4392-515e-481f-879e-6571ff2a8a36",
            "f8cdef31-a31e-4b4a-93e4-5f571e91255a"
        };

        public const string AADTenantId = "common";
        public const string AADClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public const string AADRedirectUri = "urn:ietf:wg:oauth:2.0:oob";
        public const string CSMApiVersion = "2014-01-01";
        public const string AADGraphApiVersion = "1.5";
    }
}
