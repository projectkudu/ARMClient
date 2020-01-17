using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ARMClient
{
    public static class AADHelper
    {
        const string ARMResource = "https://management.core.windows.net/";
        const string TokenEndpoint = "https://login.windows.net/{0}/oauth2/token";

        // client id for UPN is fixed to PowerShell SDK client id
        const string UPNPayload = "resource={0}&client_id=1950a258-227b-4e31-a9cf-717495945fc2&grant_type=password&username={1}&password={2}&scope=openid";
        const string SPNPayload = "resource={0}&client_id={1}&grant_type=client_credentials&client_secret={2}";
        const string AssertionPayload = "resource={0}&client_assertion_type={1}&client_assertion={2}&grant_type=client_credentials";

        public static async Task<OAuthToken> AcquireTokenBySPN(string tenantId, string clientId, string clientSecret, string resource)
        {
            var payload = String.Format(SPNPayload,
                                        WebUtility.UrlEncode(resource ?? ARMResource),
                                        WebUtility.UrlEncode(clientId),
                                        WebUtility.UrlEncode(clientSecret));

            return await HttpPost(tenantId, payload);
        }

        public static async Task<OAuthToken> AcquireTokenByUPN(string tenantId, string userName, string password, string resource)
        {
            var payload = String.Format(UPNPayload,
                                        WebUtility.UrlEncode(resource ?? ARMResource),
                                        WebUtility.UrlEncode(userName),
                                        WebUtility.UrlEncode(password));

            return await HttpPost(tenantId, payload);
        }

        public static async Task<OAuthToken> AcquireTokenByX509(string tenantId, string clientId, X509Certificate2 cert, string resource)
        {
            var jwt = GetClientAssertion(tenantId, clientId, cert);
            var payload = String.Format(AssertionPayload,
                                        WebUtility.UrlEncode(resource ?? ARMResource),
                                        WebUtility.UrlEncode("urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                                        WebUtility.UrlEncode(jwt));

            return await HttpPost(tenantId, payload);
        }

        static string GetClientAssertion(string tenantId, string clientId, X509Certificate2 cert)
        {
            var claims = new List<Claim>();
            claims.Add(new Claim("sub", clientId));

            var handler = new JwtSecurityTokenHandler();
            var credentials = new X509SigningCredentials(cert);
            return handler.CreateToken(
                issuer: clientId,
                audience: String.Format(TokenEndpoint, tenantId), 
                subject: new ClaimsIdentity(claims), 
                signingCredentials: credentials).RawData;
        }

        static async Task<OAuthToken> HttpPost(string tenantId, string payload)
        {
            using (var client = new HttpClient())
            {
                var address = String.Format(TokenEndpoint, tenantId);
                var content = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
                using (var response = await client.PostAsync(address, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Status:  {0}", response.StatusCode);
                        Console.WriteLine("Content: {0}", await response.Content.ReadAsStringAsync());
                    }

                    response.EnsureSuccessStatusCode();

                    var oauth = await response.Content.ReadAsStringAsync();
                    return JObject.Parse(oauth).ToObject<OAuthToken>();
                }
            }
        }

        public class OAuthToken
        {
            public string token_type { get; set; }
            public string expires_in { get; set; }
            public string expires_on { get; set; }
            public string resource { get; set; }
            public string access_token { get; set; }
            public string refresh_token { get; set; }
            public string scope { get; set; }
            public string id_token { get; set; }
        }
    }
}
