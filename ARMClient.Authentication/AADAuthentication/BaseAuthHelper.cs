using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.AADAuthentication
{
    public abstract class BaseAuthHelper : IAuthHelper
    {
        protected readonly ITokenStorage TokenStorage;
        protected readonly ITenantStorage TenantStorage;
        protected readonly IEnvironmentStorage EnvironmentStorage;

        private ARMConfiguration _configuration;

        protected BaseAuthHelper(ITokenStorage tokenStorage,
            ITenantStorage tenantStorage, IEnvironmentStorage environmentStorage)
        {
            this.EnvironmentStorage = environmentStorage;
            this.TokenStorage = tokenStorage;
            this.TenantStorage = tenantStorage;
        }

        public ARMConfiguration ARMConfiguration
        {
            get { return _configuration ?? (_configuration = new ARMConfiguration(this.EnvironmentStorage.GetSavedEnvironment())); }
        }

        public void SetAzureEnvironment(string env)
        {
            if (Uri.TryCreate(env, UriKind.Absolute, out var aadLoginUrl))
            {
                _configuration = new ARMConfiguration(aadLoginUrl);
            }
            else
            {
                _configuration = new ARMConfiguration(env);
            }

            this.EnvironmentStorage.SaveEnvironment(_configuration.AzureEnvironment);
        }

        public async Task AcquireTokens()
        {
            this.TokenStorage.ClearCache();
            this.TenantStorage.ClearCache();

            var tokenCache = new CustomTokenCache();
            var cacheInfo = await GetAuthorizationResult(tokenCache, Utils.GetLoginTenant());
            Utils.Trace.WriteLine(string.Format("Welcome {0} (Tenant: {1})", cacheInfo.DisplayableId, cacheInfo.TenantId));

            var tenantCache = await GetTokenForTenants(tokenCache, cacheInfo);

            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);
        }

        public async Task AzLogin()
        {
            this.TokenStorage.ClearCache();
            this.TenantStorage.ClearCache();

            var tokenCache = new CustomTokenCache();
            var tenantCache = this.TenantStorage.GetCache();
            TokenCacheInfo recentInfo = null;
            var token = AzAuthHelper.GetToken(ARMConfiguration.Current.ARMResource);
            var result = new TokenCacheInfo(token.accessToken);
            tokenCache.Add(result);

            foreach (var resource in new[] { ARMConfiguration.Current.AppServiceUrl, ARMConfiguration.Current.AADMSGraphUrl, ARMConfiguration.Current.KeyVaultResource })
            {
                try
                {
                    tokenCache.Add(new TokenCacheInfo(AzAuthHelper.GetToken(resource).accessToken));
                }
                catch (Exception)
                {
                    // best effort
                }
            }

            var tenantId = result.TenantId;
            var info = new TenantCacheInfo
            {
                tenantId = tenantId,
                displayName = "unknown",
                domain = tenantId
            };

            Utils.Trace.WriteLine(string.Format("User: {0}, Tenant: {1}", result.DisplayableId, tenantId));
            try
            {
                var subscriptions = await GetSubscriptions(result);
                Utils.Trace.WriteLine(string.Format("\tThere are {0} subscriptions", subscriptions.Length));

                info.subscriptions = subscriptions.Select(subscription => new SubscriptionCacheInfo
                {
                    subscriptionId = subscription.subscriptionId,
                    displayName = subscription.displayName
                }).ToArray();

                if (recentInfo == null || info.subscriptions.Length > 0)
                {
                    recentInfo = result;
                }

                foreach (var subscription in subscriptions)
                {
                    Utils.Trace.WriteLine(string.Format("\tSubscription {0} ({1})", subscription.subscriptionId, subscription.displayName));
                }
            }
            catch (Exception ex)
            {
                Utils.Trace.WriteLine(string.Format("\t{0}!", ex.Message));
            }

            tenantCache[tenantId] = info;
            if (!String.IsNullOrEmpty(info.domain) && info.domain != "unknown")
            {
                tenantCache[info.domain] = info;
            }

            Utils.Trace.WriteLine(string.Empty);

            if (recentInfo != null)
            {
                this.TokenStorage.SaveRecentToken(recentInfo, ARMConfiguration.ARMResource);
            }

            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);
        }

        public async Task<TokenCacheInfo> GetTokenByResource(string resource)
        {
            var cacheInfo = await GetRecentToken(resource);
            if (cacheInfo != null)
            {
                return cacheInfo;
            }

            cacheInfo = await GetToken(null, null);
            var tokenCache = TokenStorage.GetCache();
            TokenCacheInfo found;
            if (tokenCache.TryGetValue(cacheInfo.TenantId, resource, out found))
            {
                cacheInfo = found;
            }
            else
            {
                cacheInfo = await GetAuthorizationResult(tokenCache, tenantId: cacheInfo.TenantId, user: cacheInfo.DisplayableId, resource: resource);
                this.TokenStorage.SaveCache(tokenCache);
            }

            this.TokenStorage.SaveRecentToken(cacheInfo, resource);
            return cacheInfo;
        }

        public async Task<TokenCacheInfo> GetToken(string id, string resource)
        {
            try
            {
                return await GetTokenInternal(id, resource);
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("expir") < 0)
                {
                    throw;
                }

                Console.WriteLine(ex.Message);
            }

            await AcquireTokens();

            return await GetTokenInternal(id, resource);
        }

        private async Task<TokenCacheInfo> GetTokenInternal(string id, string resource)
        {
            if (String.IsNullOrEmpty(id))
            {
                return await GetRecentToken(resource ?? ARMConfiguration.ARMResource);
            }

            string tenantId = null;
            var tenantCache = this.TenantStorage.GetCache();
            TenantCacheInfo tenantInfo;
            if (tenantCache.TryGetValue(id, out tenantInfo))
            {
                id = tenantId = tenantInfo.tenantId;
            }

            if (String.IsNullOrEmpty(tenantId))
            {
                foreach (var tenant in tenantCache)
                {
                    if (tenant.Value.subscriptions.Any(s => s.subscriptionId == id))
                    {
                        tenantId = tenant.Key;
                        break;
                    }
                }
            }

            // look up tenant by assuming it is subscription
            if (String.IsNullOrEmpty(tenantId))
            {
                tenantId = await GetTenantIdFromSubscription(id, throwIfNotFound: true);
            }

            if (String.IsNullOrEmpty(tenantId))
            {
                return await GetRecentToken(ARMConfiguration.ARMResource);
            }

            if (string.IsNullOrEmpty(resource))
            {
                resource = id == tenantId ? ARMConfiguration.AADMSGraphUrl : ARMConfiguration.ARMResource;
            }

            var tokenCache = this.TokenStorage.GetCache();
            TokenCacheInfo cacheInfo;
            if (!tokenCache.TryGetValue(tenantId, resource, out cacheInfo))
            {
                cacheInfo = await GetToken(null, null);
                cacheInfo = await GetAuthorizationResult(tokenCache, tenantId: tenantId, user: cacheInfo.DisplayableId, resource: resource);
                this.TokenStorage.SaveCache(tokenCache);
                this.TokenStorage.SaveRecentToken(cacheInfo, resource);
                return cacheInfo;
            }

            if (cacheInfo.ExpiresOn <= DateTimeOffset.UtcNow)
            {
                cacheInfo = await RefreshToken(tokenCache, cacheInfo);
                this.TokenStorage.SaveCache(tokenCache);
            }

            this.TokenStorage.SaveRecentToken(cacheInfo, resource);

            var armResource = ARMConfiguration.ARMResource;
            if (resource != armResource)
            {
                TokenCacheInfo armInfo;
                if (tokenCache.TryGetValue(tenantId, armResource, out armInfo))
                {
                    this.TokenStorage.SaveRecentToken(armInfo, armResource);
                }
            }

            return cacheInfo;
        }

        private async Task<string> GetTenantIdFromSubscription(string subscriptionId, bool throwIfNotFound = true)
        {
            using (var client = new HttpClient())
            {
                var serviceUrl = ARMConfiguration.ARMUrl;
                string requestUri = String.Format("{0}/subscriptions/{1}?api-version=2014-04-01", serviceUrl.Trim('/'), subscriptionId);
                using (var response = await client.GetAsync(requestUri))
                {
                    if (response.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        if (!throwIfNotFound && response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return null;
                        }

                        throw new InvalidOperationException(String.Format("Expected Status {0} != {1} GET {2}", HttpStatusCode.Unauthorized, response.StatusCode, requestUri));
                    }

                    var header = response.Headers.WwwAuthenticate.SingleOrDefault();
                    if (header == null || String.IsNullOrEmpty(header.Parameter))
                    {
                        throw new InvalidOperationException(String.Format("Missing WWW-Authenticate response header GET {0}", requestUri));
                    }

                    // WWW-Authenticate: Bearer authorization_uri="https://login.windows.net/<tenantid>", error="invalid_token", error_description="The access token is missing or invalid."
                    var index = header.Parameter.IndexOf("authorization_uri=", StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                    {
                        throw new InvalidOperationException(String.Format("Invalid WWW-Authenticat response header {0} GET {1}", header.Parameter, requestUri));
                    }

                    var parts = header.Parameter.Substring(index).Split(new[] { '\"', '=' }, StringSplitOptions.RemoveEmptyEntries);
                    return new Uri(parts[1]).AbsolutePath.Trim('/');
                }
            }
        }

        public async Task<TokenCacheInfo> GetTokenBySpn(string tenantId, string appId, string appKey, string resource)
        {
            this.TokenStorage.ClearCache();
            this.TenantStorage.ClearCache();

            var tokenCache = new CustomTokenCache();
            var cacheInfo = await GetAuthorizationResultBySpn(tokenCache, tenantId, appId, appKey, resource ?? ARMConfiguration.ARMResource);

            var tenantCache = await GetTokenForTenants(tokenCache, cacheInfo, appId: appId, appKey: appKey);

            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);

            return cacheInfo;
        }

        public async Task<TokenCacheInfo> GetTokenBySpn(string tenantId, string appId, X509Certificate2 certificate, string resource)
        {
            this.TokenStorage.ClearCache();
            this.TenantStorage.ClearCache();

            var tokenCache = new CustomTokenCache();
            var cacheInfo = await GetAuthorizationResultBySpn(tokenCache, tenantId, appId, certificate, resource ?? ARMConfiguration.ARMResource);

            if (cacheInfo.Resource != ARMConfiguration.ARMResource)
            {
                cacheInfo = await GetAuthorizationResultBySpn(tokenCache, tenantId, appId, certificate, ARMConfiguration.ARMResource);
            }

            var tenantCache = await GetTokenForTenants(tokenCache, cacheInfo, appId: appId, appKey: "_certificate_");

            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);

            return cacheInfo;
        }

        protected async Task<TokenCacheInfo> GetRecentToken(string resource)
        {
            TokenCacheInfo cacheInfo = this.TokenStorage.GetRecentToken(resource);
            if (cacheInfo != null && cacheInfo.ExpiresOn <= DateTimeOffset.UtcNow)
            {
                var tokenCache = this.TokenStorage.GetCache();
                cacheInfo = await RefreshToken(tokenCache, cacheInfo);
                this.TokenStorage.SaveCache(tokenCache);
                this.TokenStorage.SaveRecentToken(cacheInfo, resource);
            }

            return cacheInfo;
        }

        protected async Task<TokenCacheInfo> RefreshToken(CustomTokenCache tokenCache, TokenCacheInfo cacheInfo)
        {
            if (!String.IsNullOrEmpty(cacheInfo.AppId) && cacheInfo.AppKey == "_certificate_")
            {
                throw new InvalidOperationException("Unable to refresh expired token!  Try login with certificate again.");
            }
            else if (!String.IsNullOrEmpty(cacheInfo.AppId) && !String.IsNullOrEmpty(cacheInfo.AppKey))
            {
                return await GetAuthorizationResultBySpn(tokenCache, cacheInfo.TenantId, cacheInfo.AppId, cacheInfo.AppKey, cacheInfo.Resource);
            }

            tokenCache.Remove(cacheInfo);
            return await GetAuthorizationResult(tokenCache, cacheInfo.TenantId, null, cacheInfo.Resource);
        }

        public bool IsCacheValid()
        {
            return this.EnvironmentStorage.IsCacheValid() && this.TokenStorage.IsCacheValid() && this.TenantStorage.IsCacheValid();
        }

        public void ClearTokenCache()
        {
            this.TokenStorage.ClearCache();
            this.TenantStorage.ClearCache();
            this.EnvironmentStorage.ClearSavedEnvironment();
        }

        public IEnumerable<string> DumpTokenCache()
        {
            var tokenCache = this.TokenStorage.GetCache();
            var tenantCache = this.TenantStorage.GetCache();
            foreach (var cacheItem in tokenCache.GetValues(ARMConfiguration.ARMResource))
            {
                var tenantId = cacheItem.TenantId;
                var details = tenantCache[tenantId];
                if (!String.IsNullOrEmpty(cacheItem.DisplayableId))
                {
                    yield return string.Format("User: {0}, Tenant: {1} ({2})", cacheItem.DisplayableId, tenantId, details.domain);
                }
                else if (!String.IsNullOrEmpty(cacheItem.AppId))
                {
                    yield return string.Format(String.IsNullOrEmpty(details.domain) ? "App: {0}, Tenant: {1}" : "App: {0}, Tenant: {1} ({2})", cacheItem.AppId, tenantId, details.domain);
                }
                else
                {
                    throw new NotImplementedException();
                }

                var subscriptions = details.subscriptions;
                yield return string.Format("\tThere are {0} subscriptions", subscriptions.Length);

                foreach (var subscription in subscriptions)
                {
                    yield return string.Format("\tSubscription {0} ({1})", subscription.subscriptionId, subscription.displayName);
                }
                yield return string.Empty;
            }
        }

        protected async Task<TokenCacheInfo> GetAuthorizationResult(CustomTokenCache tokenCache, string tenantId, string user = null, string resource = null)
        {
            var tcs = new TaskCompletionSource<TokenCacheInfo>();

            resource = resource ?? ARMConfiguration.ARMResource;

            TokenCacheInfo found;
            if (tokenCache.TryGetValue(tenantId, resource, out found))
            {
                return found;
            }

            var result = await MsalAuthHelper.GetUserTokenAsync(authorityHost: ARMConfiguration.AADLoginUrl, tenantId: tenantId, scope: resource);
            var cacheInfo = new TokenCacheInfo(result.AccessToken);
            tokenCache.Add(cacheInfo);
            return cacheInfo;
        }

        protected async Task<TokenCacheInfo> GetAuthorizationResultBySpn(CustomTokenCache tokenCache, string tenantId, string appId, string appKey, string resource)
        {
            TokenCacheInfo found;
            if (tokenCache.TryGetValue(tenantId, resource, out found))
            {
                return found;
            }

            var result = await MsalAuthHelper.GetClientSecretTokenAsync(authorityHost: ARMConfiguration.AADLoginUrl, tenantId: tenantId, scope: resource, clientId: appId, clientSecret: appKey);
            var cacheInfo = new TokenCacheInfo(result.AccessToken) { AppKey = appKey };
            tokenCache.Add(cacheInfo);
            return cacheInfo;
        }

        protected async Task<TokenCacheInfo> GetAuthorizationResultBySpn(CustomTokenCache tokenCache, string tenantId, string appId, X509Certificate2 certificate, string resource)
        {
            TokenCacheInfo found;
            if (tokenCache.TryGetValue(tenantId, resource, out found))
            {
                return found;
            }

            var result = await MsalAuthHelper.GetClientCertificateTokenAsync(authorityHost: ARMConfiguration.AADLoginUrl, tenantId: tenantId, scope: resource, clientId: appId, clientCertificate: certificate);
            var cacheInfo = new TokenCacheInfo(result.AccessToken) { AppKey = "_certificate_" };
            tokenCache.Add(cacheInfo);
            return cacheInfo;
        }

        protected async Task<Dictionary<string, TenantCacheInfo>> GetTokenForTenants(CustomTokenCache tokenCache, TokenCacheInfo cacheInfo,
            string appId = null, string appKey = null)
        {
            var recentInfo = cacheInfo;
            var tenantIds = await GetTenantIds(cacheInfo);
            if (!tenantIds.Contains(cacheInfo.TenantId))
            {
                var list = tenantIds.ToList();
                list.Insert(0, cacheInfo.TenantId);
                tenantIds = list.ToArray();
            }

            var tenantCache = this.TenantStorage.GetCache();
            foreach (var tenantId in tenantIds)
            {
                var info = new TenantCacheInfo
                {
                    tenantId = tenantId,
                    displayName = "unknown",
                    domain = tenantId
                };

                TokenCacheInfo result = null;
                try
                {
                    if (!String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appKey))
                    {
                        result = await GetAuthorizationResultBySpn(tokenCache, tenantId: tenantId, appId: appId, appKey: appKey, resource: ARMConfiguration.ARMResource);
                    }
                    else
                    {
                        result = await GetAuthorizationResult(tokenCache, tenantId: tenantId, user: cacheInfo.DisplayableId);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Trace.WriteLine(string.Format("User: {0}, Tenant: {1} {2}", cacheInfo.DisplayableId, tenantId, ex.Message));
                    Utils.Trace.WriteLine(string.Empty);
                    continue;
                }

                try
                {
                    TokenCacheInfo aadToken = null;
                    if (!String.IsNullOrEmpty(appId) && appKey == "_certificate_")
                    {
                        Utils.Trace.WriteLine(string.Format("AppId: {0}, Tenant: {1}", appId, tenantId));
                    }
                    else if (!String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appKey))
                    {
                        aadToken = await GetAuthorizationResultBySpn(tokenCache, tenantId: tenantId, appId: appId, appKey: appKey, resource: ARMConfiguration.AADMSGraphUrl);
                    }
                    else
                    {
                        aadToken = await GetAuthorizationResult(tokenCache, tenantId: tenantId, user: cacheInfo.DisplayableId, resource: ARMConfiguration.AADMSGraphUrl);
                    }

                    if (aadToken != null)
                    {
                        var details = await GetTenantDetail(aadToken, tenantId);
                        info.displayName = details.displayName;
                        info.domain = details.verifiedDomains.First(d => d.isDefault).name;

                        if (!String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appKey))
                        {
                            Utils.Trace.WriteLine(string.Format("AppId: {0}, Tenant: {1} ({2})", appId, tenantId, info.domain));
                        }
                        else
                        {
                            Utils.Trace.WriteLine(string.Format("User: {0}, Tenant: {1} ({2})", result.DisplayableId, tenantId, info.domain));
                        }
                    }
                }
                catch (Exception)
                {
                    if (!String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appKey))
                    {
                        Utils.Trace.WriteLine(string.Format("AppId: {0}, Tenant: {1}", appId, tenantId));
                    }
                    else
                    {
                        Utils.Trace.WriteLine(string.Format("User: {0}, Tenant: {1}", result.DisplayableId, tenantId));
                    }
                }

                try
                {
                    var subscriptions = await GetSubscriptions(result);
                    Utils.Trace.WriteLine(string.Format("\tThere are {0} subscriptions", subscriptions.Length));

                    info.subscriptions = subscriptions.Select(subscription => new SubscriptionCacheInfo
                    {
                        subscriptionId = subscription.subscriptionId,
                        displayName = subscription.displayName
                    }).ToArray();

                    if (recentInfo != null && info.subscriptions.Length > 0)
                    {
                        recentInfo = result;
                    }

                    foreach (var subscription in subscriptions)
                    {
                        Utils.Trace.WriteLine(string.Format("\tSubscription {0} ({1})", subscription.subscriptionId, subscription.displayName));
                    }
                }
                catch (Exception ex)
                {
                    Utils.Trace.WriteLine(string.Format("\t{0}!", ex.Message));
                }

                tenantCache[tenantId] = info;
                if (!String.IsNullOrEmpty(info.domain) && info.domain != "unknown")
                {
                    tenantCache[info.domain] = info;
                }

                Utils.Trace.WriteLine(string.Empty);
            }

            this.TokenStorage.SaveRecentToken(recentInfo, ARMConfiguration.ARMResource);

            return tenantCache;
        }

        private async Task<string[]> GetTenantIds(TokenCacheInfo cacheInfo)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", cacheInfo.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);

                var url = string.Format("{0}/tenants?api-version={1}", ARMConfiguration.ARMUrl, Constants.CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<TenantInfo>>();

                        if (Guid.TryParse(Utils.GetLoginTenant(), out var loginTenant))
                        {
                            return result.value.Select(tenant => tenant.tenantId).Where(tid => tid == $"{loginTenant}").ToArray();
                        }

                        return result.value.Select(tenant => tenant.tenantId).ToArray();
                    }

                    throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
                }
            }
        }

        private async Task<TenantDetails> GetTenantDetail(TokenCacheInfo cacheInfo, string tenantId)
        {
            if (Constants.InfrastructureTenantIds.Contains(tenantId))
            {
                return new TenantDetails
                {
                    id = tenantId,
                    displayName = "Infrastructure",
                    verifiedDomains = new[]
                    {
                        new VerifiedDomain
                        {
                            name = "live.com",
                            isDefault = true
                        }
                    }
                };
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", cacheInfo.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);

                var url = $"{ARMConfiguration.Current.AADMSGraphUrl}/v1.0/organization";
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<TenantDetails>>();
                        return result.value[0];
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    if (!Utils.GetDefaultVerbose() && content.StartsWith("{"))
                    {
                        var error = (JObject)JObject.Parse(content)["odata.error"];
                        if (error != null)
                        {
                            throw new InvalidOperationException(String.Format("GetTenantDetail {0}, {1}", response.StatusCode, error["message"].Value<string>("value")));
                        }
                    }

                    throw new InvalidOperationException($"GetTenantDetail {url}, {response.StatusCode}, {content}");
                }
            }
        }

        private async Task<SubscriptionInfo[]> GetSubscriptions(TokenCacheInfo cacheInfo)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", cacheInfo.CreateAuthorizationHeader());
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);

                var url = string.Format("{0}/subscriptions?api-version={1}", ARMConfiguration.ARMUrl, Constants.CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<SubscriptionInfo>>();
                        return result.value;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    if (!Utils.GetDefaultVerbose() && content.StartsWith("{"))
                    {
                        var error = (JObject)JObject.Parse(content)["error"];
                        if (error != null)
                        {
                            throw new InvalidOperationException(String.Format("GetSubscriptions {0}, {1}", response.StatusCode, error.Value<string>("message")));
                        }
                    }

                    throw new InvalidOperationException($"GetSubscriptions {url}, {response.StatusCode}, {content}");
                }
            }
        }
    }
}
