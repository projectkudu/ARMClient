using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ARMClient.Authentication.AADAuthentication;
using ARMClient.Authentication.Utilities;

namespace RDFEClient
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            var methodName = args[0];
            try
            {
                var method = typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .FirstOrDefault(m => string.Equals(methodName, m.Name, StringComparison.OrdinalIgnoreCase));
                if (method == null)
                {
                    PrintUsage();
                    return;
                }

                method.Invoke(null, args.Skip(1).ToArray());
            }
            catch (TargetParameterCountException)
            {
                PrintUsage(methodName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetBaseException());
            }
        }

        static void PrintUsage(string methodName = null)
        {
            Console.WriteLine("Usage:");
            foreach (var cmd in new[]
            {
                "  RDFEClient.exe Login",
                "  RDFEClient.exe ListCache",
                "  RDFEClient.exe GetDeployment subscriptionId serviceName",
                "  RDFEClient.exe GetConfiguration subscriptionId serviceName",
                "  RDFEClient.exe UpdateConfiguration subscriptionId serviceName content",
                "  RDFEClient.exe ListExtensions subscriptionId serviceName",
                "  RDFEClient.exe GetExtension subscriptionId serviceName extensionId",
                "  RDFEClient.exe AddExtension subscriptionId serviceName content",
                "  RDFEClient.exe DeleteExtension subscriptionId serviceName extensionId",
                "  RDFEClient.exe AddServiceTunnelingExtension subscriptionId serviceName",
                "  RDFEClient.exe EnableServiceTunnelingExtension subscriptionId serviceName serviceName",
                "  RDFEClient.exe DisableServiceTunnelingExtension subscriptionId serviceName serviceName",
                "  RDFEClient.exe AddServiceTunnelingExtensionConfiguration subscriptionId serviceName serviceName",
                "  RDFEClient.exe ListReservedIps subscriptionId",
                "  RDFEClient.exe GetReservedIp subscriptionId reservedIpName",
                "  RDFEClient.exe AddReservedIp subscriptionId reservedIpName location",
                "  RDFEClient.exe DeleteReservedIp subscriptionId reservedIpName",
                "  RDFEClient.exe GetOperation subscriptionId requestId",
            })
            {
                if (string.IsNullOrEmpty(methodName) || cmd.IndexOf(" " + methodName + " ", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    Console.WriteLine(cmd);
                }
            }
        }

        static void Login()
        {
            Utils.SetTraceListener(new ConsoleTraceListener());
            var persistentAuthHelper = new PersistentAuthHelper();
            persistentAuthHelper.AcquireTokens().Wait();
        }

        static void ListCache()
        {
            Utils.SetTraceListener(new ConsoleTraceListener());
            var persistentAuthHelper = new PersistentAuthHelper();
            foreach (var line in persistentAuthHelper.DumpTokenCache())
            {
                Console.WriteLine(line);
            }
        }

        static void GetDeployment(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }

        static void GetConfiguration(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var deploymentElem = XDocument.Parse(response.Content.ReadAsStringAsync().Result).Root;
                var deploymentNs = deploymentElem.Name.Namespace;
                var mgr = new XmlNamespaceManager(new NameTable());
                mgr.AddNamespace("x", deploymentNs.NamespaceName);

                var configurationElem = deploymentElem.XPathSelectElement("/x:Deployment/x:Configuration", mgr);
                var serviceConfigurationElem = XDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(configurationElem.Value))).Root;

                Console.WriteLine();
                Console.WriteLine("---------- ServiceConfiguration.cscfg -----------------------");
                Console.WriteLine();
                RDFEClient.PrintColoredXml(serviceConfigurationElem.ToString());
            }
        }

        static void UpdateConfiguration(string subscriptionId, string serviceName, string content)
        {
            var payload = content;
            if (File.Exists(content))
            {
                payload = File.ReadAllText(content);
            }

            if (payload.StartsWith("<ServiceConfiguration "))
            {
                payload = string.Format(@"
<ChangeConfiguration xmlns='http://schemas.microsoft.com/windowsazure'>
    <Configuration>{0}</Configuration>
    <TreatWarningsAsError>false</TreatWarningsAsError>
</ChangeConfiguration>
", Convert.ToBase64String(Encoding.UTF8.GetBytes(XDocument.Parse(payload).ToString(SaveOptions.DisableFormatting))));
            }

            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production/?comp=config", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "post", new StringContent(payload, Encoding.UTF8, "text/xml")).Result)
            {
            }
        }

        static string GetAuthorizationHeader(string subscriptionId)
        {
            Utils.SetTraceListener(new ConsoleTraceListener());

            var accessToken = Utils.GetDefaultToken();
            if (!String.IsNullOrEmpty(accessToken))
            {
                return String.Format("Bearer {0}", accessToken);
            }

            var persistentAuthHelper = new PersistentAuthHelper();
            var cacheInfo = persistentAuthHelper.GetToken(subscriptionId, "https://management.core.windows.net/").Result;
            return cacheInfo.CreateAuthorizationHeader();

        }

        static void ListExtensions(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/extensions", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }

        static void GetExtension(string subscriptionId, string serviceName, string extensionId)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/extensions/{2}", subscriptionId, serviceName, extensionId));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }

        static void AddExtension(string subscriptionId, string serviceName, string content)
        {
            var payload = content;
            if (File.Exists(content))
            {
                payload = File.ReadAllText(content);
            }

            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/extensions", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "post", new StringContent(payload, Encoding.UTF8, "text/xml")).Result)
            {
            }
        }

        static void AddServiceTunnelingExtension(string subscriptionId, string serviceName)
        {
            var payload = @"
<Extension xmlns='http://schemas.microsoft.com/windowsazure'>
  <ProviderNameSpace>Microsoft.Azure.Networking.SDN</ProviderNameSpace>
  <Type>Aquarius</Type>
  <Id>FrontEndRole-Aquarius-Production-Ext-1</Id>
  <Thumbprint></Thumbprint>
  <ThumbprintAlgorithm></ThumbprintAlgorithm>
  <PublicConfiguration>eyJQbHVnaW5zVG9FbmFibGUiOlsiU2VydmljZVR1bm5lbEV4dGVuc2lvbiJdfQ==</PublicConfiguration>
  <PrivateConfiguration>e30=</PrivateConfiguration>
  <Version>4.2</Version>
</Extension>";

            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/extensions", subscriptionId, serviceName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "post", new StringContent(payload, Encoding.UTF8, "text/xml")).Result)
            {
            }
        }

        static void EnableServiceTunnelingExtension(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production", subscriptionId, serviceName));
            string base64Configuration;
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var deploymentElem = XDocument.Parse(response.Content.ReadAsStringAsync().Result).Root;
                var deploymentNs = deploymentElem.Name.Namespace;
                var mgr = new XmlNamespaceManager(new NameTable());
                mgr.AddNamespace("x", deploymentNs.NamespaceName);

                var configurationElem = deploymentElem.XPathSelectElement("/x:Deployment/x:Configuration", mgr);
                base64Configuration = configurationElem.Value;
            }

            var payload = string.Format(@"
<ChangeConfiguration xmlns='http://schemas.microsoft.com/windowsazure'>
  <Configuration>{0}</Configuration>
  <TreatWarningsAsError>false</TreatWarningsAsError>
  <Mode>Auto</Mode>
  <ExtensionConfiguration>
    <NamedRoles>
      <Role>
        <RoleName>FrontEndRole</RoleName>
        <Extensions>
          <Extension>
            <Id>FrontEndRole-Aquarius-Production-Ext-1</Id>
          </Extension>
        </Extensions>
      </Role>
    </NamedRoles>
  </ExtensionConfiguration>
</ChangeConfiguration>", base64Configuration);

            UpdateConfiguration(subscriptionId, serviceName, payload);
        }

        static void DisableServiceTunnelingExtension(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production", subscriptionId, serviceName));
            string base64Configuration;
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var deploymentElem = XDocument.Parse(response.Content.ReadAsStringAsync().Result).Root;
                var deploymentNs = deploymentElem.Name.Namespace;
                var mgr = new XmlNamespaceManager(new NameTable());
                mgr.AddNamespace("x", deploymentNs.NamespaceName);

                var configurationElem = deploymentElem.XPathSelectElement("/x:Deployment/x:Configuration", mgr);
                base64Configuration = configurationElem.Value;
            }

            var payload = string.Format(@"
<ChangeConfiguration xmlns='http://schemas.microsoft.com/windowsazure'>
  <Configuration>{0}</Configuration>
  <TreatWarningsAsError>false</TreatWarningsAsError>
  <Mode>Auto</Mode>
  <ExtensionConfiguration>
    <NamedRoles>
      <Role>
        <RoleName>FrontEndRole</RoleName>
        <Extensions>
          <Extension>
            <Id>FrontEndRole-Aquarius-Production-Ext-1</Id>
            <State>Disable</State>
          </Extension>
        </Extensions>
      </Role>
    </NamedRoles>
  </ExtensionConfiguration>
</ChangeConfiguration>", base64Configuration);

            UpdateConfiguration(subscriptionId, serviceName, payload);
        }

        static void DeleteExtension(string subscriptionId, string serviceName, string extensionId)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/extensions/{2}", subscriptionId, serviceName, extensionId));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "delete").Result)
            {
            }
        }

        static void AddServiceTunnelingExtensionConfiguration(string subscriptionId, string serviceName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/Production", subscriptionId, serviceName));
            XElement serviceConfigurationElem;
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var deploymentElem = XDocument.Parse(response.Content.ReadAsStringAsync().Result).Root;
                var deploymentNs = deploymentElem.Name.Namespace;
                var mgr = new XmlNamespaceManager(new NameTable());
                mgr.AddNamespace("x", deploymentNs.NamespaceName);

                var configurationElem = deploymentElem.XPathSelectElement("/x:Deployment/x:Configuration", mgr);
                serviceConfigurationElem = XDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(configurationElem.Value))).Root;
            }

            var serviceConfigurationNs = serviceConfigurationElem.Name.Namespace;
            var networkConfiguration = serviceConfigurationElem.Elements().FirstOrDefault(n => n.Name == serviceConfigurationNs.GetName("NetworkConfiguration"));
            var guestAgentSettings = serviceConfigurationElem.Elements().FirstOrDefault(n => n.Name == serviceConfigurationNs.GetName("GuestAgentSettings"));

            XElement serviceTunnelingConfigurations = null;
            if (networkConfiguration == null)
            {
                networkConfiguration = new XElement(serviceConfigurationNs.GetName("NetworkConfiguration"));
                guestAgentSettings.AddBeforeSelf(networkConfiguration);
            }
            else
            {
                serviceTunnelingConfigurations = networkConfiguration.Elements().FirstOrDefault(n => n.Name == serviceConfigurationNs.GetName("ServiceTunnelingConfigurations"));
            }

            // only add if not exists
            if (serviceTunnelingConfigurations == null)
            {
                var mgr = new XmlNamespaceManager(new NameTable());
                mgr.AddNamespace("x", serviceConfigurationNs.NamespaceName);

                serviceTunnelingConfigurations = new XElement(serviceConfigurationNs.GetName("ServiceTunnelingConfigurations"));
                foreach (var endpoint in serviceConfigurationElem.XPathSelectElements("/x:ServiceConfiguration/x:NetworkConfiguration/x:AddressAssignments/x:VirtualIPs/x:VirtualIP/x:Endpoints/x:Endpoint[@role='FrontEndRole' and contains(@name,'FrontEndPort')]", mgr))
                {
                    var endpointName = endpoint.Attributes().First(a => a.Name.LocalName == "name").Value;
                    serviceTunnelingConfigurations.Add(new XElement(serviceConfigurationNs.GetName("ServiceTunnelingConfiguration"),
                            new XAttribute("name", "ServiceTunneling-" + endpointName),
                            new XAttribute("role", "FrontEndRole"),
                            new XAttribute("endpoint", endpointName)
                        ));
                }

                if (!serviceTunnelingConfigurations.Elements().Any())
                {
                    return;
                }

                networkConfiguration.Add(serviceTunnelingConfigurations);

                UpdateConfiguration(subscriptionId, serviceName, serviceConfigurationElem.ToString());
            }
        }

        static void ListReservedIps(string subscriptionId)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/networking/reservedips", subscriptionId));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }

        static void GetReservedIp(string subscriptionId, string reservedIpName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/networking/reservedips/{1}", subscriptionId, reservedIpName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }

        static void AddReservedIp(string subscriptionId, string reservedIpName, string location)
        {
            Dictionary<string, string> locationZones = new Dictionary<string, string>
            {
                { "Australia Southeast", "" },
                { "East US", "useast-AZ01" },
                { "East US 2 EUAP", "useast2euap-AZ02" },
                { "East US 2", "useast2-AZ03" },
                //{ "East US 2 EUAP", "" },
                //{ "North Central US", "usnorth-NONE" },
                { "North Central US", "" }
            };

            var pair = locationZones.FirstOrDefault(p => string.Equals(p.Key, location, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(pair.Key))
            {
                throw new InvalidOperationException("Unknown AvailabilityZone for " + location + " location!");
            }

            var payload = !string.IsNullOrEmpty(pair.Value) ? string.Format(@"
<ReservedIP xmlns='http://schemas.microsoft.com/windowsazure'>
  <Name>{0}</Name>
  <Label>AppServiceReservedIp</Label>
  <DeploymentName></DeploymentName>
  <Location>{1}</Location>
  <IPTags>
    <IPTag>
      <IPTagType>FirstPartyUsage</IPTagType>
      <Value>/AppService</Value>
    </IPTag>
    <IPTag>
      <IPTagType>AvailabilityZone</IPTagType>
      <Value>{2}</Value>
    </IPTag>
  </IPTags>
</ReservedIP>", reservedIpName, pair.Key, pair.Value) : string.Format(@"
<ReservedIP xmlns='http://schemas.microsoft.com/windowsazure'>
  <Name>{0}</Name>
  <Label>AppServiceReservedIp</Label>
  <DeploymentName></DeploymentName>
  <Location>{1}</Location>
  <IPTags>
    <IPTag>
      <IPTagType>FirstPartyUsage</IPTagType>
      <Value>/AppService</Value>
    </IPTag>
  </IPTags>
</ReservedIP>", reservedIpName, pair.Key);

            var authHeader = GetAuthorizationHeader(subscriptionId);
            var content = new StringContent(payload, Encoding.UTF8, "text/xml");
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/networking/reservedips", subscriptionId));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "post", content).Result)
            {
            }
        }

        static void DeleteReservedIp(string subscriptionId, string reservedIpName)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/services/networking/reservedips/{1}", subscriptionId, reservedIpName));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "delete").Result)
            {
            }
        }

        static void GetOperation(string subscriptionId, string requestId)
        {
            var authHeader = GetAuthorizationHeader(subscriptionId);
            var uri = new Uri(string.Format("https://management.core.windows.net/{0}/operations/{1}", subscriptionId, requestId));
            using (var response = RDFEClient.HttpInvoke(uri, authHeader, "get").Result)
            {
            }
        }
    }
}
