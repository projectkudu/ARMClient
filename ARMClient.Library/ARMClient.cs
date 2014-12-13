using System;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARMClient.Authentication;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;

namespace ARMClient.Library
{
    public class ARMClient : DynamicObject
    {
        private string _url = "https://management.azure.com";
        private string _authorizationHeader;
        private readonly string _apiVersion;
        private readonly IAuthHelper _authHelper;
        private readonly AzureEnvironments _azureEnvironment;

        public static dynamic GetDynamicClient(string apiVersion, IAuthHelper authHelper = null,
            AzureEnvironments azureEnvironment = AzureEnvironments.Prod, string url = null)
        {
            return new ARMClient(apiVersion, authHelper, azureEnvironment, url);
        }

        public static dynamic GetDynamicClient(string apiVersion, string authorizationHeader, string url = null)
        {
            return new ARMClient(apiVersion, authorizationHeader, url);
        }

        private ARMClient(string apiVersion, IAuthHelper authHelper = null,
            AzureEnvironments azureEnvironment = AzureEnvironments.Prod, string url = null)
        {
            this._apiVersion = apiVersion;
            this._authHelper = authHelper ?? new AuthHelper(azureEnvironment);
            this._azureEnvironment = azureEnvironment;
            this._url = url ?? "https://management.azure.com";
        }

        private ARMClient(string apiVersion, string authorizationHeader, string url = null)
        {
            this._authorizationHeader = authorizationHeader;
            this._url = url ?? "https://management.azure.com";
        }

        public async Task InitializeToken(string subscriptionId = null)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                var match = Regex.Match(_url, ".*\\/subscriptions\\/(.*?)\\/", RegexOptions.IgnoreCase);
                subscriptionId = match.Success ? match.Groups[1].ToString() : string.Empty;
            }

            if (!this._authHelper.IsCacheValid())
            {
                await this._authHelper.AcquireTokens().ConfigureAwait(false);
            }

            this._authorizationHeader = await this._authHelper.GetAuthorizationHeader(subscriptionId).ConfigureAwait(false);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryHandleDynamicCall(binder.Name, out result);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            return TryHandleDynamicCall(indexes[0].ToString(), out result);
        }

        private bool TryHandleDynamicCall(string name, out object result)
        {
            var url = string.Format("{0}/{1}", _url, name);

            result = string.IsNullOrEmpty(this._authorizationHeader)
                ? new ARMClient(this._apiVersion, this._authHelper, this._azureEnvironment, url)
                : new ARMClient(this._apiVersion, this._authorizationHeader, url);

            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            this._url = string.Format("{0}?api-version={1}", this._url, this._apiVersion);
            var isAsync = binder.Name.EndsWith("async", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(_authorizationHeader))
            {
                if (isAsync)
                {
                    throw new InvalidDataException(
                        "To call an Async methond you need to either pass in an AuthorizationHeader or call InitializeToken before");
                }
                else
                {
                    InitializeToken().Wait();
                }
            }

            Func<string, string> methodName = s => string.Format("{0}{1}", s, isAsync ? "async" : string.Empty);

            if ((String.Equals(binder.Name, methodName("get"), StringComparison.OrdinalIgnoreCase)
                 || String.Equals(binder.Name, methodName("delete"), StringComparison.OrdinalIgnoreCase)
                 || String.Equals(binder.Name, methodName("put"), StringComparison.OrdinalIgnoreCase)
                 || String.Equals(binder.Name, methodName("post"), StringComparison.OrdinalIgnoreCase)))
            {
                var verb = isAsync
                    ? binder.Name.Substring(0, binder.Name.IndexOf("async", StringComparison.OrdinalIgnoreCase))
                    : binder.Name;
                var task = HttpInvoke(new Uri(this._url), verb, args.Length > 0 ? args[0].ToString() : string.Empty);
                if (isAsync)
                {
                    result = task;
                }
                else
                {
                    result = task.Result;
                }
            }
            else
            {
                throw new InvalidOperationException(string.Format("Method {0} doesn't exist", binder.Name));
            }

            return true;
        }

        private async Task<HttpResponseMessage> HttpInvoke(Uri uri, string verb, string payload)
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Add("Authorization", this._authorizationHeader);
                client.DefaultRequestHeaders.Add("User-Agent", "CSMClient-" + Environment.MachineName);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                HttpResponseMessage response = null;
                if (String.Equals(verb, "get", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.GetAsync(uri).ConfigureAwait(false);
                }
                else if (String.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.DeleteAsync(uri).ConfigureAwait(false);
                }
                else if (String.Equals(verb, "post", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PostAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                }
                else if (String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PutAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Invalid http verb {0}!", verb));
                }

                return response;
            }
        }
    }
}