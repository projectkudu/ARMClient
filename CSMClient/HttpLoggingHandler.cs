using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CSMClient
{
    class HttpLoggingHandler : DelegatingHandler
    {
        private readonly bool _addOutputColor;

        public HttpLoggingHandler(HttpMessageHandler innerHandler, bool addOutputColor)
            : base(innerHandler)
        {
            _addOutputColor = addOutputColor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const ConsoleColor headerNameColor = ConsoleColor.White;
            const ConsoleColor headerValueColor = ConsoleColor.Cyan;
            const ConsoleColor successColor = ConsoleColor.Green;
            const ConsoleColor failureColor = ConsoleColor.Red;

            ConsoleColor originalColor;
            Console.WriteLine("---------- Request -----------------------");
            Console.WriteLine();
            originalColor = Console.ForegroundColor;
            try
            {
                if (_addOutputColor)
                    Console.ForegroundColor = headerNameColor;
                Console.WriteLine("{0} {1} HTTP/{2}", request.Method, request.RequestUri.PathAndQuery, request.Version);
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
                    if (_addOutputColor)
                        Console.ForegroundColor = headerNameColor;
                    Console.Write("{0}: ", header.Key);
                    if (_addOutputColor)
                        Console.ForegroundColor = headerValueColor;
                    Console.WriteLine(String.Join("; ", header.Value));
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
            Console.WriteLine();
            await DumpContent(request.Content);
            Console.WriteLine();

            var watch = new Stopwatch();
            watch.Start();
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            watch.Stop();

            Console.WriteLine("---------- Response ({0} ms) -----------", watch.ElapsedMilliseconds);
            Console.WriteLine();

            originalColor = Console.ForegroundColor;
            try
            {
                if (_addOutputColor)
                    Console.ForegroundColor = response.IsSuccessStatusCode ? successColor : failureColor;
                Console.WriteLine("HTTP/{0} {1} {2}", response.Version, (int)response.StatusCode, response.StatusCode);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }

            foreach (var header in response.Headers)
            {
                originalColor = Console.ForegroundColor;
                try
                {
                    if (_addOutputColor)
                        Console.ForegroundColor = headerNameColor;
                    Console.Write("{0}: ", header.Key);
                    if (_addOutputColor)
                        Console.ForegroundColor = headerValueColor;
                    Console.WriteLine(String.Join("; ", header.Value));
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
            Console.WriteLine();
            await DumpContent(response.Content);
            Console.WriteLine();

            return response;
        }

        private async Task DumpContent(HttpContent content)
        {
            if (content == null)
            {
                return;
            }

            var result = await content.ReadAsStringAsync();
            if (content.Headers.ContentType.MediaType.Contains("application/json"))
            {
                if (result.StartsWith("["))
                {
                    if (_addOutputColor)
                    {
                        Program.PrintColoredJson(JArray.Parse(result));
                    }
                    else
                    {
                        Console.WriteLine(JArray.Parse(result));
                    }
                }
                else if (result.StartsWith("{"))
                {
                    if (_addOutputColor)
                    {
                        Program.PrintColoredJson(JObject.Parse(result));
                    }
                    else
                    {
                        Console.WriteLine(JObject.Parse(result));
                    }
                }
                else
                {
                    Console.WriteLine(result);
                }
            }
            else
            {
                Console.WriteLine(result);
            }
        }
    }
}
