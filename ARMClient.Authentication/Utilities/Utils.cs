using System;
using System.Diagnostics;

namespace ARMClient.Authentication.Utilities
{
    public static class Utils
    {
        static TraceListener _traceListener;

        public static TraceListener Trace
        {
            get { return _traceListener ?? DefaultTraceListener.Default; }
        }

        public static void SetTraceListener(TraceListener listener)
        {
            _traceListener = listener;
        }

        class DefaultTraceListener : TraceListener
        {
            public readonly static TraceListener Default = new DefaultTraceListener();

            public override void Write(string message)
            {
                System.Diagnostics.Trace.Write(message);
            }

            public override void WriteLine(string message)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }
        }
    }
}
