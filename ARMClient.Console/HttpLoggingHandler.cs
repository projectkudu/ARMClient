using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ARMClient.Authentication;
using ARMClient.Authentication.Utilities;
using Newtonsoft.Json.Linq;

namespace ARMClient
{
    class HttpLoggingHandler : DelegatingHandler
    {
        private readonly bool _verbose;

        public HttpLoggingHandler(HttpMessageHandler innerHandler, bool verbose)
            : base(innerHandler)
        {
            _verbose = verbose;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const ConsoleColor headerNameColor = ConsoleColor.White;
            const ConsoleColor headerValueColor = ConsoleColor.Cyan;
            const ConsoleColor successColor = ConsoleColor.Green;
            const ConsoleColor failureColor = ConsoleColor.Red;

            if (InnerHandler is WinHttpHandler)
            {
                request.Version = new Version(2, 0);
            }

            ConsoleColor originalColor = Console.ForegroundColor;
            if (_verbose)
            {
                Console.WriteLine("---------- Request -----------------------");
                Console.WriteLine();
                try
                {
                    Console.ForegroundColor = headerNameColor;
                    Console.WriteLine("{0} {1} HTTP/{2}", request.Method, request.RequestUri.PathAndQuery, request.Version);
                    Console.Write("Host: ");
                    Console.ForegroundColor = headerValueColor;
                    Console.WriteLine(request.RequestUri.Host);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }

                foreach (var header in request.Headers)
                {
                    originalColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = headerNameColor;
                        Console.Write("{0}: ", header.Key);
                        Console.ForegroundColor = headerValueColor;
                        if (String.Equals("Authorization", header.Key))
                        {
                            Console.WriteLine(header.Value.First().Substring(0, 20) + "...");
                        }
                        else
                        {
                            Console.WriteLine(String.Join("; ", header.Value));
                        }
                    }
                    finally
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }
                Console.WriteLine();
                await DumpContent(request.Content);
                Console.WriteLine();
            }

            var watch = new Stopwatch();
            watch.Start();
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            watch.Stop();

            if (_verbose)
            {
                Console.WriteLine("---------- Response ({0} ms) ------------", watch.ElapsedMilliseconds);
                Console.WriteLine();

                originalColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = (int)response.StatusCode < 400 ? successColor : failureColor;
                    Console.WriteLine("HTTP/{0} {1} {2}", response.Version, (int)response.StatusCode, response.StatusCode);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }

                foreach (var header in response.Content.Headers)
                {
                    originalColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = headerNameColor;
                        Console.Write("{0}: ", header.Key);
                        Console.ForegroundColor = headerValueColor;
                        Console.WriteLine(String.Join("; ", header.Value));
                    }
                    finally
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }

                foreach (var header in response.Headers)
                {
                    originalColor = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = headerNameColor;
                        Console.Write("{0}: ", header.Key);
                        Console.ForegroundColor = headerValueColor;
                        Console.WriteLine(String.Join("; ", header.Value));
                    }
                    finally
                    {
                        Console.ForegroundColor = originalColor;
                    }
                }
                Console.WriteLine();
            }

            await DumpContent(response.Content);

            if (_verbose)
            {
                Console.WriteLine();
            }

            return response;
        }

        private async Task DumpContent(HttpContent content)
        {
            if (content == null || content.Headers.ContentType == null)
            {
                return;
            }

            var result = await content.ReadAndDecodeAsStringAsync();
            if (content.Headers.ContentType.MediaType.Contains(Constants.JsonContentType))
            {
                try
                {
                    if (result.StartsWith("["))
                    {
                        Program.PrintColoredJson(JArray.Parse(result));
                        return;
                    }
                    else if (result.StartsWith("{"))
                    {
                        Program.PrintColoredJson(JObject.Parse(result));
                        return;
                    }
                }
                catch (Exception)
                {
                    // best effort
                }
            }
            else if (content.Headers.ContentType.MediaType.Contains(Constants.XmlContentType) ||
                content.Headers.ContentType.MediaType.Contains("application/xml"))
            {
                try
                {
                    Program.PrintColoredXml(XDocument.Parse(result).ToString());
                    return;
                }
                catch (Exception)
                {
                    // best effort
                }
            }

            Console.Write(result);
        }
    }
}
