using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace ARMClient.Authentication.Utilities
{
    public static class Utils
    {
        static TraceListener _traceListener;

        public static TraceListener Trace
        {
            get { return _traceListener ?? DefaultTraceListener.Default; }
        }

        public static AzureEnvironments GetDefaultEnv()
        {
            AzureEnvironments env;
            return Enum.TryParse<AzureEnvironments>(Environment.GetEnvironmentVariable("ARMCLIENT_ENV"), true, out env) ? env : AzureEnvironments.Prod;
        }

        public static string GetDefaultCachePath()
        {
            return Environment.GetEnvironmentVariable("ARMCLIENT_CACHEPATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".arm");
        }

        public static bool GetDefaultVerbose()
        {
            return Environment.GetEnvironmentVariable("ARMCLIENT_VERBOSE") == "1";
        }

        public static void SetTraceListener(TraceListener listener)
        {
            _traceListener = listener;
        }

        class DefaultTraceListener : TraceListener
        {
            public readonly static TraceListener Default = new DefaultTraceListener();

            public override void Write(string message)
            {
                System.Diagnostics.Trace.Write(message);
            }

            public override void WriteLine(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }
        }

        public static async Task<int> HttpInvoke(Uri uri, TokenCacheInfo cacheInfo, string verb, DelegatingHandler handler, HttpContent content)
        {
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("Authorization", cacheInfo.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);
                client.DefaultRequestHeaders.Add("Accept", Constants.JsonContentType);

                if (Utils.IsRdfe(uri))
                {
                    client.DefaultRequestHeaders.Add("x-ms-version", "2013-10-01");
                }

                client.DefaultRequestHeaders.Add("x-ms-request-id", Guid.NewGuid().ToString());

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
                else if (String.Equals(verb, "patch", StringComparison.OrdinalIgnoreCase))
                {
                    using (var message = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
                    {
                        message.Content = content;
                        response = await client.SendAsync(message).ConfigureAwait(false);
                    }
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

        public static Uri EnsureAbsoluteUri(string path, PersistentAuthHelper persistentAuthHelper)
        {
            Uri ret;
            if (Uri.TryCreate(path, UriKind.Absolute, out ret))
            {
                return ret;
            }

            var env = persistentAuthHelper.IsCacheValid() ? persistentAuthHelper.AzureEnvironments : AzureEnvironments.Prod;
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

        public static bool IsRdfe(Uri uri)
        {
            var host = uri.Host;
            return Constants.RdfeUrls.Any(url => url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0);
        }

        public static bool IsGraphApi(Uri uri)
        {
            var host = uri.Host;
            return Constants.AADGraphUrls.Any(url => url.IndexOf(host, StringComparison.OrdinalIgnoreCase) > 0);
        }
    }
}
