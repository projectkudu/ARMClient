using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            var csmClient = ARMClient.GetDynamicClient(apiVersion: "2014-04-01", authHelper: new PersistentAuthHelper(AzureEnvironments.Prod));

            var sitesResponse = (HttpResponseMessage)csmClient.Subscriptions["{subscriptionName}"].ResourceGroups["{resourceGroupName}"].Providers["Microsoft.Web"].Sites.Get();

            if (sitesResponse.IsSuccessStatusCode)
            {
                var sites = sitesResponse.Content.ReadAsAsync<JArray>().Result;

                Func<object, bool> p = s => s.ToString().Equals("West US", StringComparison.OrdinalIgnoreCase);

                foreach (dynamic site in sites.Where(t => p(t["location"])))
                {
                    Console.WriteLine(site.name);
                }
            }
            else
            {
                Console.WriteLine(sitesResponse.Content.ReadAsStringAsync().Result);
            }
        }
    }
}
