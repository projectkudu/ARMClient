using System;
using System.Diagnostics;
using System.Reflection;

namespace ARMClient.Authentication
{
    public static class Constants
    {
        public static string[] AADLoginUrls = new[]
        {
            "https://login.chinacloudapi.cn",
            "https://login.windows-ppe.net",
            "https://login.windows-ppe.net",
            "https://login.windows.net"
        };

        public static string[] AADGraphUrls = new[]
        {
            "https://graph.windows.net",
            "https://graph.ppe.windows.net",
            "https://graph.ppe.windows.net",
            "https://graph.windows.net"
        };

        public static string[] CSMUrls = new[]
        {
            "https://management.chinacloudapi.cn",
            "https://api-current.resources.windows-int.net",
            "https://api-dogfood.resources.windows-int.net",
            "https://management.azure.com"
        };

        public static string[] RdfeUrls = new[]
        {
            "https://management.core.chinacloudapi.cn",
            "https://umapi.rdfetest.dnsdemo4.com",
            "https://umapi-preview.core.windows-int.net",
            "https://management.core.windows.net"
        };

        public static string[] CSMResources = new[]
        {
            "https://management.core.chinacloudapi.cn/",
            "https://management.core.windows.net/",
            "https://management.core.windows.net/",
            "https://management.core.windows.net/"
        };

        public static string[] SCMSuffixes = new[]
        {
            ".scm.chinacloudsites.cn",
            ".scm.antdir0.antares-test.windows-int.net",
            ".waws-ppedf.windows-int.net",
            ".scm.azurewebsites.net"
        };

        public static string[] VsoSuffixes = new[]
        {
            ".notsupport.com",
            ".notsupport.com",
            ".tfsallin.net",
            ".visualstudio.com"
        };


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

        public const string AADTenantId = "common";
        public const string AADClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public const string AADRedirectUri = "urn:ietf:wg:oauth:2.0:oob";
        public const string CSMApiVersion = "2014-01-01";
        public const string AADGraphApiVersion = "1.5";
        public const string JsonContentType = "application/json";
    }
}
