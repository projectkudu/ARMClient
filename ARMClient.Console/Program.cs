using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace ARMClient
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            try
            {
                var persistentAuthHelper = new PersistentAuthHelper();
                if (args.Length > 0)
                {
                    if (String.Equals(args[0], "login", StringComparison.OrdinalIgnoreCase))
                    {
                        var env = AzureEnvironments.Prod;
                        if (args.Length > 1)
                        {
                            env = (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), args[1], ignoreCase: true);
                        }
                        persistentAuthHelper.SetEnvironment(env);
                        persistentAuthHelper.AcquireTokens().Wait();
                        return 0;
                    }
                    else if (String.Equals(args[0], "listcache", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var line in persistentAuthHelper.DumpTokenCache())
                        {
                            Console.WriteLine(line);
                        }
                        return 0;
                    }
                    else if (String.Equals(args[0], "clearcache", StringComparison.OrdinalIgnoreCase))
                    {
                        persistentAuthHelper.ClearTokenCache();
                        return 0;
                    }
                    else if (String.Equals(args[0], "token", StringComparison.OrdinalIgnoreCase))
                    {
                        AuthenticationResult authResult;
                        if (args.Length >= 2)
                        {
                            string tenantId = Guid.Parse(args[1]).ToString();
                            string user = null;
                            AzureEnvironments? env = null;
                            if (args.Length >= 3)
                            {
                                user = args[2].Contains("@") ? args[2] : null;
                                env = user == null ? (AzureEnvironments?)Enum.Parse(typeof(AzureEnvironments), args[2], true) : null;

                                if (args.Length >= 4)
                                {
                                    env = env ?? (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), args[3], true);
                                    user = user ?? args[3];
                                }
                            }

                            authResult = persistentAuthHelper.GetTokenByTenant(tenantId).Result;
                        }
                        else
                        {
                            authResult = persistentAuthHelper.GetRecentToken().Result;
                        }

                        var bearer = authResult.CreateAuthorizationHeader();
                        Clipboard.SetText(bearer);
                        Console.WriteLine(bearer);
                        Console.WriteLine();
                        DumpClaims(authResult.AccessToken);
                        Console.WriteLine();
                        Console.WriteLine("Expires: " + authResult.ExpiresOn.ToLocalTime().ToString("o"));
                        Console.WriteLine();
                        Console.WriteLine("Token copied to clipboard successfully.");
                        return 0;
                    }
                    else if (String.Equals(args[0], "spn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length >= 4)
                        {
                            AzureEnvironments env = AzureEnvironments.Prod;
                            if (args.Length >= 5)
                            {
                                env = (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), args[4], ignoreCase: true);
                            }

                            string tenantId = Guid.Parse(args[1]).ToString();
                            string appId = Guid.Parse(args[2]).ToString();
                            string appKey = args[3];
                            persistentAuthHelper.SetEnvironment(env);
                            var authResult = persistentAuthHelper.GetTokenBySpn(tenantId, appId, appKey).Result;
                            return 0;
                        }
                    }
                    else if (String.Equals(args[0], "get", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "delete", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "put", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "post", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length >= 2)
                        {
                            Dictionary<string, string> parameters;
                            args = ParseArguments(args, out parameters);
                            var addOutputColor = !parameters.ContainsKey("-nocolor");
                            var verb = args[0];
                            var uri = new Uri(args[1]);

                            var subscriptionId = GetSubscription(uri);
                            AuthenticationResult authResult;
                            if (String.IsNullOrEmpty(subscriptionId))
                            {
                                authResult = persistentAuthHelper.GetRecentToken().Result;
                            }
                            else
                            {
                                authResult = persistentAuthHelper.GetTokenBySubscription(subscriptionId).Result;
                            }

                            string content = null;
                            string file = null;
                            if (parameters.TryGetValue("-content", out file))
                            {
                                content = File.ReadAllText(file);
                            }
                            HttpInvoke(uri, authResult, verb, addOutputColor, content).Wait();
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

        static string[] ParseArguments(string[] args, out Dictionary<string, string> parameters)
        {
            parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var ret = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    var index = arg.IndexOf(':');
                    if (index < 0)
                    {
                        parameters.Add(arg, String.Empty);
                    }
                    else
                    {
                        parameters.Add(arg.Substring(0, index), arg.Substring(index + 1));
                    }
                }
                else
                {
                    ret.Add(arg);
                }
            }

            return ret.ToArray();
        }

        static void DumpClaims(string accessToken)
        {
            var base64 = accessToken.Split('.')[1];

            // fixup
            int mod4 = base64.Length % 4;
            if (mod4 > 0)
            {
                base64 += new string('=', 4 - mod4);
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            PrintColoredJson(JObject.Parse(json));
            Console.WriteLine();
        }

        static void PrintUsage()
        {
            Console.WriteLine("ARMClient supports getting token and simple Http ARM resources.");
            Console.WriteLine("Source codes are available at https://github.com/projectkudu/ARMClient.");

            Console.WriteLine();
            Console.WriteLine("Login and get tokens");
            Console.WriteLine("    ARMClient.exe login");

            Console.WriteLine();
            Console.WriteLine("Call ARM api");
            Console.WriteLine("    ARMClient.exe [get|post|put|delete] [url] (-content:<file>)");

            Console.WriteLine();
            Console.WriteLine("Copy token to clipboard");
            Console.WriteLine("    ARMClient.exe token [tenant|subscription]");

            Console.WriteLine();
            Console.WriteLine("Get token by ServicePrincipalName");
            Console.WriteLine("    ARMClient.exe spn [tenant] [appId] [appKey]");

            Console.WriteLine();
            Console.WriteLine("List token cache");
            Console.WriteLine("    ARMClient.exe listcache");

            Console.WriteLine();
            Console.WriteLine("Clear token cache");
            Console.WriteLine("    ARMClient.exe clearcache");
        }

        static async Task HttpInvoke(Uri uri, AuthenticationResult authResult, string verb, bool addOutputColor, string payload)
        {
            using (var client = new HttpClient(new HttpLoggingHandler(new HttpClientHandler(), addOutputColor)))
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", "ARMClient-" + Environment.MachineName);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                if (IsRdfe(uri))
                {
                    client.DefaultRequestHeaders.Add("x-ms-version", "2013-10-01");
                }

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
                    response = await client.PostAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, "application/json"));
                }
                else if (String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PutAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, "application/json"));
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Invalid http verb {0}!", verb));
                }

                response.Dispose();
            }
        }

        //http://stackoverflow.com/questions/4810841/how-can-i-pretty-print-json-using-javascript
        public static void PrintColoredJson(JContainer json)
        {
            const string jsonPatterns =
                @"(\s*""(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\""])*""(\s*:)?|\s*\b(true|false|null)\b|\s*-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?|\s*[\[\{\]\},]|\s*\n)";
            const ConsoleColor keyColor = ConsoleColor.DarkGreen;
            const ConsoleColor numbersColor = ConsoleColor.Cyan;
            const ConsoleColor stringColor = ConsoleColor.DarkYellow;
            const ConsoleColor booleanColor = ConsoleColor.DarkCyan;
            const ConsoleColor nullColor = ConsoleColor.DarkMagenta;

            var originalColor = Console.ForegroundColor;

            try
            {

                var regex = new Regex(jsonPatterns, RegexOptions.None);

                foreach (Match match in regex.Matches(json.ToString()))
                {
                    if (match.Success)
                    {
                        var value = match.Groups[1].Value;
                        var currentColor = numbersColor;
                        if (Regex.IsMatch(value, "^\\s*\""))
                        {
                            currentColor = Regex.IsMatch(value, ":$") ? keyColor : stringColor;
                        }
                        else if (Regex.IsMatch(value, "true|false"))
                        {
                            currentColor = booleanColor;
                        }
                        else if (Regex.IsMatch(value, "null"))
                        {
                            currentColor = nullColor;
                        }
                        else if (Regex.IsMatch(value, @"[\[\{\]\},]"))
                        {
                            currentColor = originalColor;
                        }

                        Console.ForegroundColor = currentColor;
                        Console.Write(value);
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        static bool IsRdfe(Uri uri)
        {
            return uri.Host == "umapinext.rdfetest.dnsdemo4.com"
                || uri.Host == "umapi.rdfetest.dnsdemo4.com"
                || uri.Host == "umapi-preview.core.windows-int.net"
                || uri.Host == "management.core.windows.net";
        }

        static bool IsGraphApi(Uri uri)
        {
            return uri.Host == "graph.ppe.windows.net"
                || uri.Host == "graph.windows.net";
        }

        static string GetSubscription(Uri uri)
        {
            try
            {
                if (IsGraphApi(uri))
                {
                    return null;
                }

                var paths = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (paths.Length >= 2 && paths[0] == "subscriptions")
                {
                    return Guid.Parse(paths[1]).ToString();
                }

                Guid subscription;
                if (Guid.TryParse(paths[0], out subscription))
                {
                    return subscription.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(String.Format("Invalid url {0}!", uri), ex);
            }
        }
    }
}