using System;
using System.Diagnostics;
using System.Reflection;

namespace ARMClient.Authentication
{
    public static class Constants
    {
        public static string[] InfrastructureTenantIds = new[]
        {
            "ea8a4392-515e-481f-879e-6571ff2a8a36",
            "f8cdef31-a31e-4b4a-93e4-5f571e91255a"
        };

        public static Lazy<string> FileVersion = new Lazy<string>(() =>
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        });

        public static Lazy<string> UserAgent = new Lazy<string>(() =>
        {
            return "ARMClient/" + FileVersion.Value;
        });

        public const string AADCommonTenant = "common";
        // auxteststagemanual.ccsctp.net
        public const string AADDogfoodTenant = "83abe5cd-bcc3-441a-bd86-e6a75360cecc";
        public const string AADClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public const string AADRedirectUri = "urn:ietf:wg:oauth:2.0:oob";
        public const string CSMApiVersion = "2014-01-01";
        public const string AADGraphApiVersion = "1.5";
        public const string JsonContentType = "application/json";
        public const string XmlContentType = "text/xml";
        public const string ARMProdEnv = "Prod";
        public const string ARMDogfoodEnv = "Dogfood";
    }
}
