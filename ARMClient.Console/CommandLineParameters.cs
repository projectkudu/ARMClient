using System;
using System.Collections.Generic;
using System.Linq;

namespace ARMClient
{
    class CommandLineParameters
    {
        private Dictionary<string, object> _parameters = null;

        public CommandLineParameters(string[] args)
        {
            _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            string parameter = null;
            var index = 0;
            var ret = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    // '-' can't be the first parameter
                    if (index == 0)
                    {
                        throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", arg));
                    }

                    _parameters[arg] = MergeArguments(arg);
                    parameter = arg;
                    index = -1;
                }
                else if (parameter != null)
                {
                    _parameters[parameter] = MergeArguments(parameter, arg);
                    parameter = null;
                }
                else
                {
                    // non '-' should appear before option
                    if (index < 0)
                    {
                        throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", arg));
                    }

                    _parameters[index.ToString()] = MergeArguments(index.ToString(), arg);
                    index++;
                }
            }
        }

        public string Get(object key, string keyName = null, bool requires = true)
        {
            return GetValue<string>(key, keyName, requires);
        }

        public T GetValue<T>(object key, string keyName = null, bool requires = true)
        {
            object value = null;
            if (!_parameters.TryGetValue(key.ToString(), out value))
            {
                if (requires)
                {
                    throw new CommandLineException(String.Format("Parameter '{0}' is required!", keyName ?? key));
                }

                return default(T);
            }

            _parameters.Remove(key.ToString());
            return (T)value;
        }

        public void ThrowIfUnknown()
        {
            if (_parameters.Count > 0)
            {
                var pair = _parameters.First();
                throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", pair.Key.StartsWith("-") ? pair.Key : pair.Value));
            }
        }

        private object MergeArguments(string key, string value = null)
        {
            if (string.Equals(key, "-h", StringComparison.OrdinalIgnoreCase))
            {
                object obj = null;
                if (!_parameters.TryGetValue(key, out obj))
                {
                    obj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }

                var headers = (Dictionary<string, List<string>>)obj;
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split(new[] { ':' }, 2).Select(v => v.Trim()).ToArray();
                    if (parts.Length > 1)
                    {
                        var headerName = parts[0];
                        List<string> values = null;
                        if (!headers.TryGetValue(headerName, out values))
                        {
                            headers[headerName] = values = new List<string>();
                        }

                        var headerValues = parts[1];
                        values.AddRange(headerValues.Split(';').Select(v => v.Trim()).Where(v => !string.IsNullOrEmpty(v)));
                    }
                }

                return headers;
            }

            return value ?? string.Empty;
        }
    }

    class CommandLineException : Exception
    {
        public CommandLineException(string message)
            : base(message)
        {
        }
    }
}
