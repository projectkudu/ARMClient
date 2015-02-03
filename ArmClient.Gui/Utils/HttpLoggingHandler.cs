using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ArmGuiClient.Utils
{
    class HttpLoggingHandler : DelegatingHandler
    {
        private readonly bool _verbose;

        public HttpLoggingHandler(HttpMessageHandler innerHandler, bool verbose)
            : base(innerHandler)
        {
            this._verbose = verbose;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_verbose)
            {
                Logger.InfoLn("---------- Request -----------------------");

                Logger.InfoLn("{0} {1} HTTP/{2}", request.Method, request.RequestUri.PathAndQuery, request.Version);
                Logger.InfoLn("Host: {0}", request.RequestUri.Host);

                foreach (var header in request.Headers)
                {
                    string headerVal = string.Empty;
                    if (String.Equals("Authorization", header.Key))
                    {
                        headerVal = header.Value.First().Substring(0, 70) + "...";
                    }
                    else
                    {
                        headerVal = String.Join("; ", header.Value);
                    }
                    Logger.InfoLn("{0}: {1}", header.Key, headerVal);
                }

                await DumpContent(request.Content);
            }

            var watch = new Stopwatch();
            watch.Start();
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            watch.Stop();

            int responseStatusCocde = (int)response.StatusCode;

            if (_verbose)
            {
                Logger.InfoLn("---------- Response ({0} ms) ------------", watch.ElapsedMilliseconds);
                Logger.InfoLn("HTTP/{0} {1} {2}", response.Version, responseStatusCocde, response.StatusCode);
                foreach (var header in response.Headers)
                {
                    Logger.InfoLn("{0}: {1}", header.Key, String.Join("; ", header.Value));
                }
            }

            await DumpContent(response.Content, this.isErrorRequest(responseStatusCocde));
            return response;
        }

        private async Task DumpContent(HttpContent content, bool isErrorContent = false)
        {
            if (content == null || content.Headers.ContentType == null)
            {
                return;
            }
            var result = await content.ReadAsStringAsync();

            Logger.InfoLn(string.Empty);
            if (!string.IsNullOrWhiteSpace(result))
            {
                dynamic parsedJson = JsonConvert.DeserializeObject(result);
                string indentedResult = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);

                Logger.WriteLn(indentedResult, isErrorContent ? Logger.ErrorBrush : Logger.InfoBrush);
            }
        }

        private bool isErrorRequest(int responseStatusCode)
        {
            return responseStatusCode >= 400 && responseStatusCode < 600;
        }
    }
}
