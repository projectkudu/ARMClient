//------------------------------------------------------------------------------
// <copyright file="PersistedAuthRecord.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ARMClient.Authentication.Utilities;
using Azure.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ARMClient.Authentication.AADAuthentication
{
    public class PersistedAuthRecord
    {
        //{
        //  "username": "suwatch@microsoft.com",
        //  "authority": "login.microsoftonline.com",
        //  "homeAccountId": "a62060da-1ae3-44eb-8988-1197a68ff41e.72f988bf-86f1-41af-91ab-2d7cd011db47",
        //  "tenantId": "72f988bf-86f1-41af-91ab-2d7cd011db47",
        //  "clientId": "04b07795-8ddb-461a-bbee-02f9e1bf7b46",
        //  "version": "1.0"
        //}
        public PersistedAuthRecord(string authorityHost, string jwtToken) 
        {
            var json = JwtHelper.Parse(jwtToken);
            username = new[] { "upn", "unique_name", "name" }
                .Select(json.Value<string>)
                .Where(v => !string.IsNullOrEmpty(v))
                .FirstOrDefault();
            authority = new Uri(authorityHost).Host;
            tenantId = json.Value<string>("tid");
            homeAccountId = $"{json.Value<string>("oid")}.{tenantId}";
            clientId = json.Value<string>("appid");
            version = "1.0";
        }

        public string username { get; set; }
        public string authority { get; set; }
        public string homeAccountId { get; set; }
        public string tenantId { get; set; }
        public string clientId { get; set; }
        public string version { get; set; }

        public static void SaveIfNotExists(string file, string authorityHost, string jwtToken)
        {
            if (!File.Exists(file))
            {
                try
                {
                    var authRecord = new PersistedAuthRecord(authorityHost, jwtToken);
                    using var stream = new StreamWriter(file);
                    stream.Write(JsonConvert.SerializeObject(authRecord, Formatting.None));
                }
                catch
                {
                    // best effort
                }
            }
        }

        public static async Task SaveIfNotExistsAsync(string file, string authorityHost, string jwtToken)
        {
            if (!File.Exists(file))
            {
                try
                {
                    var authRecord = new PersistedAuthRecord(authorityHost, jwtToken);
                    using var stream = new StreamWriter(file);
                    await stream.WriteAsync(JsonConvert.SerializeObject(authRecord, Formatting.None)).ConfigureAwait(false);
                }
                catch
                {
                    // best effort
                }
            }
        }

        public static AuthenticationRecord Deserialize(string file)
        {
            if (File.Exists(file))
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    return AuthenticationRecord.Deserialize(stream);
                }
                catch
                {
                    // best effort
                }
            }

            return null;
        }
    }
}