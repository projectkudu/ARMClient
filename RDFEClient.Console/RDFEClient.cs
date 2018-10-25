using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RDFEClient
{
    public class RDFEClient
    {
        public const string JsonContentType = "application/json";
        public const string XmlContentType = "text/xml";

        public static string Jwt { get; set; }

        public static HttpClient HttpClient = new HttpClient(new HttpLoggingHandler(new HttpClientHandler(), verbose: true));

        public static Lazy<string> FileVersion = new Lazy<string>(() =>
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        });

        public static Lazy<string> UserAgent = new Lazy<string>(() =>
        {
            return "RDFEClient/" + FileVersion.Value;
        });

        public static async Task<HttpResponseMessage> HttpInvoke(Uri uri, string authHeader, string verb, HttpContent content = null)
        {
            var client = HttpClient;
            using (var request = new HttpRequestMessage(new HttpMethod(verb.ToUpper()), uri))
            {
                request.Headers.Add("Authorization", authHeader);
                request.Headers.Add("User-Agent", UserAgent.Value);
                request.Headers.Add("Accept", JsonContentType);
                request.Headers.Add("x-ms-version", "2017-06-01");

                var requestId = Guid.NewGuid().ToString();
                request.Headers.Add("x-ms-request-id", requestId);
                request.Headers.Add("x-ms-client-request-id", requestId);
                request.Headers.Add("x-ms-correlation-request-id", requestId);

                if (content != null)
                {
                    request.Content = content;
                }

                var response = await client.SendAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    IEnumerable<string> values;
                    if (response.Headers.TryGetValues("x-ms-request-id", out values))
                    {
                        Console.WriteLine("RDFEClient.exe GetOperation {0} {1}", uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).First(), values.First());
                    }
                }

                return response;
            }
        }

        //http://stackoverflow.com/questions/4810841/how-can-i-pretty-print-json-using-javascript
        public static void PrintColoredJson(JContainer json)
        {
            const string jsonPatterns =
                @"(\s*""(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\""])*""(\s*:)?|\s*\b(true|false|null)\b|\s*-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?|\s*[\[\{\]\},]|\s*\n)";
            const ConsoleColor keyColor = ConsoleColor.DarkGreen;
            const ConsoleColor numbersColor = ConsoleColor.Cyan;
            const ConsoleColor stringColor = ConsoleColor.DarkYellow;
            const ConsoleColor booleanColor = ConsoleColor.DarkCyan;
            const ConsoleColor nullColor = ConsoleColor.DarkMagenta;

            var originalColor = Console.ForegroundColor;

            try
            {

                var regex = new Regex(jsonPatterns, RegexOptions.None);

                foreach (Match match in regex.Matches(json.ToString()))
                {
                    if (match.Success)
                    {
                        var value = match.Groups[1].Value;
                        var currentColor = numbersColor;
                        if (Regex.IsMatch(value, "^\\s*\""))
                        {
                            currentColor = Regex.IsMatch(value, ":$") ? keyColor : stringColor;
                        }
                        else if (Regex.IsMatch(value, "true|false"))
                        {
                            currentColor = booleanColor;
                        }
                        else if (Regex.IsMatch(value, "null"))
                        {
                            currentColor = nullColor;
                        }
                        else if (Regex.IsMatch(value, @"[\[\{\]\},]"))
                        {
                            currentColor = originalColor;
                        }

                        Console.ForegroundColor = currentColor;
                        Console.Write(value);
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        public static void PrintColoredXml(string str)
        {
            ConsoleColor HC_NODE = ConsoleColor.DarkGreen;
            ConsoleColor HC_STRING = ConsoleColor.Blue;
            ConsoleColor HC_ATTRIBUTE = ConsoleColor.Red;
            ConsoleColor HC_COMMENT = ConsoleColor.DarkGray;
            ConsoleColor HC_INNERTEXT = ConsoleColor.DarkYellow;

            int cur = 0;
            int k = 0;

            int st, en;
            int lasten = -1;
            while (k < str.Length)
            {
                st = str.IndexOf('<', k);

                if (st < 0)
                    break;

                if (lasten > 0)
                {
                    PrintColor(HC_INNERTEXT, str, lasten + 1, st - lasten - 1, ref cur);
                }

                en = str.IndexOf('>', st + 1);
                if (en < 0)
                    break;

                k = en + 1;
                lasten = en;

                if (str[st + 1] == '!' && str[st + 2] == '-' && str[st + 3] == '-')
                {
                    k = str.IndexOf("-->", st + 3) + 2;
                    PrintColor(HC_COMMENT, str, st + 1, k - st - 1, ref cur);
                    PrintColor(HC_NODE, str, k, 1, ref cur);
                    ++k;
                    lasten = k - 1;
                    continue;

                }
                String nodeText = str.Substring(st + 1, en - st - 1);


                bool inString = false;

                int lastSt = -1;
                int state = 0;
                // 0 = before node name
                // 1 = in node name
                // 2 = after node name
                // 3 = in attribute
                // 4 = in string
                int startNodeName = 0, startAtt = 0;
                for (int i = 0; i < nodeText.Length; ++i)
                {
                    if (nodeText[i] == '"')
                        inString = !inString;

                    if (inString && nodeText[i] == '"')
                        lastSt = i;
                    else
                        if (nodeText[i] == '"')
                    {
                        PrintColor(HC_STRING, str, lastSt + st + 2, i - lastSt - 1, ref cur);
                    }

                    switch (state)
                    {
                        case 0:
                            if (!Char.IsWhiteSpace(nodeText, i))
                            {
                                startNodeName = i;
                                state = 1;
                            }
                            break;
                        case 1:
                            if (Char.IsWhiteSpace(nodeText, i))
                            {
                                PrintColor(HC_NODE, str, startNodeName + st, i - startNodeName + 1, ref cur);
                                state = 2;
                            }
                            break;
                        case 2:
                            if (!Char.IsWhiteSpace(nodeText, i))
                            {
                                startAtt = i;
                                state = 3;
                            }
                            break;

                        case 3:
                            if (Char.IsWhiteSpace(nodeText, i) || nodeText[i] == '=')
                            {
                                PrintColor(HC_ATTRIBUTE, str, startAtt + st, i - startAtt + 1, ref cur);
                                state = 4;
                            }
                            break;
                        case 4:
                            if (nodeText[i] == '"' && !inString)
                                state = 2;
                            break;


                    }

                }

                if (state == 1)
                {
                    PrintColor(HC_NODE, str, st + 1, nodeText.Length, ref cur);
                }
            }

            if (cur < str.Length)
            {
                PrintColor(ConsoleColor.DarkGreen, str, cur, str.Length - cur, ref cur);
            }
        }

        static void PrintColor(ConsoleColor color, string str, int begin, int lenght, ref int cur)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                if (cur < begin)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(str.Substring(cur, begin - cur));
                }

                Console.ForegroundColor = color;
                Console.Write(str.Substring(begin, lenght));
                cur = begin + lenght;
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
}
