//------------------------------------------------------------------------------
// <copyright file="DefaultAzureCredentialHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ARMClient.Authentication.Utilities;
using Azure.Core;
using Azure.Identity;

namespace ARMClient.Authentication.AADAuthentication
{
    public static class DefaultAzureCredentialHelper
    {
        // token cache file: C:\Users\{userName}\AppData\Local\.IdentityService\ANTARES_USER_TOKEN_CACHE.nocae
        // use different cache files between user and aad app to avoid issue 'Using a combined flat storage, like a file, to store both app and user tokens is not supported.'
        private const string TokenUserAutheticationRecordJson = "ANTARES_USER_AUTH_RECORD.json";
        private const string TokenUserCacheName = "ANTARES_USER_TOKEN_CACHE";
        private const string TokenAppCacheName = "ANTARES_APP_TOKEN_CACHE";
        private static readonly ConcurrentDictionary<string, AccessTokenCache> _inMemoryTokenCaches = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, TokenCredential> _defaultAzureCredentials = new(StringComparer.OrdinalIgnoreCase);
        private readonly static Lazy<string> TokenUserAuthenticationRecordFile = new Lazy<string>(() => Path.Combine(Environment.ExpandEnvironmentVariables($@"%USERPROFILE%\AppData\Local\.IdentityService"), TokenUserAutheticationRecordJson));

        public static AccessToken GetUserToken(string authorityHost, string tenantId, string scope, CancellationToken cancellationToken = default)
            => _inMemoryTokenCaches.GetOrAdd(GetCacheKey("user", tenantId, scope), () =>
            {
                var cred = GetDefaultAzureCredential(authorityHost, null);
                var accessToken = cred.GetToken(new TokenRequestContext(
                    scopes: [scope.EnsureDefaultScope()],
                    tenantId: tenantId,
                    parentRequestId: GetActivityId()), 
                    cancellationToken);
                PersistedAuthRecord.SaveIfNotExists(TokenUserAuthenticationRecordFile.Value, authorityHost, accessToken.Token);
                return accessToken;
            });

        public static async ValueTask<AccessToken> GetUserTokenAsync(string authorityHost, string tenantId, string scope, CancellationToken cancellationToken = default)
            => await _inMemoryTokenCaches.GetOrAddAsync(GetCacheKey("user", tenantId, scope), async() =>
            {
                var cred = GetDefaultAzureCredential(authorityHost, null);
                var accessToken = await cred.GetTokenAsync(new TokenRequestContext(
                    scopes: [scope.EnsureDefaultScope()],
                    tenantId: tenantId,
                    parentRequestId: GetActivityId()),
                    cancellationToken).ConfigureAwait(false);
                await PersistedAuthRecord.SaveIfNotExistsAsync(TokenUserAuthenticationRecordFile.Value, authorityHost, accessToken.Token).ConfigureAwait(false);
                return accessToken;
            });

        public static AccessToken GetManagedIdentityToken(string authorityHost, string managedIdentityResourceId, string tenantId, string scope, CancellationToken cancellationToken = default)
            => _inMemoryTokenCaches.GetOrAdd(GetCacheKey(managedIdentityResourceId, tenantId, scope), () => GetDefaultAzureCredential(authorityHost, managedIdentityResourceId).GetToken(new TokenRequestContext(
                scopes: [scope],
                tenantId: tenantId,
                parentRequestId: GetActivityId()
            ), cancellationToken));

        public static async ValueTask<AccessToken> GetManagedIdentityTokenAsync(string authorityHost, string managedIdentityResourceId, string tenantId, string scope, CancellationToken cancellationToken = default)
            => await _inMemoryTokenCaches.GetOrAddAsync(GetCacheKey(managedIdentityResourceId, tenantId, scope), () => GetDefaultAzureCredential(authorityHost, managedIdentityResourceId).GetTokenAsync(new TokenRequestContext(
                scopes: [scope],
                tenantId: tenantId,
                parentRequestId: GetActivityId()
            ), cancellationToken)).ConfigureAwait(false);

        public static TokenCredential GetDefaultAzureCredential(string authorityHost, string managedIdentityResourceId = null)
            => _defaultAzureCredentials.GetOrAdd(managedIdentityResourceId ?? string.Empty, id =>
            {
                var authorityHostUri = new UriBuilder(authorityHost) { Path = "/" }.Uri;
                var isInteractive = string.IsNullOrEmpty(id);
                if (!isInteractive)
                {
                    // For MI, token is cached by default, see https://devblogs.microsoft.com/azure-sdk/azure-sdk-release-august-2022/
                    // for system-assigned, both ManagedIdentityClientId and ManagedIdentityResourceId MUST not be set.
                    if (Guid.TryParse(id, out _))
                    {
                        return new ManagedIdentityCredential(id, options: new() { AuthorityHost = authorityHostUri });
                    }
                    else if (managedIdentityResourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ManagedIdentityCredential(new(id), options: new() { AuthorityHost = authorityHostUri });
                    }

                    // this means system-assigned identity
                    return new ManagedIdentityCredential(options: new() { AuthorityHost = authorityHostUri });
                }

                // InteractiveBrowserCredential provides browser user logon experience
                // using Microsoft Azure CLI AppId (04b07795-8ddb-461a-bbee-02f9e1bf7b46)
                // the result is written to the persistent cache
                return new InteractiveBrowserCredential(options: new()
                {
                    AuthorityHost = authorityHostUri,
                    TokenCachePersistenceOptions = new() { Name = TokenUserCacheName },
                    AdditionallyAllowedTenants = { "*" },
                    AuthenticationRecord = PersistedAuthRecord.Deserialize(TokenUserAuthenticationRecordFile.Value),
                });
            });

        public static ClientCertificateCredential GetClientCertificateCredential(string authorityHost, string tenantId, string clientId, X509Certificate2 clientCertificate)
        => (ClientCertificateCredential)_defaultAzureCredentials.GetOrAdd($"{tenantId}_{clientId}_{clientCertificate.Thumbprint}", id =>
            {
                var options = new ClientCertificateCredentialOptions
                {
                    AuthorityHost = new UriBuilder(authorityHost) { Path = "/" }.Uri,
                    SendCertificateChain = true,
                    // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.13.1/sdk/identity/Azure.Identity/samples/TokenCache.md
                    // By setting UnsafeAllowUnencryptedStorage to true, the credential will encrypt the contents of the token cache before persisting it if data protection is available on the current platform.
                    // If platform data protection is unavailable, it will write and read the persisted token data to an unencrypted local file ACL'd to the current account.
                    // If UnsafeAllowUnencryptedStorage is false (the default), a CredentialUnavailableException will be raised in the case no data protection is available.
                    TokenCachePersistenceOptions = new()
                    {
                        Name = TokenAppCacheName,
                    },
                };

                return new ClientCertificateCredential(tenantId: tenantId, clientId: clientId, clientCertificate: clientCertificate, options: options);
            });

        public static ClientSecretCredential GetClientSecretCredential(string authorityHost, string tenantId, string clientId, string clientSecret)
        => (ClientSecretCredential)_defaultAzureCredentials.GetOrAdd($"{tenantId}_{clientId}_{clientSecret.GetHashCode()}", id =>
        {
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = new UriBuilder(authorityHost) { Path = "/" }.Uri,
                // https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.13.1/sdk/identity/Azure.Identity/samples/TokenCache.md
                // By setting UnsafeAllowUnencryptedStorage to true, the credential will encrypt the contents of the token cache before persisting it if data protection is available on the current platform.
                // If platform data protection is unavailable, it will write and read the persisted token data to an unencrypted local file ACL'd to the current account.
                // If UnsafeAllowUnencryptedStorage is false (the default), a CredentialUnavailableException will be raised in the case no data protection is available.
                TokenCachePersistenceOptions = new()
                {
                    Name = TokenAppCacheName,
                },
            };

            return new ClientSecretCredential(tenantId: tenantId, clientId: clientId, clientSecret: clientSecret, options: options);
        });

        public static void ClearTokenCache()
        {
            var dirInfo = new DirectoryInfo(Environment.ExpandEnvironmentVariables($@"%USERPROFILE%\AppData\Local\.IdentityService"));
            if (dirInfo.Exists) 
            {
                foreach (var file in dirInfo.GetFiles("ANTARES_*"))
                {
                    try 
                    {
                        file.Delete();
                    }
                    // best effort
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static string GetActivityId()
            => $"{(Trace.CorrelationManager.ActivityId == Guid.Empty ? Guid.NewGuid() : Trace.CorrelationManager.ActivityId)}";

        private static string GetCacheKey(string managedIdentityResourceId, string tenantId, string scope)
            => $"{managedIdentityResourceId}_{tenantId}_{scope}";

        private static AccessToken GetOrAdd(this ConcurrentDictionary<string, AccessTokenCache> caches, string cacheKey, Func<AccessToken> func)
        {
            // implement in-memory token cache to provide consistency across all credential sources
            if (caches.TryGetValue(cacheKey, out var cache) && cache.ExpiresOn > DateTimeOffset.UtcNow)
            {
                return cache.Token;
            }

            try
            {
                var accessToken = func();
                caches[cacheKey] = new() { Token = accessToken, ExpiresOn = GetCacheExpiresOn(accessToken.ExpiresOn) };
                return accessToken;
            }
            catch (Exception)
            {
                // best effort to continue to use valid token
                if (cache != null && cache.Token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    return cache.Token;
                }

                throw;
            }
        }

        private static async ValueTask<AccessToken> GetOrAddAsync(this ConcurrentDictionary<string, AccessTokenCache> caches, string cacheKey, Func<ValueTask<AccessToken>> funcTask)
        {
            // implement in-memory token cache to provide consistency across all credential sources
            if (caches.TryGetValue(cacheKey, out var cache) && cache.ExpiresOn > DateTimeOffset.UtcNow)
            {
                return cache.Token;
            }

            try
            {
                var accessToken = await funcTask().ConfigureAwait(false);
                caches[cacheKey] = new() { Token = accessToken, ExpiresOn = GetCacheExpiresOn(accessToken.ExpiresOn) };
                return accessToken;
            }
            catch (Exception)
            {
                // best effort to continue to use valid token
                if (cache != null && cache.Token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    return cache.Token;
                }

                throw;
            }
        }

        // cache only half of the token life time
        private static DateTimeOffset GetCacheExpiresOn(DateTimeOffset expiresOn)
            => expiresOn.Subtract(TimeSpan.FromSeconds((expiresOn - DateTimeOffset.UtcNow).TotalSeconds / 2));

        private sealed class AccessTokenCache
        {
            public AccessToken Token { get; set; }
            public DateTimeOffset ExpiresOn { get; set; }
        }
    }
}