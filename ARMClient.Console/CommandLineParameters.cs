using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARMClient
{
    class CommandLineParameters
    {
        private Dictionary<string, string> _parameters = null;

        public CommandLineParameters(string[] args)
        {
            _parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

                    _parameters[arg] = String.Empty;
                    parameter = arg;
                    index = -1;
                }
                else if (parameter != null)
                {
                    _parameters[parameter] = arg;
                    parameter = null;
                }
                else
                {
                    // non '-' should appear before option
                    if (index < 0)
                    {
                        throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", arg));
                    }

                    _parameters[index.ToString()] = arg;
                    index++;
                }
            }
        }

        public string Get(object key, string keyName = null, bool requires = true)
        {
            string value = null;
            if (!_parameters.TryGetValue(key.ToString(), out value))
            {
                if (requires)
                {
                    throw new CommandLineException(String.Format("Parameter '{0}' is required!", keyName ?? key));
                }

                return null;
            }

            _parameters.Remove(key.ToString());
            return value;
        }

        public void ThrowIfUnknown()
        {
            if (_parameters.Count > 0)
            {
                var pair = _parameters.First();
                throw new CommandLineException(String.Format("Parameter '{0}' is invalid!", pair.Key.StartsWith("-") ? pair.Key : pair.Value));
            }
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
