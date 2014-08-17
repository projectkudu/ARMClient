using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AADHelpers;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace CSMClient
{
    class Program
    {
        static string[] CSMUrls = new[]
        {
            "https://api-next.resources.windows-int.net",
            "https://api-current.resources.windows-int.net",
            "https://api-dogfood.resources.windows-int.net",
            "https://management.azure.com"
        };

        public const string CSMApiVersion = "2014-01-01";

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    if (String.Equals(args[0], "login", StringComparison.OrdinalIgnoreCase))
                    {
                        AzureEnvs env = AzureEnvs.Prod;
                        if (args.Length > 1)
                        {
                            env = (AzureEnvs)Enum.Parse(typeof(AzureEnvs), args[1], ignoreCase: true);
                        }
                        TokenUtils.AcquireToken(env).Wait();
                        return 0;
                    }
                    else if (String.Equals(args[0], "listcache", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length > 1)
                        {
                            AzureEnvs env = (AzureEnvs)Enum.Parse(typeof(AzureEnvs), args[1], ignoreCase: true);
                            TokenUtils.DumpTokenCache(env);
                        }
                        else
                        {
                            foreach (AzureEnvs env in Enum.GetValues(typeof(AzureEnvs)))
                            {
                                Console.WriteLine("Env: {0}", env);
                                TokenUtils.DumpTokenCache(env);
                                Console.WriteLine();
                            }
                        }
                        return 0;
                    }
                    else if (String.Equals(args[0], "clearcache", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length > 1)
                        {
                            AzureEnvs env = (AzureEnvs)Enum.Parse(typeof(AzureEnvs), args[1], ignoreCase: true);
                            TokenUtils.ClearTokenCache(env);
                        }
                        else
                        {
                            foreach (AzureEnvs env in Enum.GetValues(typeof(AzureEnvs)))
                            {
                                TokenUtils.ClearTokenCache(env);
                            }
                        }
                        return 0;
                    }
                    else if (String.Equals(args[0], "token", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length >= 2)
                        {
                            string tenantId = Guid.Parse(args[1]).ToString();
                            string user = args.Length >= 3 ? args[2] : null;
                            var authResult = TokenUtils.GetTokenByTenant(tenantId, user).Result;
                            var bearer = authResult.CreateAuthorizationHeader();
                            Clipboard.SetText(bearer);
                            Console.WriteLine(bearer);
                            Console.WriteLine();
                            Console.WriteLine("Token copied to clipboard successfully.");
                            return 0;
                        }
                    }
                    else if (String.Equals(args[0], "get", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "delete", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "post", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length >= 2)
                        {
                            var verb = args[0];
                            var uri = new Uri(args[1]);
                            var env = GetAzureEnvs(uri);
                            var subscriptionId = GetSubscription(uri);
                            string user = args.Length >= 3 ? args[2] : null;
                            var authResult = TokenUtils.GetTokenBySubscription(env, subscriptionId, user).Result;
                            HttpInvoke(uri, authResult, verb).Wait();
                            return 0;
                        }
                    }
                }

                PrintUsage();
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("CSMClient supports getting token and simple Http CSM resources.");

            Console.WriteLine();
            Console.WriteLine("Login and get tokens");
            Console.WriteLine("    CSMClient.exe login ([Prod|Current|Dogfood|Next])");

            Console.WriteLine();
            Console.WriteLine("Call CSM api");
            Console.WriteLine("    CSMClient.exe [get|post|put|delete] [url] ([user])");

            Console.WriteLine();
            Console.WriteLine("Copy token to clipboard");
            Console.WriteLine("    CSMClient.exe token [tenant] ([user])");

            Console.WriteLine();
            Console.WriteLine("List token cache");
            Console.WriteLine("    CSMClient.exe listcache ([Prod|Current|Dogfood|Next])");

            Console.WriteLine();
            Console.WriteLine("Clear token cache");
            Console.WriteLine("    CSMClient.exe clearcache ([Prod|Current|Dogfood|Next])");
        }

        static async Task HttpInvoke(Uri uri, AuthenticationResult authResult, string verb)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", "CSMClient");
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                HttpResponseMessage response = null;
                if (String.Equals(verb, "get", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.GetAsync(uri);
                }
                else if (String.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.DeleteAsync(uri);
                }
                else if (String.Equals(verb, "post", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PostAsync(uri, new StringContent(String.Empty, Encoding.UTF8, "application/json"));
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Invalid http verb {0}!", verb));
                }

                Console.WriteLine("HttpStatus: {0}", response.StatusCode);
                foreach (var header in response.Headers)
                {
                    Console.WriteLine("{0}: {1}", header.Key, String.Join("; ", header.Value));
                }

                var content = response.Content.ReadAsStringAsync().Result.Trim();
                if (content.StartsWith("["))
                {
                    Console.WriteLine(JArray.Parse(content));
                }
                else if (content.StartsWith("{"))
                {
                    Console.WriteLine(JObject.Parse(content));
                }
                else
                {
                    Console.WriteLine(content);
                }
            }
        }

        static AzureEnvs GetAzureEnvs(Uri uri)
        {
            if (uri.Host == "api-next.resources.windows-int.net")
            {
                return AzureEnvs.Next;
            }
            else if (uri.Host == "api-current.resources.windows-int.net")
            {
                return AzureEnvs.Current;
            }
            else if (uri.Host == "api-dogfood.resources.windows-int.net")
            {
                return AzureEnvs.Dogfood;
            }
            else if (uri.Host == "management.azure.com")
            {
                return AzureEnvs.Prod;
            }

            throw new InvalidOperationException(String.Format("Invalid CSM host {0}!", uri.Host));
        }

        static string GetSubscription(Uri uri)
        {
            var paths = uri.PathAndQuery.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (paths.Length >= 2 && paths[0] == "subscriptions")
            {
                return Guid.Parse(paths[1]).ToString();
            }

            throw new InvalidOperationException(String.Format("Invalid url {0}!", uri));
        }
    }
}