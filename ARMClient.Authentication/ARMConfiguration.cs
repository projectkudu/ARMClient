using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace ARMClient.Authentication
{
    public class ARMConfiguration
    {
        static readonly ConcurrentDictionary<string, string> _configs = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, string> _defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Dogfood.AADGraphUrl", "https://graph.ppe.windows.net" },
            { "Dogfood.AADMSGraphUrl", "https://graph.microsoft-ppe.com" },
            { "Dogfood.AADLoginUrl", "https://login.windows-ppe.net" },
            { "Dogfood.ARMResource", "https://management.core.windows.net/" },
            { "Dogfood.ARMUrl", "https://api-dogfood.resources.windows-int.net" },
            { "Dogfood.KeyVaultResource", "https://vault-int.azure-int.net" },
            { "Dogfood.RDFEUrl", "https://umapi-preview.core.windows-int.net" },
            { "Dogfood.ScmSuffix", ".windows-int.net" },
            { "Dogfood.VsoSuffix", ".tfsallin.net" },
            { "Dogfood.AppServiceUrl", "https://appservice.azure.com" },

            { "Prod.AADGraphUrl", "https://graph.windows.net" },
            { "Prod.AADMSGraphUrl", "https://graph.microsoft.com" },
            { "Prod.AADLoginUrl", "https://login.microsoftonline.com" },
            { "Prod.ARMResource", "https://management.core.windows.net/" },
            { "Prod.ARMUrl", "https://management.azure.com" },
            { "Prod.KeyVaultResource", "https://vault.azure.net" },
            { "Prod.RDFEUrl", "https://management.core.windows.net" },
            { "Prod.ScmSuffix", ".scm.azurewebsites.net" },
            { "Prod.VsoSuffix", ".visualstudio.com" },
            { "Prod.AppServiceUrl", "https://appservice.azure.com" },

            // https://docs.microsoft.com/en-us/azure/azure-government/documentation-government-developer-guide
            { "Fairfax.AADGraphUrl", "https://graph.windows.net" },
            { "Fairfax.AADMSGraphUrl", "https://graph.microsoft.us" },
            { "Fairfax.AADLoginUrl", "https://login.microsoftonline.us" },
            { "Fairfax.ARMResource", "https://management.core.usgovcloudapi.net/" },
            { "Fairfax.ARMUrl", "https://management.usgovcloudapi.net" },
            { "Fairfax.KeyVaultResource", "https://vault.usgovcloudapi.net" },
            { "Fairfax.RDFEUrl", "https://management.core.usgovcloudapi.net" },
            { "Fairfax.ScmSuffix", ".scm.azurewebsites.us" },
            { "Fairfax.AppServiceUrl", "https://appservice.azure.us" },

            // https://docs.microsoft.com/en-us/azure/china/resources-developer-guide
            { "Mooncake.AADGraphUrl", "https://graph.chinacloudapi.cn" },
            { "Mooncake.AADMSGraphUrl", "https://microsoftgraph.chinacloudapi.cn" },
            { "Mooncake.AADLoginUrl", "https://login.chinacloudapi.cn" },
            { "Mooncake.ARMResource", "https://management.core.chinacloudapi.cn/" },
            { "Mooncake.ARMUrl", "https://management.chinacloudapi.cn" },
            { "Mooncake.KeyVaultResource", "https://vault.azure.cn" },
            { "Mooncake.RDFEUrl", "https://management.core.chinacloudapi.cn" },
            { "Mooncake.ScmSuffix", ".scm.chinacloudsites.cn" },
            { "Mooncake.AppServiceUrl", "https://appservice.azure.cn" },

            // https://docs.microsoft.com/en-us/azure/germany/germany-developer-guide
            { "Blackforest.AADGraphUrl", "https://graph.cloudapi.de" },
            { "Blackforest.AADMSGraphUrl", "https://microsoftgraph.cloudapi.de" },
            { "Blackforest.AADLoginUrl", "https://login.microsoftonline.de" },
            { "Blackforest.ARMResource", "https://management.core.cloudapi.de/" },
            { "Blackforest.ARMUrl", "https://management.microsoftazure.de" },
            { "Blackforest.KeyVaultResource", "https://vault.microsoftazure.de" },
            { "Blackforest.RDFEUrl", "https://management.core.cloudapi.de/" },
            { "Blackforest.ScmSuffix", ".scm.azurewebsites.de" },
            { "Blackforest.AppServiceUrl", "https://appservice.azure.de" },

            // Sovereign cloud - France (Bleu)
            { "Bleu.AADGraphUrl", string.Empty },
            { "Bleu.AADMSGraphUrl", "https://graph.svc.sovcloud.fr" },
            { "Bleu.AADLoginUrl", "https://login.sovcloud-identity.fr" },
            { "Bleu.ARMResource", "https://management.core.sovcloud-api.fr/" },
            { "Bleu.ARMUrl", "https://management.sovcloud-api.fr" },
            { "Bleu.KeyVaultResource", "https://vault.sovcloud-api.fr" },
            { "Bleu.RDFEUrl", "https://management.core.sovcloud-api.fr" },
            { "Bleu.ScmSuffix", ".scm.azurewebsites.fr" },
            { "Bleu.AppServiceUrl", "https://appservice.azure.sovcloud-api.fr" },

            // Sovereign cloud - Germany (Delos)
            { "Delos.AADGraphUrl", string.Empty },
            { "Delos.AADMSGraphUrl", "https://graph.svc.sovcloud.de" },
            { "Delos.AADLoginUrl", "https://login.sovcloud-identity.de" },
            { "Delos.ARMResource", "https://management.core.sovcloud-api.de/" },
            { "Delos.ARMUrl", "https://management.sovcloud-api.de" },
            { "Delos.KeyVaultResource", "https://vault.sovcloud-api.de" },
            { "Delos.RDFEUrl", "https://management.core.sovcloud-api.de" },
            { "Delos.ScmSuffix", ".scm.azurewebsites.de" },
            { "Delos.AppServiceUrl", "https://appservice.azure.de" },
        };

        private string _aadLoginUrl;
        private string _aadGraphUrl;
        private string _aadMSGraphUrl;
        private string _armUrl;
        private string _rdfeUrl;
        private string _armResource;
        private string _keyVaultResource;
        private string _scmSuffix;
        private string _vsoSuffix;
        private string _appServiceUrl;

        public ARMConfiguration(string env)
        {
            AzureEnvironment = env;
            Current = this;
        }

        public ARMConfiguration(Uri aadLoginUrl)
            : this(_defaults.First(kv => kv.Key.EndsWith(".AADLoginUrl") && string.Equals(new Uri(kv.Value).Host, aadLoginUrl.Host, StringComparison.OrdinalIgnoreCase)).Key.Split('.')[0])
        {
        }

        public static ARMConfiguration Current { get; private set; }
        public string AzureEnvironment { get; private set; }
        public string AADLoginUrl { get { return _aadLoginUrl ?? (_aadLoginUrl = _configs.GetOrAdd($"{AzureEnvironment}.AADLoginUrl", k => GetValue(k))); } }
        public string AADGraphUrl { get { return _aadGraphUrl ?? (_aadGraphUrl = _configs.GetOrAdd($"{AzureEnvironment}.AADGraphUrl", k => GetValue(k))); } }
        public string AADMSGraphUrl { get { return _aadMSGraphUrl ?? (_aadMSGraphUrl = _configs.GetOrAdd($"{AzureEnvironment}.AADMSGraphUrl", k => GetValue(k))); } }
        public string ARMUrl { get { return _armUrl ?? (_armUrl = _configs.GetOrAdd($"{AzureEnvironment}.ARMUrl", k => GetValue(k))); } }
        public string RDFEUrl { get { return _rdfeUrl ?? (_rdfeUrl = _configs.GetOrAdd($"{AzureEnvironment}.RDFEUrl", k => GetValue(k))); } }
        public string ARMResource { get { return _armResource ?? (_armResource = _configs.GetOrAdd($"{AzureEnvironment}.ARMResource", k => GetValue(k))); } }
        public string KeyVaultResource { get { return _keyVaultResource ?? (_keyVaultResource = _configs.GetOrAdd($"{AzureEnvironment}.KeyVaultResource", k => GetValue(k))); } }
        public string ScmSuffix { get { return _scmSuffix ?? (_scmSuffix = _configs.GetOrAdd($"{AzureEnvironment}.ScmSuffix", k => GetValue(k))); } }
        public string VsoSuffix { get { return _vsoSuffix ?? (_vsoSuffix = _configs.GetOrAdd($"{AzureEnvironment}.VsoSuffix", k => GetValue(k))); } }
        public string AppServiceUrl { get { return _appServiceUrl ?? (_appServiceUrl = _configs.GetOrAdd($"{AzureEnvironment}.{nameof(AppServiceUrl)}", k => GetValue(k))); } }

        static string GetValue(string key)
        {
            // env -> appSetting -> default
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = ConfigurationManager.AppSettings[key];
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (_defaults.TryGetValue(key, out value))
            {
                return value;
            }

            throw new InvalidOperationException($"Configuration for {key} is not found.   Please see https://github.com/projectkudu/ARMClient/wiki/ARMConfiguration for details.");
        }

        public static string GetEnvironmentByRequest(Uri uri)
        {
            foreach (var pair in _defaults)
            {
                if (Uri.TryCreate(pair.Value, UriKind.Absolute, out Uri defaultUri))
                {
                    if (uri.Host.EndsWith(defaultUri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Key.Split('.')[0];
                    }
                }
            }

            foreach (var pair in _configs)
            {
                if (Uri.TryCreate(pair.Value, UriKind.Absolute, out Uri defaultUri))
                {
                    if (uri.Host.EndsWith(defaultUri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Key.Split('.')[0];
                    }
                }
            }

            return null;
        }
    }
}
