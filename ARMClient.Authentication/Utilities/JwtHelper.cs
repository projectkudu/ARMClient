//------------------------------------------------------------------------------
// <copyright file="JwtHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ARMClient.Authentication.Utilities
{
    public class JwtHelper
    {
        public const int DefaultTokenLifetimeInMinutes = 60;
        private const string AssertionPayload = "resource={0}&client_assertion_type={1}&client_assertion={2}&grant_type=client_credentials";

        private static DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private static IDictionary<string, string> _algorithmMap = new Dictionary<string, string>
        {
            { "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", "RS256" },
            { "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256", "HS256" }
        };

        public async Task<OAuthToken> AcquireTokenByX509(string tenantId, string clientId, X509Certificate2 cert, string resource, string tokenEndpoint)
        {
            var jwt = GetClientAssertion(tenantId, clientId, cert, tokenEndpoint);
            var payload = String.Format(AssertionPayload,
                                        WebUtility.UrlEncode(resource),
                                        WebUtility.UrlEncode("urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
                                        WebUtility.UrlEncode(jwt));

            return await HttpPost(tokenEndpoint, payload);
        }

        public string GetClientAssertion(string tenantId, string clientId, X509Certificate2 cert, string tokenEndpoint)
        {
            var claims = new List<Claim>();
            claims.Add(new Claim("sub", clientId));
            claims.Add(new Claim("jti", Guid.NewGuid().ToString()));

            var helper = new JwtHelper();
            return helper.CreateToken(clientId, tokenEndpoint, new ClaimsIdentity(claims), cert);
        }

        public string CreateToken(string issuer, string audience, ClaimsIdentity subject, X509Certificate2 signingCertificate)
        {
            var signingCredentials = new X509SigningCredentials(signingCertificate);
            var header = CreateHeader(signingCredentials);
            var payload = CreatePayload(issuer, audience, subject);

            var jwt = new StringBuilder();
            jwt.Append(header);
            jwt.Append('.');
            jwt.Append(payload);

            var signature = CreateSignature(jwt.ToString(), signingCredentials);
            jwt.Append('.');
            jwt.Append(signature);

            return jwt.ToString();
        }

        private string CreateHeader(SigningCredentials signingCredentials)
        {
            var json = new Dictionary<string, object>();
            json["typ"] = "JWT";

            string alg = null;
            if (_algorithmMap.TryGetValue(signingCredentials.SignatureAlgorithm, out alg))
            {
                json["alg"] = alg;
            }
            else
            {
                json["alg"] = signingCredentials.SignatureAlgorithm;
            }

            var x509 = signingCredentials as X509SigningCredentials;
            if (x509 != null && x509.Certificate != null)
            {
                json["x5t"] = Base64UrlEncoder.Encode(x509.Certificate.GetCertHash());

                if (IsCertificateAME(x509.Certificate))
                {
                    json["x5c"] = Convert.ToBase64String(x509.Certificate.GetRawCertData());
                }
            }

            var serializer = new JavaScriptSerializer();
            return Base64UrlEncoder.Encode(serializer.Serialize(json));
        }

        private string CreatePayload(string issuer, string audience, ClaimsIdentity subject)
        {
            var json = new Dictionary<string, object>();
            json["iss"] = issuer;
            json["aud"] = audience;
            json["exp"] = (int)DateTime.UtcNow.AddMinutes(DefaultTokenLifetimeInMinutes).Subtract(EpochTime).TotalSeconds;
            foreach (var claim in subject.Claims)
            {
                json[claim.Type] = claim.Value;
            }

            var serializer = new JavaScriptSerializer();
            return Base64UrlEncoder.Encode(serializer.Serialize(json));
        }

        public static bool IsCertificateAME(X509Certificate2 cert)
        {
            // should this be configurable to support others (eg. Nationals)?
            const string AMERoot = "CN=ameroot, DC=AME, DC=GBL";
            const string AMECert = "DC=AME, DC=GBL";

            // fast check
            if (!cert.Issuer.Contains(AMECert))
            {
                return false;
            }

            // since token is cached on the upper layer, we need not worry about perf
            var chain = new X509Chain();
            chain.Build(cert);

            var certs = chain.ChainElements;
            if (certs.Count <= 1)
            {
                return false;
            }

            var rootCert = certs[certs.Count - 1].Certificate;
            return rootCert.Issuer == AMERoot;
        }

        private string CreateSignature(string input, SigningCredentials signingCredentials)
        {
            var asymmetricKey = signingCredentials.SigningKey as AsymmetricSecurityKey;
            if (asymmetricKey == null)
            {
                throw new InvalidOperationException(string.Format("SigningKey {0} not supported!", signingCredentials.SigningKey.GetType().Name));
            }

            var algorithm = signingCredentials.SignatureAlgorithm;
            var formatter = asymmetricKey.GetSignatureFormatter(algorithm);
            byte[] bytes;
            using (var hash = asymmetricKey.GetHashAlgorithmForSignature(algorithm))
            {
                formatter.SetHashAlgorithm(hash.GetType().ToString());
                bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            var signature = formatter.CreateSignature(bytes);
            return Base64UrlEncoder.Encode(signature);
        }

        private async Task<OAuthToken> HttpPost(string address, string payload)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent.Value);

                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    var requestId = Guid.NewGuid().ToString();
                    request.Headers.Add("x-ms-request-id", requestId);
                    request.Headers.Add("x-ms-client-request-id", requestId);
                    request.Headers.Add("x-ms-correlation-request-id", requestId);

                    request.Content = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
                    using (var response = await client.SendAsync(request))
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var serializer = new JavaScriptSerializer();

                        var error = serializer.Deserialize<ErrorResponse>(json);

                        if (error != null && !string.IsNullOrEmpty(error.error_description))
                        {
                            throw new InvalidOperationException("Failed to acquire token.  " + error.error_description);
                        }

                        response.EnsureSuccessStatusCode();

                        return serializer.Deserialize<OAuthToken>(json);
                    }
                }
            }
        }

        static class Base64UrlEncoder
        {
            private static char base64PadCharacter = '=';
            private static string doubleBase64PadCharacter = string.Format("{0}{0}", Base64UrlEncoder.base64PadCharacter);
            private static char base64Character62 = '+';
            private static char base64Character63 = '/';
            private static char base64UrlCharacter62 = '-';
            private static char base64UrlCharacter63 = '_';

            public static string Encode(string arg)
            {
                if (arg == null)
                {
                    throw new ArgumentNullException(arg);
                }
                return Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(arg));
            }

            public static string Encode(byte[] arg)
            {
                if (arg == null)
                {
                    throw new ArgumentNullException("arg");
                }
                string text = Convert.ToBase64String(arg);
                text = text.Split(new[] { Base64UrlEncoder.base64PadCharacter })[0];
                text = text.Replace(Base64UrlEncoder.base64Character62, Base64UrlEncoder.base64UrlCharacter62);
                return text.Replace(Base64UrlEncoder.base64Character63, Base64UrlEncoder.base64UrlCharacter63);
            }

            public static byte[] DecodeBytes(string str)
            {
                if (str == null)
                {
                    throw new ArgumentNullException("str");
                }
                str = str.Replace(Base64UrlEncoder.base64UrlCharacter62, Base64UrlEncoder.base64Character62);
                str = str.Replace(Base64UrlEncoder.base64UrlCharacter63, Base64UrlEncoder.base64Character63);
                switch (str.Length % 4)
                {
                    case 0:
                        break;
                    case 2:
                        str += Base64UrlEncoder.doubleBase64PadCharacter;
                        break;
                    case 3:
                        str += Base64UrlEncoder.base64PadCharacter;
                        break;
                    default:
                        throw new InvalidOperationException(string.Format("JwtHelper: Unable to decode: '{0}' as Base64url encoded string.", str));
                }

                return Convert.FromBase64String(str);
            }

            public static string Decode(string str)
            {
                return Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(str));
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

            public DateTime ExpirationTime
            {
                get
                {
                    var secs = Int32.Parse(expires_on);
                    return EpochTime.AddSeconds(secs);
                }
            }

            public bool IsValid()
            {
                var secs = 0;
                if (!int.TryParse(expires_on, out secs))
                {
                    return false;
                }

                return EpochTime.AddSeconds(secs) > DateTime.UtcNow.AddMinutes(10);
            }
        }

        public class ErrorResponse
        {
            public string error { get; set; }
            public string error_description { get; set; }
            public int[] error_codes { get; set; }
            public DateTime timestamp { get; set; }
            public string trace_id { get; set; }
            public string correlation_id { get; set; }
        }
    }
}