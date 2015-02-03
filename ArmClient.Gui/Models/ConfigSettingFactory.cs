
using ArmGuiClient.Utils;
using System;
using System.IO;
using System.Web.Script.Serialization;
namespace ArmGuiClient.Models
{
    internal class ConfigSettingFactory
    {
        private static readonly string ConfigFilePath = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
        private static ConfigSettings _settingInstance;
        private static FileSystemWatcher _configWatcher;

        public static void Init()
        {
            _configWatcher = new FileSystemWatcher();
            _configWatcher.Path = System.IO.Path.GetDirectoryName(ConfigFilePath);
            _configWatcher.Filter = System.IO.Path.GetFileName(ConfigFilePath);
            _configWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _configWatcher.Changed += new FileSystemEventHandler(AutoUpdateConfigOnChanged);
            _configWatcher.EnableRaisingEvents = true;
        }

        public static void Shutdown()
        {
            if (_configWatcher != null)
            {
                _configWatcher.Dispose();
            }
        }

        public static ConfigSettings ConfigSettings
        {
            get
            {
                if (_settingInstance == null)
                {
                    _settingInstance = GetConfigSettings();
                }

                return _settingInstance;
            }
        }

        public static void Refresh()
        {
            try
            {
                _settingInstance = GetConfigSettings();
            }
            catch (Exception ex)
            {
            }            
        }

        private static ConfigSettings GetConfigSettings()
        {
            var ser = new JavaScriptSerializer();
            return ser.Deserialize<ConfigSettings>(File.ReadAllText(ConfigFilePath));
        }

        private static void AutoUpdateConfigOnChanged(object source, FileSystemEventArgs e)
        {
            Refresh();
        }
    }
}
