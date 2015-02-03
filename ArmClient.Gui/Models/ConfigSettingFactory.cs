
using ArmGuiClient.Utils;
using System;
using System.IO;
using System.Timers;
using System.Web.Script.Serialization;
using System.Windows;
namespace ArmGuiClient.Models
{
    internal class ConfigSettingFactory
    {
        public static readonly string ConfigFilePath = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
        private static ConfigSettings _settingInstance;
        private static FileSystemWatcher _configWatcher;
        private static Timer _refreshTimer;

        public static void Init()
        {
            _configWatcher = new FileSystemWatcher();
            _configWatcher.Path = System.IO.Path.GetDirectoryName(ConfigFilePath);
            _configWatcher.Filter = System.IO.Path.GetFileName(ConfigFilePath);
            _configWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _configWatcher.Changed += new FileSystemEventHandler(AutoUpdateConfigOnChanged);
            _configWatcher.EnableRaisingEvents = true;

            // https://msdn.microsoft.com/en-us/library/xcc1t119(v=vs.71).aspx
            // FileSystemWatcher changed event will be raised multiple time
            // delay action by 1 seconds to wait till all events have been raised
            _refreshTimer = new Timer();
            _refreshTimer.AutoReset = false;
            _refreshTimer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            _refreshTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                try
                {
                    _settingInstance = GetConfigSettings();
                    Logger.InfoLn("Changes are detected from config.json. Setting updated.");
                }
                catch (Exception ex)
                {
                    Logger.ErrorLn("Changes are detected from config.json. {0} {1}", ex.Message, ex.StackTrace);
                }
            };
        }

        public static void Shutdown()
        {
            if (_configWatcher != null)
            {
                _configWatcher.Dispose();
            }

            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
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
            _refreshTimer.Start();
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
