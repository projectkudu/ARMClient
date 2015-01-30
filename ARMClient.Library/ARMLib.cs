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
using Newtonsoft.Json;
using System.Diagnostics;

namespace ARMClient.Library
{
    public class ARMLib : DynamicObject
    {
        private string _url = null;
        private TokenCacheInfo _tokenCacheInfo;
        private readonly string _apiVersion;
        private readonly IAuthHelper _authHelper;
        private readonly AzureEnvironments _azureEnvironment;
        private LoginType _loginType;
        private string _tenantId;
        private string _appId;
        private string _appKey;
        private string _userName;
        private string _password;
        private string _query;

        public static dynamic GetDynamicClient(string apiVersion, AzureEnvironments azureEnvironment = AzureEnvironments.Prod, string url = null)
        {
            return new ARMLib(apiVersion, azureEnvironment, url);
        }

        private ARMLib(string apiVersion, AzureEnvironments azureEnvironment, string url)
        {
            this._apiVersion = apiVersion;
            this._authHelper = new AuthHelper(azureEnvironment);
            this._azureEnvironment = azureEnvironment;
            this._url = url ?? Constants.CSMUrls[(int)azureEnvironment];
            this._query = string.Empty;
        }

        private ARMLib(ARMLib oldClient, string url, string query)
        {
            this._loginType = oldClient._loginType;
            this._apiVersion = oldClient._apiVersion;
            this._appId = oldClient._appId;
            this._appKey = oldClient._appKey;
            this._authHelper = oldClient._authHelper;
            this._tokenCacheInfo = oldClient._tokenCacheInfo;
            this._azureEnvironment = oldClient._azureEnvironment;
            this._loginType = oldClient._loginType;
            this._password = oldClient._password;
            this._tenantId = oldClient._tenantId;
            this._userName = oldClient._userName;
            this._query = query;
            this._url = url;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryHandleDynamicCall(binder.Name.FirstLetterToLowerCase(), out result);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            return TryHandleDynamicCall(indexes[0].ToString(), out result);
        }

        private bool TryHandleDynamicCall(string name, out object result)
        {
            var url = string.Format("{0}/{1}", _url, name);
            result = new ARMLib(this, url, this._query);
            return true;
        }

        public async Task<T> GetAsync<T>()
        {
            var httpResponse = await HttpInvoke("Get", Enumerable.Empty<object>().ToArray()).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsAsync<T>().ConfigureAwait(false);
        }

        public async Task<T> PostAsync<T>(params object[] args)
        {
            var httpResponse = await HttpInvoke("Post", args).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsAsync<T>().ConfigureAwait(false);
        }

        public async Task<T> PutAsync<T>(params object[] args)
        {
            var httpResponse = await HttpInvoke("Put", args).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsAsync<T>().ConfigureAwait(false);
        }

        public async Task<T> DeleteAsync<T>()
        {
            var httpResponse = await HttpInvoke("Delete", Enumerable.Empty<object>().ToArray()).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsAsync<T>().ConfigureAwait(false);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (binder.Name.Equals("ConfigureLogin", StringComparison.OrdinalIgnoreCase))
            {
                this._loginType = (LoginType) args[0];
                switch (this._loginType)
                {
                    case LoginType.Spn:
                        this._tenantId = args[1].ToString();
                        this._appId = args[2].ToString();
                        this._appKey = args[3].ToString();
                        break;
                    case LoginType.Upn:
                        this._userName = args[1].ToString();
                        this._password = args[2].ToString();
                        break;
                }
                result = this;
            }
            else if (binder.Name.Equals("Query", StringComparison.OrdinalIgnoreCase))
            {
                var query = this._query + "&" + args[0];
                result = new ARMLib(this, this._url, query);
            }
            else
            {
                result = InvokeDynamicMember(binder.Name, args);
            }

            return true;
        }

        private object InvokeDynamicMember(string memberName, object[] args)
        {
            var isAsync = memberName.EndsWith("async", StringComparison.OrdinalIgnoreCase);
            memberName = isAsync
                ? memberName.Substring(0, memberName.IndexOf("async", StringComparison.OrdinalIgnoreCase))
                : memberName;
            return HttpInvoke(memberName, args);
        }

        private Task<HttpResponseMessage> HttpInvoke(string httpVerb, object[] args)
        {
            var uri = string.Format("{0}?api-version={1}{2}", this._url, this._apiVersion, this._query);
            return HttpInvoke(new Uri(uri), httpVerb, args.Length > 0 ? args[0] : string.Empty);
        }

        private async Task<HttpResponseMessage> HttpInvoke(Uri uri, string verb, object objectPayload)
        {
            var payload = JsonConvert.SerializeObject(objectPayload);
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Add("Authorization", await GetAuthorizationHeader());
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

        private async Task<string> GetAuthorizationHeader()
        {
            var match = Regex.Match(_url, ".*\\/subscriptions\\/(.*?)\\/", RegexOptions.IgnoreCase);
            var subscriptionId = match.Success ? match.Groups[1].ToString() : null;

            if (this._tokenCacheInfo == null || this._tokenCacheInfo.ExpiresOn <= DateTimeOffset.UtcNow)
            {
                switch (this._loginType)
                {
                    case LoginType.Interactive:
                        await this._authHelper.AcquireTokens().ConfigureAwait(false);

                        break;
                    case LoginType.Spn:
                        await this._authHelper.GetTokenBySpn(this._tenantId, this._appId, this._appKey).ConfigureAwait(false);
                        break;
                    case LoginType.Upn:
                        await this._authHelper.GetTokenByUpn(this._userName, this._password).ConfigureAwait(false);
                        break;
                }
                this._tokenCacheInfo = await this._authHelper.GetToken(subscriptionId, Constants.CSMResource).ConfigureAwait(false);
            }
            return this._tokenCacheInfo.CreateAuthorizationHeader();
        }
    }

    internal static class StringExtension
    {
        public static string FirstLetterToLowerCase(this string value)
        {
            return Char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }

    public enum LoginType
    {
        Upn,
        Spn,
        Interactive
    }
}