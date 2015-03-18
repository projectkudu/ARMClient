using ARMClient.Authentication.Contracts;
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace ArmGuiClient.Models
{
    public class ActionParam
    {
        public string Name { get; set; }
        public string PlaceHolder { get; set; }

        public bool Required { get; set; }
    }

    public class ConfigActioin
    {
        private static readonly JavaScriptSerializer _ser = new JavaScriptSerializer();
        private dynamic _payload;

        public string Name { get; set; }
        public string Template { get; set; }

        public ActionParam[] Params { get; set; }

        public string HttpMethod { get; set; }
        public dynamic Payload
        {
            get
            {
                if (this._payload == null)
                {
                    return "";
                }

                return _ser.Serialize(this._payload);
            }

            set
            {
                this._payload = value;
            }
        }
    }

    public class Profile
    {
        public string Name { get; set; }

        public Dictionary<string, object> DefaultValues { get; set; }

        public string TargetEnvironment { get; set; }
    }

    public class ConfigSettings
    {
        private string _editor;
        private Dictionary<string, object> _defaultValues;

        public string TargetEnvironment { get; set; }

        public bool Verbose { get; set; }

        public string Editor
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this._editor))
                {
                    this._editor = @"%windir%\system32\notepad.exe";
                }

                return Environment.ExpandEnvironmentVariables(this._editor);
            }

            set
            {
                this._editor = value;
            }
        }

        public string[] ApiVersions { get; set; }

        public bool AutoPromptEditor { get; set; }

        [ScriptIgnore]
        public Dictionary<string, object> DefaultValues
        {
            get
            {
                if (this._defaultValues == null)
                {
                    this._defaultValues = new Dictionary<string, object>();
                }

                return this._defaultValues;
            }
            set
            {
                this._defaultValues = value;
            }
        }

        public Profile[] Profiles { get; set; }

        public ConfigActioin[] Actioins { get; set; }

        public AzureEnvironments GetAzureEnvironments()
        {
            AzureEnvironments env;
            if (Enum.TryParse<AzureEnvironments>(this.TargetEnvironment, true, out env))
            {
                return env;
            }

            return AzureEnvironments.Prod;
        }
    }
}
