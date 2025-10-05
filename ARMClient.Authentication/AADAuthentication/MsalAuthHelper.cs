//------------------------------------------------------------------------------
// <copyright file="MsalLoginHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ARMClient.Authentication.Utilities;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ARMClient.Authentication.AADAuthentication
{
    // Azure.Identity does not work with PPE/Dogfood Entra
    // Use MSAL instead
    public static class MsalAuthHelper
    {
        private const string TokenUserCacheName = "ANTARES_MSAL_USER_TOKEN_CACHE";
        private const string TokenAppCacheName = "ANTARES_MSAL_APP_TOKEN_CACHE";
        private static readonly ConcurrentDictionary<string, IClientApplicationBase> _clientApplicationCache = new ConcurrentDictionary<string, IClientApplicationBase>(StringComparer.OrdinalIgnoreCase);

        public static async Task<AuthenticationResult> GetUserTokenAsync(string authorityHost, string tenantId, string scope, CancellationToken cancellationToken = default)
        {
            var app = (PublicClientApplication)await GetClientApplicationAsync(
                clientId: Constants.AADClientId,
                authorityUri: new UriBuilder(authorityHost) { Path = tenantId }.Uri.AbsoluteUri,
                redirectUri: Constants.AADRedirectUri);

            var accounts = await app.GetAccountsAsync();
            try
            {
                return await app.AcquireTokenSilent([scope.EnsureDefaultScope()], accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
            }
            catch (MsalUiRequiredException)
            {
                return await app.AcquireTokenInteractive([scope.EnsureDefaultScope()]).ExecuteAsync(cancellationToken);
            }
        }

        public static async Task<AuthenticationResult> GetClientCertificateTokenAsync(string authorityHost, string tenantId, string scope, string clientId, X509Certificate2 clientCertificate, CancellationToken cancellationToken = default)
        {
            var app = (ConfidentialClientApplication)await GetClientApplicationAsync(
                clientId: clientId,
                authorityUri: new UriBuilder(authorityHost) { Path = tenantId }.Uri.AbsoluteUri,
                certificate: clientCertificate);

            return await app.AcquireTokenForClient([scope.EnsureDefaultScope()]).WithSendX5C(withSendX5C: true).ExecuteAsync(cancellationToken);
        }

        public static async Task<AuthenticationResult> GetClientSecretTokenAsync(string authorityHost, string tenantId, string scope, string clientId, string clientSecret, CancellationToken cancellationToken = default)
        {
            var app = (ConfidentialClientApplication)await GetClientApplicationAsync(
                clientId: clientId,
                authorityUri: new UriBuilder(authorityHost) { Path = tenantId }.Uri.AbsoluteUri,
                clientSecret: clientSecret);

            return await app.AcquireTokenForClient([scope.EnsureDefaultScope()]).ExecuteAsync(cancellationToken);
        }

        private static async Task<IClientApplicationBase> GetClientApplicationAsync(string clientId, string authorityUri, string redirectUri = null, X509Certificate2 certificate = null, string clientSecret = null)
        {
            var key = $"{clientId}|{authorityUri}|{redirectUri}|{certificate?.Thumbprint}";
            if (_clientApplicationCache.TryGetValue(key, out var clientApp))
            {
                return clientApp;
            }

            StorageCreationProperties storageProperties = null;
            if (certificate != null)
            {
                var confidentialClientApp = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithCertificate(certificate)
                    .WithAuthority(authorityUri, validateAuthority: true)
                    .Build();
                storageProperties =
                    new StorageCreationPropertiesBuilder($"{TokenAppCacheName}_{ARMConfiguration.Current.AzureEnvironment}.dat", Environment.ExpandEnvironmentVariables($@"%USERPROFILE%\AppData\Local\.IdentityService"))
                    .Build();

                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
                cacheHelper.VerifyPersistence();
                cacheHelper.RegisterCache(confidentialClientApp.AppTokenCache);

                clientApp = confidentialClientApp;
            }
            else if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                var confidentialClientApp = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(authorityUri, validateAuthority: true)
                    .Build();
                storageProperties =
                    new StorageCreationPropertiesBuilder($"{TokenAppCacheName}_{ARMConfiguration.Current.AzureEnvironment}.dat", Environment.ExpandEnvironmentVariables($@"%USERPROFILE%\AppData\Local\.IdentityService"))
                    .Build();

                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
                cacheHelper.VerifyPersistence();
                cacheHelper.RegisterCache(confidentialClientApp.AppTokenCache);

                clientApp = confidentialClientApp;
            }
            else
            {
                var publicClientApp = PublicClientApplicationBuilder.Create(clientId)
                    .WithRedirectUri(redirectUri)
                    .WithAuthority(authorityUri, validateAuthority: true)
                    .Build();
                storageProperties =
                    new StorageCreationPropertiesBuilder($"{TokenUserCacheName}_{ARMConfiguration.Current.AzureEnvironment}.dat", Environment.ExpandEnvironmentVariables($@"%USERPROFILE%\AppData\Local\.IdentityService"))
                    .Build();

                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
                cacheHelper.VerifyPersistence();
                cacheHelper.RegisterCache(publicClientApp.UserTokenCache);

                clientApp = publicClientApp;
            }

            _clientApplicationCache[key] = clientApp;
            return clientApp;
        }
    }
}
