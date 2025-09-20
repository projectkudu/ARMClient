//------------------------------------------------------------------------------
// <copyright file="AzAuthHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using ARMClient.Authentication.Contracts;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;

namespace ARMClient.Authentication.AADAuthentication
{
    public static class AzAuthHelper
    {
        public static AzCloudEnvironment GetEnvironment() => Run<AzCloudEnvironment>("cloud show");

        public static AzAccessToken GetToken(string resource) => Run<AzAccessToken>($"account get-access-token --resource {resource}");

        private static T Run<T>(string args)
        {
            var azCmd = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft SDKs\Azure\CLI2\wbin\az.cmd");
            if (!File.Exists(azCmd))
            {
                throw new InvalidOperationException("Azure cli is required.  Please download and install from https://aka.ms/InstallAzureCliWindows");
            }

            var processInfo = new ProcessStartInfo(azCmd, args);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            // start
            var process = Process.Start(processInfo);
            var processName = process.ProcessName;
            var processId = process.Id;

            // hook process event
            var processEvent = new ManualResetEvent(true);
            processEvent.SafeWaitHandle = new SafeWaitHandle(process.Handle, false);

            var stdOutput = new StringBuilder();
            DataReceivedEventHandler stdHandler = (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null)
                {
                    stdOutput.Append(e.Data.Trim());
                }
            };

            // hook stdout and stderr
            process.OutputDataReceived += stdHandler;
            process.BeginOutputReadLine();
            process.ErrorDataReceived += stdHandler;
            process.BeginErrorReadLine();

            // wait for ready
            Console.Write($"Executing az {args} ... ");
            processEvent.WaitOne();
            if (process.ExitCode != 0 || !stdOutput.ToString().StartsWith("{"))
            {
                // if success, it contains the list of subscriptions
                Console.WriteLine($"exit with {process.ExitCode}, {stdOutput}");

                throw new InvalidOperationException("Process exit with " + process.ExitCode);
            }

            Console.WriteLine("successful");
            return JsonConvert.DeserializeObject<T>(stdOutput.ToString());
        }
    }
}