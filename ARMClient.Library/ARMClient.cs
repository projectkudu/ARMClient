using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARMClient.Authentication;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Contracts;
using Microsoft.CSharp.RuntimeBinder;

namespace ARMClient.Library
{
    public class ARMClient : DynamicObject
    {
        private string _url = null;
        private string _authorizationHeader;
        private readonly string _apiVersion;
        private readonly IAuthHelper _authHelper;
        private readonly AzureEnvironments _azureEnvironment;

        public static dynamic GetDynamicClient(string apiVersion, IAuthHelper authHelper = null,
            AzureEnvironments azureEnvironment = AzureEnvironments.Prod, string url = null)
        {
            return new ARMClient(apiVersion, authHelper, azureEnvironment, url);
        }

        private ARMClient(string apiVersion, IAuthHelper authHelper = null,
            AzureEnvironments azureEnvironment = AzureEnvironments.Prod, string url = null)
        {
            this._apiVersion = apiVersion;
            this._authHelper = authHelper ?? new AuthHelper(azureEnvironment);
            this._azureEnvironment = azureEnvironment;
            this._url = url ?? Constants.CSMUrls[(int)azureEnvironment];
        }

        private ARMClient(string apiVersion, string authorizationHeader, string url)
        {
            this._authorizationHeader = authorizationHeader;
            this._url = url;
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

            TokenCacheInfo cacheInfo = await this._authHelper.GetToken(subscriptionId, Constants.CSMResource).ConfigureAwait(false);
            this._authorizationHeader = cacheInfo.CreateAuthorizationHeader();
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


        public T Get<T>()
        {
            return Task.Run(() => GetAsync<T>()).Result;
        }

        public async Task<T> GetAsync<T>()
        {
            var httpResponse = await InvokeAsyncDynamicMember("Get", Enumerable.Empty<object>().ToArray());
            return await httpResponse.Content.ReadAsAsync<T>();
        }

        public T Post<T>(params object[] args)
        {
            return Task.Run(() => PostAsync<T>(args)).Result;
        }

        public async Task<T> PostAsync<T>(params object[] args)
        {
            var httpResponse = await InvokeAsyncDynamicMember("Post", args);
            return await httpResponse.Content.ReadAsAsync<T>();
        }

        public T Put<T>(params object[] args)
        {
            return Task.Run(() => PutAsync<T>(args)).Result;
        }

        public async Task<T> PutAsync<T>(params object[] args)
        {
            var httpResponse = await InvokeAsyncDynamicMember("Put", args);
            return await httpResponse.Content.ReadAsAsync<T>();
        }

        public T Delete<T>()
        {
            return Task.Run(() => DeleteAsync<T>()).Result;
        }

        public async Task<T> DeleteAsync<T>()
        {
            var httpResponse = await InvokeAsyncDynamicMember("Delete", Enumerable.Empty<object>().ToArray());
            return await httpResponse.Content.ReadAsAsync<T>();
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = InvokeDynamicMember(binder.Name, args);
            return true;
        }

        private object InvokeDynamicMember(string memberName, object[] args)
        {
            if (string.IsNullOrEmpty(this._authorizationHeader))
            {
                InitializeToken().Wait();
            }

            var isAsync = memberName.EndsWith("async", StringComparison.OrdinalIgnoreCase);
            memberName = isAsync
                ? memberName.Substring(0, memberName.IndexOf("async", StringComparison.OrdinalIgnoreCase))
                : memberName;



            object result = HttpInvoke(memberName, args);

            return isAsync ? result : Task.Run(() => (Task<HttpResponseMessage>)result).Result;
        }

        private async Task<HttpResponseMessage> InvokeAsyncDynamicMember(string memberName, object[] args)
        {
            if (string.IsNullOrEmpty(_authorizationHeader))
            {
                await InitializeToken();
            }

            if ((String.Equals(memberName, "get", StringComparison.OrdinalIgnoreCase)
                 || String.Equals(memberName, "delete", StringComparison.OrdinalIgnoreCase)
                 || String.Equals(memberName, "put", StringComparison.OrdinalIgnoreCase)
                 || String.Equals(memberName, "post", StringComparison.OrdinalIgnoreCase)))
            {
                return await HttpInvoke(memberName, args);
            }
            else
            {
                throw new RuntimeBinderException(string.Format("'ARMClient' does not contain a http definition for '{0}'", memberName));
            }
        }

        private Task<HttpResponseMessage> HttpInvoke(string httpVerb, object[] args)
        {
            var uri = string.Format("{0}?api-version={1}", this._url, this._apiVersion);
            return HttpInvoke(new Uri(uri), httpVerb, args.Length > 0 ? args[0].ToString() : string.Empty);
        }

        private async Task<HttpResponseMessage> HttpInvoke(Uri uri, string verb, string payload)
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Add("Authorization", this._authorizationHeader);
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);
                client.DefaultRequestHeaders.Add("Accept", Constants.JsonContentType);
                client.DefaultRequestHeaders.Add("x-ms-request-id", Guid.NewGuid().ToString());

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
                    response = await client.PostAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, Constants.JsonContentType)).ConfigureAwait(false);
                }
                else if (String.Equals(verb, "put", StringComparison.OrdinalIgnoreCase))
                {
                    response = await client.PutAsync(uri, new StringContent(payload ?? String.Empty, Encoding.UTF8, Constants.JsonContentType)).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException(String.Format("Invalid http verb '{0}'!", verb));
                }

                return response;
            }
        }
    }
}