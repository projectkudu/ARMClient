using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ARMClient.Authentication;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Utils.SetTraceListener(new ConsoleTraceListener());
            try
            {
                var persistentAuthHelper = new PersistentAuthHelper();
                persistentAuthHelper.AzureEnvironments = AzureEnvironments.Prod;
                if (args.Length > 0)
                {
                    var _parameters = new CommandLineParameters(args);
                    var verb = _parameters.Get(0, "verb");
                    if (String.Equals(verb, "login", StringComparison.OrdinalIgnoreCase))
                    {
                        _parameters.ThrowIfUnknown();
                        persistentAuthHelper.AcquireTokens().Wait();
                        return 0;
                    }
                    else if (String.Equals(verb, "spn", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenantId = _parameters.Get(1, keyName: "tenant");
                        var appId = _parameters.Get(2, keyName: "appId");
                        EnsureGuidFormat(appId);

                        X509Certificate2 certificate = null;
                        var appKey = _parameters.Get(3, keyName: "appKey", requires: false);
                        if (appKey == null)
                        {
                            appKey = PromptForPassword("appKey");
                        }
                        else
                        {
                            if (File.Exists(appKey))
                            {
                                var password = _parameters.Get(4, keyName: "password", requires: false);
                                if (password == null)
                                {
                                    password = PromptForPassword("password");
                                }

                                certificate = new X509Certificate2(appKey, password);
                            }
                        }

                        if (certificate == null)
                        {
                            appKey = Utils.EnsureBase64Key(appKey);
                        }

                        _parameters.ThrowIfUnknown();

                        persistentAuthHelper.AzureEnvironments = Utils.GetDefaultEnv();
                        var info = certificate != null ?
                            AADHelper.AcquireTokenByX509(tenantId, appId, certificate).Result :
                            AADHelper.AcquireTokenBySPN(tenantId, appId, appKey).Result;
                        Clipboard.SetText(info.access_token);
                        DumpClaims(info.access_token);
                        Console.WriteLine();
                        Console.WriteLine("Token copied to clipboard successfully.");
                        return 0;
                    }
                    else if (String.Equals(verb, "get-tenant", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        _parameters.ThrowIfUnknown();

                        var path = String.Format("/{0}/tenantDetails?api-version=1.6", tenant);
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "get-tenant", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        _parameters.ThrowIfUnknown();

                        var path = String.Format("/{0}/tenantDetails/{0}?api-version=1.6", tenant);
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "get-apps", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        _parameters.ThrowIfUnknown();

                        var path = String.Format("/{0}/applications?api-version=1.6", tenant);
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    // https://azure.microsoft.com/en-us/documentation/articles/resource-group-authenticate-service-principal/
                    // https://github.com/Azure-Samples/active-directory-dotnet-graphapi-console/blob/master/GraphConsoleAppV3/Program.cs
                    else if (String.Equals(verb, "add-app", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var app = _parameters.Get(2, keyName: "app");
                        _parameters.ThrowIfUnknown();

                        Guid unused;
                        var isGuid = Guid.TryParse(app, out unused);

                        var path = isGuid ? String.Format("/{0}/applications?$filter=appId eq '{1}'&api-version=1.6", tenant, app)
                            : String.Format("/{0}/applications?$filter=displayName eq '{1}'&api-version=1.6", tenant, app);

                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "get-app", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var app = _parameters.Get(2, keyName: "app");
                        _parameters.ThrowIfUnknown();

                        Guid unused;
                        var isGuid = Guid.TryParse(app, out unused);

                        var path = isGuid ? String.Format("/{0}/applications?$filter=appId eq '{1}'&api-version=1.6", tenant, app)
                            : String.Format("/{0}/applications?$filter=displayName eq '{1}'&api-version=1.6", tenant, app);

                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    // https://msdn.microsoft.com/library/azure/ad/graph/api/entity-and-complex-type-reference#serviceprincipalentity
                    else if (String.Equals(verb, "get-spns", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var app = _parameters.Get(2, keyName: "app");
                        _parameters.ThrowIfUnknown();

                        Guid appGuid = new Guid(app);
                        var path = String.Format("/{0}/applications/{1}/serviceprincipal?api-version=1.6", tenant, appGuid);

                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "add-cred", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var app = _parameters.Get(2, keyName: "app");
                        X509Certificate2 certificate = null;
                        var appKey = _parameters.Get(3, keyName: "appKey", requires: false);
                        if (appKey == null)
                        {
                            appKey = PromptForPassword("appKey");
                        }
                        else
                        {
                            if (File.Exists(appKey))
                            {
                                certificate = new X509Certificate2(appKey);
                                if (certificate.HasPrivateKey)
                                {
                                    throw new Exception("Certificate must not contain private key!");
                                }
                            }
                        }

                        if (certificate == null)
                        {
                            appKey = Utils.EnsureBase64Key(appKey);
                        }

                        _parameters.ThrowIfUnknown();

                        var appObject = GetAppObject(persistentAuthHelper, tenant, app).Result;
                        var appObjectId = GetAppObjectId(appObject);
                        HttpContent content;
                        if (certificate != null)
                        {
                            content = GetPatchContent(appObject, certificate);
                        }
                        else
                        {
                            content = GetPatchContent(appObject, appKey);
                        }

                        var path = String.Format("/{0}/directoryObjects/{1}/Microsoft.DirectoryServices.Application?api-version=1.6", tenant, appObjectId);

                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;

                        return HttpInvoke(uri, cacheInfo, "patch", Utils.GetDefaultVerbose(), content).Result;
                    }
                    else if (String.Equals(verb, "del-cred", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var app = _parameters.Get(2, keyName: "app");
                        var keyId = _parameters.Get(3, keyName: "keyId");
                        EnsureGuidFormat(keyId);
                        _parameters.ThrowIfUnknown();

                        var appObject = GetAppObject(persistentAuthHelper, tenant, app).Result;
                        var appObjectId = GetAppObjectId(appObject);
                        var content = GetRemoveContent(appObject, keyId);
                        var path = String.Format("/{0}/directoryObjects/{1}/Microsoft.DirectoryServices.Application?api-version=1.6", tenant, appObjectId);

                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;

                        return HttpInvoke(uri, cacheInfo, "patch", Utils.GetDefaultVerbose(), content).Result;
                    }
                    else if (String.Equals(verb, "get-users", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        _parameters.ThrowIfUnknown();

                        var path = String.Format("/{0}/users?api-version=1.6", tenant);
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "get-user", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var user = _parameters.Get(2, keyName: "user");
                        _parameters.ThrowIfUnknown();

                        var path = String.Format("/{0}/users/{1}?api-version=1.6", tenant, user);
                        if ((user.StartsWith("1") || user.StartsWith("0")) && user.Length == 16)
                        {
                            path = String.Format("/{0}/users?api-version=1.2-internal&$filter=netId eq '{1}' or alternativeSecurityIds/any(x:x/type eq 1 and x/identityProvider eq null and x/key eq X'{1}')", tenant, user);
                        }
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        return HttpInvoke(uri, cacheInfo, "get", Utils.GetDefaultVerbose(), null).Result;
                    }
                    else if (String.Equals(verb, "get-groups", StringComparison.OrdinalIgnoreCase))
                    {
                        var tenant = _parameters.Get(1, keyName: "tenant");
                        var user = _parameters.Get(2, keyName: "user");
                        _parameters.ThrowIfUnknown();


                        var path = String.Format("/{0}/users/{1}/getMemberGroups?api-version=1.6", tenant, user);
                        var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

                        var subscriptionId = GetTenantOrSubscription(uri);
                        TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;
                        var content = new StringContent("{\"securityEnabledOnly\": false}", Encoding.UTF8, "application/json");
                        return HttpInvoke(uri, cacheInfo, "post", Utils.GetDefaultVerbose(), content).Result;
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

        static async Task<JObject> GetAppObject(PersistentAuthHelper persistentAuthHelper, string tenant, string app)
        {
            Guid unused;
            var isGuid = Guid.TryParse(app, out unused);

            var path = isGuid ? String.Format("/{0}/applications?$filter=appId eq '{1}'&api-version=1.6", tenant, app)
                : String.Format("/{0}/applications?$filter=displayName eq '{1}'&api-version=1.6", tenant, app);

            var uri = EnsureAbsoluteUri(path, persistentAuthHelper);

            var subscriptionId = GetTenantOrSubscription(uri);
            TokenCacheInfo cacheInfo = persistentAuthHelper.GetToken(subscriptionId).Result;

            var json = await Utils.HttpGet(uri, cacheInfo);
            var apps = json.Value<JArray>("value");
            if (apps.Count != 1)
            {
                throw new Exception("Invalid application!");
            }

            return (JObject)apps[0];
        }

        static string GetAppObjectId(JObject appObject)
        {
            return appObject.Value<string>("objectId");
        }

        static HttpContent GetRemoveContent(JObject appObject, string keyId)
        {
            var creds = GetPasswordCredentials(appObject);
            var updates = new JArray(creds.Where(cred => !String.Equals(cred.Value<string>("keyId"), keyId, StringComparison.OrdinalIgnoreCase)));
            if (updates.Count < creds.Count)
            {
                var json = new JObject();
                json["odata.type"] = "Microsoft.DirectoryServices.Application";
                json["passwordCredentials@odata.type"] = "Collection(Microsoft.DirectoryServices.PasswordCredential)";
                json["passwordCredentials"] = updates;
                return new StringContent(json.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            }

            creds = GetKeyCredentials(appObject);
            updates = new JArray(creds.Where(cred => !String.Equals(cred.Value<string>("keyId"), keyId, StringComparison.OrdinalIgnoreCase)));
            if (updates.Count < creds.Count)
            {
                var json = new JObject();
                json["odata.type"] = "Microsoft.DirectoryServices.Application";
                json["keyCredentials@odata.type"] = "Collection(Microsoft.DirectoryServices.KeyCredential)";
                json["keyCredentials"] = updates;
                return new StringContent(json.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            }

            throw new Exception("Cannot find key with " + keyId + " id");
        }

        static HttpContent GetPatchContent(JObject appObject, string appKey)
        {
            var cred = new JObject();
            cred["startDate"] = DateTime.UtcNow.ToString("o");
            cred["endDate"] = DateTime.UtcNow.AddYears(1).ToString("o");
            cred["value"] = appKey;

            var creds = GetPasswordCredentials(appObject);
            creds.Add(cred);

            var json = new JObject();
            json["odata.type"] = "Microsoft.DirectoryServices.Application";
            json["passwordCredentials@odata.type"] = "Collection(Microsoft.DirectoryServices.PasswordCredential)";
            json["passwordCredentials"] = creds;
            return new StringContent(json.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        }

        static HttpContent GetPatchContent(JObject appObject, X509Certificate2 certificate)
        {
            var cred = new JObject();
            cred["startDate"] = DateTime.UtcNow.ToString("o");
            cred["endDate"] = DateTime.UtcNow.AddYears(1).ToString("o");
            cred["type"] = "AsymmetricX509Cert";
            cred["usage"] = "Verify";
            cred["customKeyIdentifier"] = Convert.ToBase64String(certificate.GetCertHash());
            cred["value"] = Convert.ToBase64String(certificate.GetRawCertData());

            var creds = GetKeyCredentials(appObject);
            creds.Add(cred);

            var json = new JObject();
            json["odata.type"] = "Microsoft.DirectoryServices.Application";
            json["keyCredentials@odata.type"] = "Collection(Microsoft.DirectoryServices.KeyCredential)";
            json["keyCredentials"] = creds;
            return new StringContent(json.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        }

        static JArray GetPasswordCredentials(JObject appObject)
        {
            return appObject.Value<JArray>("passwordCredentials");
        }

        static JArray GetKeyCredentials(JObject appObject)
        {
            return appObject.Value<JArray>("keyCredentials");
        }

        static string PromptForPassword(string title)
        {
            string pass = String.Empty;
            Console.Write("Enter {0}: ", title);
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return pass;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    while (pass.Length > 0)
                    {
                        pass = pass.Remove(pass.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (pass.Length > 0)
                    {
                        pass = pass.Substring(0, pass.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
            }
        }

        static Uri EnsureAbsoluteUri(string path, PersistentAuthHelper persistentAuthHelper)
        {
            Uri ret;
            if (Uri.TryCreate(path, UriKind.Absolute, out ret))
            {
                return ret;
            }

            var env = persistentAuthHelper.IsCacheValid() ? persistentAuthHelper.AzureEnvironments : Utils.GetDefaultEnv();
            var parts = path.Split(new[] { '/', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0
                || String.Equals(parts[0], "tenants", StringComparison.OrdinalIgnoreCase)
                || String.Equals(parts[0], "subscriptions", StringComparison.OrdinalIgnoreCase)
                || String.Equals(parts[0], "providers", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(new Uri(ARMClient.Authentication.Constants.CSMUrls[(int)env]), path);
            }

            Guid guid;
            if (Guid.TryParse(parts[0], out guid))
            {
                if (path.Length > 1 && String.Equals(parts[1], "services", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(new Uri(ARMClient.Authentication.Constants.RdfeUrls[(int)env]), path);
                }
            }

            return new Uri(new Uri(ARMClient.Authentication.Constants.AADGraphUrls[(int)env]), path);
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
            Console.WriteLine(@"AADClient version {0}", typeof(Program).Assembly.GetName().Version);
            Console.WriteLine("A simple tool to invoke the Graph API");
            Console.WriteLine("Source code is available on https://github.com/projectkudu/ARMClient.");

            Console.WriteLine();
            Console.WriteLine("Login and get tokens");
            Console.WriteLine("    AADClient.exe login");

            Console.WriteLine();
            Console.WriteLine("Get Tenant Details");
            Console.WriteLine("    AADClient.exe get-tenant [tenant]");

            Console.WriteLine();
            Console.WriteLine("Get Applications");
            Console.WriteLine("    AADClient.exe get-apps [tenant]");
            Console.WriteLine("    AADClient.exe get-app [tenant] [appId]");

            Console.WriteLine();
            Console.WriteLine("ServicePrincipal Secrets");
            Console.WriteLine("    AADClient.exe add-cred [tenant] [appId] (appKey)");
            Console.WriteLine("    AADClient.exe add-cred [tenant] [appId] [certificate] (password)");
            Console.WriteLine("    AADClient.exe del-cred [tenant] [appId] [keyId]");

            Console.WriteLine();
            Console.WriteLine("Get token by ServicePrincipal");
            Console.WriteLine("    AADClient.exe spn [tenant] [appId] (appKey)");
            Console.WriteLine("    AADClient.exe spn [tenant] [appId] [certificate] (password)");

            Console.WriteLine();
            Console.WriteLine("Get Users");
            Console.WriteLine("    AADClient.exe get-users [tenant]");
            Console.WriteLine("    AADClient.exe get-user [tenant] [puid|altsecid|upn|oid]");

            Console.WriteLine();
            Console.WriteLine("Get Groups");
            Console.WriteLine("    AADClient.exe get-groups [tenant] [upn|oid]");
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

        static async Task<int> HttpInvoke(Uri uri, TokenCacheInfo cacheInfo, string verb, bool verbose, HttpContent content)
        {
            var logginerHandler = new HttpLoggingHandler(new HttpClientHandler(), verbose);
            return await Utils.HttpInvoke(uri, cacheInfo, verb, logginerHandler, content);
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

        static string GetTenantOrSubscription(Uri uri)
        {
            try
            {
                var paths = uri.AbsolutePath.Split(new[] { '/', '?' }, StringSplitOptions.RemoveEmptyEntries);
                if (Utils.IsGraphApi(uri))
                {
                    return paths[0];
                }

                if (paths.Length >= 2 && String.Equals(paths[0], "subscriptions", StringComparison.OrdinalIgnoreCase))
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

        static AzureEnvironments GetAzureEnvironments(Uri uri, PersistentAuthHelper persistentAuthHelper)
        {
            var host = uri.Host;

            var graphs = Constants.AADGraphUrls.Where(url => url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0);
            if (graphs.Count() > 1)
            {
                var env = persistentAuthHelper.AzureEnvironments;
                if (Constants.AADGraphUrls[(int)env].IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return env;
                }

                env = Utils.GetDefaultEnv();
                if (Constants.AADGraphUrls[(int)env].IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return env;
                }
            }

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

            for (int i = 0; i < Constants.VsoSuffixes.Length; ++i)
            {
                var suffix = Constants.VsoSuffixes[i];
                if (host.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return (AzureEnvironments)i;
                }
            }

            return AzureEnvironments.Prod;
        }
    }
}