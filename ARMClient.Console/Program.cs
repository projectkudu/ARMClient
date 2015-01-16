using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ARMClient.Authentication;
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
                    var _parameters = new CommandLineParameters(args);
                    var verb = _parameters.Get(0, "verb");
                    if (String.Equals(verb, "login", StringComparison.OrdinalIgnoreCase))
                    {
                        var env = _parameters.Get(1, requires: false);
                        _parameters.ThrowIfUnknown();

                        persistentAuthHelper.AzureEnvironments = env == null ? AzureEnvironments.Prod :
                            (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), args[1], ignoreCase: true);
                        persistentAuthHelper.AcquireTokens().Wait();
                        return 0;
                    }
                    else if (String.Equals(verb, "listcache", StringComparison.OrdinalIgnoreCase))
                    {
                        _parameters.ThrowIfUnknown();
                        
                        EnsureTokenCache(persistentAuthHelper);

                        foreach (var line in persistentAuthHelper.DumpTokenCache())
                        {
                            Console.WriteLine(line);
                        }
                        return 0;
                    }
                    else if (String.Equals(verb, "clearcache", StringComparison.OrdinalIgnoreCase))
                    {
                        _parameters.ThrowIfUnknown();
                        
                        persistentAuthHelper.ClearTokenCache();
                        return 0;
                    }
                    else if (String.Equals(verb, "token", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenantId = _parameters.Get(1, requires: false);
                        _parameters.ThrowIfUnknown();

                        EnsureTokenCache(persistentAuthHelper);

                        AuthenticationResult authResult;
                        if (tenantId != null)
                        {
                            EnsureGuidFormat(tenantId);
                            authResult = persistentAuthHelper.GetTokenByTenant(tenantId).Result;
                        }
                        else
                        {
                            authResult = persistentAuthHelper.GetRecentToken().Result;
                        }

                        var bearer = authResult.CreateAuthorizationHeader();
                        Clipboard.SetText(bearer);
                        DumpClaims(authResult.AccessToken);
                        Console.WriteLine();
                        Console.WriteLine("Token copied to clipboard successfully.");
                        return 0;
                    }
                    else if (String.Equals(verb, "spn", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenantId = _parameters.Get(1, keyName: "tenant");
                        EnsureGuidFormat(tenantId);
                        
                        var appId = _parameters.Get(2, keyName: "appId");
                        EnsureGuidFormat(appId);

                        var appKey = _parameters.Get(3, keyName: "appKey");
                        var env = _parameters.Get(1, requires: false);
                        _parameters.ThrowIfUnknown();

                        persistentAuthHelper.AzureEnvironments = env == null ? AzureEnvironments.Prod :
                            (AzureEnvironments)Enum.Parse(typeof(AzureEnvironments), args[1], ignoreCase: true);
                        var authResult = persistentAuthHelper.GetTokenBySpn(tenantId, appId, appKey).Result;
                        return 0;
                    }
                    else if (String.Equals(verb, "get", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(verb, "post", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = _parameters.Get(1, keyName: "url");
                        var verbose = _parameters.Get("-verbose", requires: false) != null;
                        var baseUri = new Uri(ARMClient.Authentication.Constants.CSMUrls[(int)AzureEnvironments.Prod]);
                        var uri = new Uri(baseUri, path);

                        if (!verbose)
                        {
                            Trace.Listeners.Clear();
                        }

                        if (!persistentAuthHelper.IsCacheValid())
                        {
                            persistentAuthHelper.AzureEnvironments = GetAzureEnvironments(uri);
                            persistentAuthHelper.AcquireTokens().Wait();
                        }

                        var env = persistentAuthHelper.AzureEnvironments;
                        baseUri = new Uri(ARMClient.Authentication.Constants.CSMUrls[(int)env]);
                        uri = new Uri(baseUri, path);
                        var content = ParseHttpContent(verb, _parameters);
                        _parameters.ThrowIfUnknown();

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

                        return HttpInvoke(uri, authResult, verb, verbose, content).Result;
                    }
                    else
                    {
                        throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", verb));
                    }
                }

                PrintUsage();
                return 1;
            }
            catch (Exception ex)
            {
                DumpException(ex);
                return -1;
            }
        }

        static void EnsureGuidFormat(string parameter)
        {
            Guid result;
            if (!Guid.TryParse(parameter, out result))
            {
                throw new CommandLineException(String.Format("Parameter '{0}' is not a valid guid!", parameter));
            }
        }

        static void EnsureTokenCache(PersistentAuthHelper persistentAuthHelper)
        {
            if (!persistentAuthHelper.IsCacheValid())
            {
                throw new CommandLineException("There is no login token.  Please login to acquire token.");
            }
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

        static void DumpException(Exception ex)
        {
            if (ex.InnerException != null)
            {
                DumpException(ex.InnerException);
            }

            // Aggregate exceptions themselves don't have interesting messages
            if (!(ex is AggregateException))
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine(@"ARMClient version {0}", typeof(Program).Assembly.GetName().Version);
            Console.WriteLine("A simple tool to invoke the Azure Resource Manager API");
            Console.WriteLine("Source code is available on https://github.com/projectkudu/ARMClient.");

            Console.WriteLine();
            Console.WriteLine("Login and get tokens");
            Console.WriteLine("    ARMClient.exe login");

            Console.WriteLine();
            Console.WriteLine("Call ARM api");
            Console.WriteLine("    ARMClient.exe [get|post|put|delete] [url] (<@file|content>) (-verbose)");

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

        static HttpContent ParseHttpContent(string verb, CommandLineParameters parameters)
        {
            bool requiresData = String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(verb, "patch", StringComparison.OrdinalIgnoreCase);
            bool inputRedirected = Console.IsInputRedirected;

            if (requiresData || String.Equals(verb, "post", StringComparison.OrdinalIgnoreCase))
            {
                string data = parameters.Get("2", "content", requires: requiresData && !inputRedirected);
                if (data == null)
                {
                    if (inputRedirected)
                    {
                        return new StringContent(Console.In.ReadToEnd(), Encoding.UTF8, Constants.JsonContentType);
                    }

                    return new StringContent(String.Empty, Encoding.UTF8, Constants.JsonContentType);
                }

                if (data.StartsWith("@"))
                {
                    data = File.ReadAllText(data.Substring(1));
                }

                return new StringContent(data, Encoding.UTF8, Constants.JsonContentType);
            }
            return null;
        }

        static async Task<int> HttpInvoke(Uri uri, AuthenticationResult authResult, string verb, bool verbose, HttpContent content)
        {
            using (var client = new HttpClient(new HttpLoggingHandler(new HttpClientHandler(), verbose)))
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);
                client.DefaultRequestHeaders.Add("Accept", Constants.JsonContentType);

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
                    response = await client.PostAsync(uri, content);
                }
                else if (String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PutAsync(uri, content);
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Invalid http verb {0}!", verb));
                }

                using (response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return 0;
                    }

                    return (-1) * (int)response.StatusCode;
                }
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
            var host = uri.Host;
            return Constants.RdfeUrls.Any(url => url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0);
        }

        static bool IsGraphApi(Uri uri)
        {
            var host = uri.Host;
            return Constants.AADGraphUrls.Any(url => url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0);
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
                if (paths.Length > 0 && Guid.TryParse(paths[0], out subscription))
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

        static AzureEnvironments GetAzureEnvironments(Uri uri)
        {
            var host = uri.Host;
            for (int i = 0; i < Constants.AADGraphUrls.Length; ++i)
            {
                var url = Constants.AADGraphUrls[i];
                if (url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return (AzureEnvironments)i;
                }
            }

            for (int i = 0; i < Constants.CSMUrls.Length; ++i)
            {
                var url = Constants.CSMUrls[i];
                if (url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return (AzureEnvironments)i;
                }
            }

            for (int i = 0; i < Constants.RdfeUrls.Length; ++i)
            {
                var url = Constants.RdfeUrls[i];
                if (url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return (AzureEnvironments)i;
                }
            }

            for (int i = 0; i < Constants.SCMSuffixes.Length; ++i)
            {
                var suffix = Constants.SCMSuffixes[i];
                if (host.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return (AzureEnvironments)i;
                }
            }

            return AzureEnvironments.Prod;
        }
    }
}