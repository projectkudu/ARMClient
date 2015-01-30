using System;
using System.Threading.Tasks;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using Newtonsoft.Json.Linq;

namespace ARMClient.Library.Runner
{
    class Program
    {
        private static void Main(string[] args)
        {
            Run().Wait();
        }

        private static async Task Run()
        {
            var armClient = ARMLib.GetDynamicClient(apiVersion: "2014-11-01").ConfigureLogin(LoginType.Upn, "userName", "password");

            var resrouceGroups = await armClient.Subscriptions["{subscriptionId}"]
                                                .ResourceGroups
                                                .GetAsync<JObject>();

            foreach (var resrouceGroup in resrouceGroups.value)
            {
                var sites = (Site[])await armClient.Subscriptions["{subscriptionId}"]
                                                   .ResourceGroups[resrouceGroup.name]
                                                   .Providers["Microsoft.Web"]
                                                   .Sites
                                                   .GetAsync<Site[]>();

                if (sites.Length == 0)
                {
                }
            }
        }
    }

    public class Site
    {
        public string location { get; set; }
        public string name { get; set; }
    }
}
