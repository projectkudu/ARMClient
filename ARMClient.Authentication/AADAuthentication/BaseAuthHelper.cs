using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ARMClient.Authentication.Contracts;
using ARMClient.Authentication.EnvironmentStorage;
using ARMClient.Authentication.TenantStorage;
using ARMClient.Authentication.TokenStorage;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.AADAuthentication
{
    public abstract class BaseAuthHelper : IAuthHelper
    {
        protected readonly ITokenStorage TokenStorage;
        protected readonly ITenantStorage TenantStorage;
        protected readonly IEnvironmentStorage EnvironmentStorage;
        protected BaseAuthHelper(AzureEnvironments azureEnvironment, ITokenStorage tokenStorage,
            ITenantStorage tenantStorage, IEnvironmentStorage environmentStorage)
        {
            this.EnvironmentStorage = environmentStorage;
            this.TokenStorage = tokenStorage;
            this.TenantStorage = tenantStorage;
        }

        public AzureEnvironments AzureEnvironments
        {
            get { return this.EnvironmentStorage.GetSavedEnvironment(); }
            set { this.EnvironmentStorage.SaveEnvironment(value); }
        }

        public async Task AcquireTokens()
        {
            var tokenCache = new Dictionary<TokenCacheKey, string>();
            
            var authResult = await GetAuthorizationResult(tokenCache, Constants.AADTenantId);
            Trace.WriteLine(string.Format("Welcome {0} (Tenant: {1})", authResult.UserInfo.UserId, authResult.TenantId));

            var tenantCache = await GetTokenForTenants(tokenCache, authResult);

            this.TokenStorage.SaveRecentToken(authResult);
            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);
        }

        public async Task<AuthenticationResult> GetTokenByTenant(string tenantId)
        {
            var found = false;
            var tenantCache = this.TenantStorage.GetCache();
            if (tenantCache.ContainsKey(tenantId))
            {
                found = true;
            }

            if (!found)
            {
                foreach (var tenant in tenantCache)
                {
                    if (tenant.Value.subscriptions.Any(s => s.subscriptionId == tenantId))
                    {
                        tenantId = tenant.Key;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                return await GetRecentToken();
            }

            var tokenCache = this.TokenStorage.GetCache();
            var authResults = tokenCache.Where(p => p.Key.TenantId == tenantId)
                .Select(p => AuthenticationResult.Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(p.Value)))).ToArray();
            if (authResults.Length <= 0)
            {
                return await GetRecentToken();
            }

            if (authResults.Length > 1)
            {
                foreach (var authResult in authResults)
                {
                    Trace.WriteLine(authResult.UserInfo.UserId);
                }

                throw new InvalidOperationException("Multiple users found.  Please specify user argument!");
            }
            else
            {
                var authResult = authResults[0];
                if (authResult.ExpiresOn <= DateTime.UtcNow)
                {
                    authResult = await RefreshToken(tokenCache, authResult);
                    this.TokenStorage.SaveCache(tokenCache);
                }

                this.TokenStorage.SaveRecentToken(authResult);

                return authResult;
            }
        }

        public async Task<AuthenticationResult> GetTokenBySubscription(string subscriptionId)
        {
            var tenantCache = this.TenantStorage.GetCache();
            var pairs = tenantCache.Where(p => p.Value.subscriptions.Any(subscription => subscriptionId == subscription.subscriptionId)).ToArray();
            if (pairs.Length == 0)
            {
                return await GetRecentToken();
            }

            return await GetTokenByTenant(pairs[0].Key);
        }

        public async Task<AuthenticationResult> GetTokenBySpn(string tenantId, string appId, string appKey)
        {
            var tokenCache = new Dictionary<TokenCacheKey, string>();

            var authResult = GetAuthorizationResult(tokenCache, tenantId, appId, appKey);

            var tenantCache = new Dictionary<string, TenantCacheInfo>();
            var info = new TenantCacheInfo
            {
                tenantId = tenantId
            };

            Trace.WriteLine(String.Format("App: {0}, Tenant: {1}", appId, tenantId));

            var subscriptions = await GetSubscriptions(authResult);
            Trace.WriteLine(String.Format("\tThere are {0} subscriptions", subscriptions.Length));

            info.subscriptions = subscriptions.Select(subscription => new SubscriptionCacheInfo
            {
                subscriptionId = subscription.subscriptionId,
                displayName = subscription.displayName
            }).ToArray();

            foreach (var subscription in subscriptions)
            {
                Trace.WriteLine(String.Format("\tSubscription {0} ({1})", subscription.subscriptionId, subscription.displayName));
            }

            tenantCache[tenantId] = info;

            this.TokenStorage.SaveRecentToken(authResult);
            this.TokenStorage.SaveCache(tokenCache);
            this.TenantStorage.SaveCache(tenantCache);

            return authResult;
        }

        public async Task<AuthenticationResult> GetRecentToken()
        {
            AuthenticationResult recentToken = this.TokenStorage.GetRecentToken();
            if (recentToken != null && recentToken.ExpiresOn <= DateTime.UtcNow)
            {
                var tokenCache = this.TokenStorage.GetCache();
                recentToken = await RefreshToken(tokenCache, recentToken);
                this.TokenStorage.SaveCache(tokenCache);
                this.TokenStorage.SaveRecentToken(recentToken);
            }

            return recentToken;
        }

        protected async Task<AuthenticationResult> RefreshToken(Dictionary<TokenCacheKey, string> tokenCache, AuthenticationResult authResult)
        {
            if (!String.IsNullOrEmpty(authResult.RefreshToken))
            {
                authResult = await GetAuthorizationResult(tokenCache, authResult.TenantId, authResult.UserInfo.UserId);
            }
            else if (tokenCache.Count == 1)
            {
                var key = tokenCache.Keys.First();
                string tenantId, appId, appKey;
                GetApplicationInfo(key, out tenantId, out appId, out appKey);

                if (!String.IsNullOrEmpty(tenantId) && !String.IsNullOrEmpty(appId) && !String.IsNullOrEmpty(appKey))
                {
                    tokenCache.Clear();
                    authResult = GetAuthorizationResult(tokenCache, tenantId, appId, appKey);
                }
            }

            return authResult;
        }

        private void SaveApplicationInfo(TokenCacheKey key, string tenantId, string appId, string appKey)
        {
            key.TenantId = tenantId;
            key.ClientId = appId;
            key.UserId = appKey;
        }

        private void GetApplicationInfo(TokenCacheKey key, out string tenantId, out string appId, out string appKey)
        {
            tenantId = key.TenantId;
            appId = key.ClientId;
            appKey = key.UserId;
        }

        public async Task<string> GetAuthorizationHeader(string subscriptionId)
        {
            return (await(string.IsNullOrEmpty(subscriptionId)
                ? GetRecentToken()
                : GetTokenBySubscription(subscriptionId: subscriptionId)).ConfigureAwait(false)).CreateAuthorizationHeader();
        }

        public bool IsCacheValid()
        {
            return this.TokenStorage.IsCacheValid() && this.TenantStorage.IsCacheValid() && this.EnvironmentStorage.IsCacheValid();
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
            if (tokenCache.Count > 0)
            {
                foreach (var item in tokenCache)
                {
                    var key = item.Key;
                    var value = item.Value;
                    var authResult = AuthenticationResult.Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
                    var tenantId = authResult.TenantId ?? key.TenantId;

                    if (Constants.InfrastructureTenantIds.Contains(tenantId))
                    {
                        continue;
                    }

                    var details = tenantCache[tenantId];
                    if (authResult.UserInfo != null)
                    {
                        var user = authResult.UserInfo.UserId;
                        yield return string.Format("User: {0}, Tenant: {1}", user, tenantId);
                        //yield return string.Format("User: {0}, Tenant: {1} {2} ({3})", user, tenantId, details.displayName, details.domain);
                    }
                    else
                    {
                        var appId = key.ClientId;
                        yield return string.Format("App: {0}, Tenant: {1}", appId, tenantId);
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
        }

        protected Task<AuthenticationResult> GetAuthorizationResult(Dictionary<TokenCacheKey, string> tokenCache, string tenantId, string user = null)
        {
            var tcs = new TaskCompletionSource<AuthenticationResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    var azureEnvironment = this.AzureEnvironments;
                    var authority = String.Format("{0}/{1}", Constants.AADLoginUrls[(int)azureEnvironment], tenantId);
                    var context = new AuthenticationContext(
                        authority: authority,
                        validateAuthority: true,
                        tokenCacheStore: tokenCache);

                    AuthenticationResult result = null;
                    if (!string.IsNullOrEmpty(user))
                    {
                        result = context.AcquireToken(
                            resource: "https://management.core.windows.net/",
                            clientId: Constants.AADClientId,
                            redirectUri: new Uri(Constants.AADRedirectUri),
                            userId: null);
                    }
                    else
                    {
                        result = context.AcquireToken(
                            resource: "https://management.core.windows.net/",
                            clientId: Constants.AADClientId,
                            redirectUri: new Uri(Constants.AADRedirectUri),
                            promptBehavior: PromptBehavior.Always);
                    }

                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = "AcquireTokenThread";
            thread.Start();

            return tcs.Task;
        }

        protected AuthenticationResult GetAuthorizationResult(Dictionary<TokenCacheKey, string> tokenCache, string tenantId, string appId, string appKey)
        {
            var azureEnvironment = this.AzureEnvironments;
            var authority = String.Format("{0}/{1}", Constants.AADLoginUrls[(int)azureEnvironment], tenantId);
            var context = new AuthenticationContext(
                authority: authority,
                validateAuthority: true,
                tokenCacheStore: tokenCache);
            var credential = new ClientCredential(appId, appKey);
            var authResult = context.AcquireToken("https://management.core.windows.net/", credential);

            // this will only get us one token, we save AppId and AppKey info in TokenCacheKey
            var key = tokenCache.Keys.First();
            SaveApplicationInfo(key, tenantId, appId, appKey);

            return authResult;
        }

        protected async Task<Dictionary<string, TenantCacheInfo>> GetTokenForTenants(Dictionary<TokenCacheKey, string> tokenCache, AuthenticationResult authResult)
        {
            var tenantIds = await GetTenantIds(authResult);
            Trace.WriteLine(string.Format("User {0} belongs to {1} tenants", authResult.UserInfo.UserId, tenantIds.Length));

            var tenantCache = this.TenantStorage.GetCache();
            foreach (var tenantId in tenantIds)
            {
                var info = new TenantCacheInfo
                {
                    tenantId = tenantId,
                    displayName = "unknown",
                    domain = "unknown"
                };

                AuthenticationResult result = null;
                try
                {
                    result = await GetAuthorizationResult(tokenCache, tenantId: tenantId, user: authResult.UserInfo.UserId);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("User: {0}, Tenant: {1} {2}", authResult.UserInfo.UserId, tenantId, ex.Message));
                    Trace.WriteLine(string.Empty);
                    continue;
                }

                // blocked on Graph API failure
                //try
                //{
                //    var details = await GetTenantDetail(result, tenantId);
                //    info.displayName = details.displayName;
                //    info.domain = details.verifiedDomains.First(d => d.@default).name;
                //    Trace.WriteLine(string.Format("User: {0}, Tenant: {1} {2} ({3})", result.UserInfo.UserId, tenantId, details.displayName, details.verifiedDomains.First(d => d.@default).name));
                //}
                //catch (Exception ex)
                //{
                //    Trace.WriteLine(string.Format("User: {0}, Tenant: {1} {2}", result.UserInfo.UserId, tenantId, ex.Message));
                //}

                Trace.WriteLine(string.Format("User: {0}, Tenant: {1}", result.UserInfo.UserId, tenantId));

                try
                {
                    var subscriptions = await GetSubscriptions(result);
                    Trace.WriteLine(string.Format("\tThere are {0} subscriptions", subscriptions.Length));

                    info.subscriptions = subscriptions.Select(subscription => new SubscriptionCacheInfo
                    {
                        subscriptionId = subscription.subscriptionId,
                        displayName = subscription.displayName
                    }).ToArray();

                    foreach (var subscription in subscriptions)
                    {
                        Trace.WriteLine(string.Format("\tSubscription {0} ({1})", subscription.subscriptionId, subscription.displayName));
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("\t{0}!", ex.Message));
                }
                tenantCache[tenantId] = info;
                Trace.WriteLine(string.Empty);
            }

            return tenantCache;
        }

        private async Task<string[]> GetTenantIds(AuthenticationResult authResult)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());

                var azureEnvironment = this.AzureEnvironments;
                var url = string.Format("{0}/tenants?api-version={1}", Constants.CSMUrls[(int)azureEnvironment], Constants.CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<TenantInfo>>();
                        return result.value.Select(tenant => tenant.tenantId).ToArray();
                    }

                    throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
                }
            }
        }

        private async Task<TenantDetails> GetTenantDetail(AuthenticationResult authResult, string tenantId)
        {
            if (Constants.InfrastructureTenantIds.Contains(tenantId))
            {
                return new TenantDetails
                {
                    objectId = tenantId,
                    displayName = "Infrastructure",
                    verifiedDomains = new[]
                    {
                        new VerifiedDomain
                        {
                            name = "live.com",
                            @default = true
                        }
                    }
                };
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());

                var azureEnvironment = this.AzureEnvironments;
                var url = string.Format("{0}/{1}/tenantDetails?api-version={2}", Constants.AADGraphUrls[(int)azureEnvironment], tenantId, Constants.AADGraphApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<TenantDetails>>();
                        return result.value[0];
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    if (content.StartsWith("{"))
                    {
                        var error = (JObject)JObject.Parse(content)["odata.error"];
                        if (error != null)
                        {
                            throw new InvalidOperationException(String.Format("GetTenantDetail {0}, {1}", response.StatusCode, error["message"].Value<string>("value")));
                        }
                    }

                    throw new InvalidOperationException(String.Format("GetTenantDetail {0}, {1}", response.StatusCode, await response.Content.ReadAsStringAsync()));
                }
            }
        }

        private async Task<SubscriptionInfo[]> GetSubscriptions(AuthenticationResult authResult)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", authResult.CreateAuthorizationHeader());

                var azureEnvironment = this.AzureEnvironments;
                var url = string.Format("{0}/subscriptions?api-version={1}", Constants.CSMUrls[(int)azureEnvironment], Constants.CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsAsync<ResultOf<SubscriptionInfo>>();
                        return result.value;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    if (content.StartsWith("{"))
                    {
                        var error = (JObject)JObject.Parse(content)["error"];
                        if (error != null)
                        {
                            throw new InvalidOperationException(String.Format("GetSubscriptions {0}, {1}", response.StatusCode, error.Value<string>("message")));
                        }
                    }

                    throw new InvalidOperationException(String.Format("GetSubscriptions {0}, {1}", response.StatusCode, await response.Content.ReadAsStringAsync()));
                }
            }
        }

    }
}
