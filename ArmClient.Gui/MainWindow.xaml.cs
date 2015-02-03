﻿using ARMClient.Authentication;
using ARMClient.Authentication.Contracts;
using ArmGuiClient.Models;
using ArmGuiClient.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuthUtils = ARMClient.Authentication.Utilities.Utils;

namespace ArmGuiClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string _tmpPayloadFile = System.IO.Path.Combine(Environment.CurrentDirectory, "ArmGuiClient.Payload.json");
        private GuiPersistentAuthHelper _authHelper;

        public MainWindow()
        {
            InitializeComponent();
            this.Dispatcher.ShutdownStarted += this.OnApplicationShutdown;
            Logger.Init(this.OutputRTB);
            ConfigSettingFactory.Init();

            try
            {
                this._authHelper = new GuiPersistentAuthHelper(ConfigSettingFactory.ConfigSettings.GetAzureEnvironments());
                this.InitUI();
                if (this.CheckIsLogin())
                {
                    this.PopulateTenant();
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorLn("{0} {1}", ex.Message, ex.StackTrace);
                this.ExecuteBtn.IsEnabled = false;
            }
        }

        private void InitUI()
        {
            // populate api version
            if (ConfigSettingFactory.ConfigSettings.ApiVersions == null
                || ConfigSettingFactory.ConfigSettings.ApiVersions.Length == 0)
            {
                Logger.ErrorLn("Missing api version from config.json");
                return;
            }
            this.ApiVersionCB.Items.Clear();

            foreach (var str in ConfigSettingFactory.ConfigSettings.ApiVersions)
            {
                ApiVersionCB.Items.Add(new ComboBoxItem()
                {
                    Content = str
                });
            }
            this.ApiVersionCB.SelectedIndex = 0;

            // populate actions
            if (ConfigSettingFactory.ConfigSettings.Actioins == null
                || ConfigSettingFactory.ConfigSettings.Actioins.Length == 0)
            {
                Logger.ErrorLn("No action define in config.json");
                return;
            }
            this.ActionCB.Items.Clear();
            foreach (var item in ConfigSettingFactory.ConfigSettings.Actioins)
            {
                this.ActionCB.Items.Add(new ComboBoxItem()
                {
                    Content = item.Name
                });
            }
            this.ActionCB.SelectedIndex = 0;
            this.OutputRTB.Document.Blocks.Clear();

            // bind keyboard short cuts
            var editPayloadCommand = new RoutedCommand();
            editPayloadCommand.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control, "Ctrl + E"));
            this.CommandBindings.Add(new CommandBinding(editPayloadCommand, this.ExecuteEditPayloadCommand));

            var executeArmCommand = new RoutedCommand();
            executeArmCommand.InputGestures.Add(new KeyGesture(Key.Enter, ModifierKeys.Control, "Ctrl + Enter"));
            this.CommandBindings.Add(new CommandBinding(executeArmCommand, this.ExecuteRunArmRequestCommand));

            var editConfigCommand = new RoutedCommand();
            editConfigCommand.InputGestures.Add(new KeyGesture(Key.P, ModifierKeys.Control, "Ctrl + P"));
            this.CommandBindings.Add(new CommandBinding(editConfigCommand, this.ExecuteEditConfigCommand));
        }

        private void PopulateParamsUI(ConfigActioin action)
        {
            this.ParamLV.Items.Clear();
            double textboxWidth = this.ParamLV.Width * 0.7;
            if (action == null || action.Params == null || action.Params.Length == 0)
            {
                return;
            }

            for (int i = 0; i < action.Params.Length; i++)
            {
                ActionParam param = action.Params[i];

                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Vertical;

                Label label = new Label();
                label.Content = param.Name + (param.Required ? "*" : string.Empty);

                TextBox textbox = new TextBox();
                textbox.Name = param.PlaceHolder;
                textbox.Width = textboxWidth;
                textbox.KeyUp += new KeyEventHandler((object sender, KeyEventArgs e) =>
                {
                    this.UpdateCmdText();
                });

                sp.Children.Add(label);
                sp.Children.Add(textbox);

                this.ParamLV.Items.Add(sp);
            }
        }

        private bool CheckIsLogin()
        {
            if (this._authHelper != null && this._authHelper.IsCacheValid())
            {
                this.LoginBtn.IsEnabled = false;
                this.LogoutBtn.IsEnabled = true;
                this.ExecuteBtn.IsEnabled = true;
                return true;
            }
            else
            {
                this.LoginBtn.IsEnabled = true;
                this.LogoutBtn.IsEnabled = false;
                this.ExecuteBtn.IsEnabled = false;
                this.TenantCB.ItemsSource = new object[0];
                this.SubscriptionCB.ItemsSource = new object[0];
                return false;
            }
        }

        private void UpdateCmdText()
        {
            ConfigActioin action = this.GetSelectedAction();
            if (action == null)
            {
                return;
            }

            string cmd = action.Template;
            foreach (var item in this.ParamLV.Items)
            {
                StackPanel wrapperPanel = item as StackPanel;
                if (wrapperPanel != null && wrapperPanel.Children.Count == 2)
                {
                    TextBox tb = wrapperPanel.Children[1] as TextBox;
                    if (tb != null && !string.IsNullOrWhiteSpace(tb.Text))
                    {
                        cmd = cmd.Replace("{" + tb.Name + "}", tb.Text);
                    }
                }
            }

            // api version
            ComboBoxItem apiItem = this.ApiVersionCB.SelectedValue as ComboBoxItem;
            cmd = cmd.Replace("{apiVersion}", apiItem.Content as string);

            // subscription
            string subscriptionId = this.SubscriptionCB.SelectedValue as string;
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                cmd = cmd.Replace("{subscription}", subscriptionId);
            }

            this.CmdText.Text = cmd;
        }

        private void PopulateTenant()
        {
            Dictionary<string, TenantCacheInfo> tenants = this._authHelper.GetTenants();
            this.TenantCB.ItemsSource = tenants.Values;
            this.TenantCB.DisplayMemberPath = "displayName";
            this.TenantCB.SelectedValuePath = "tenantId";
            Logger.InfoLn("{0} tenant found.", tenants.Count);
            if (tenants.Count > 0)
            {
                this.TenantCB.SelectedIndex = 0;
            }
        }

        private ConfigActioin GetSelectedAction()
        {
            if (this.ActionCB == null)
            {
                return null;
            }

            int actionIdx = this.ActionCB.SelectedIndex;

            if (actionIdx > -1)
            {
                return ConfigSettingFactory.ConfigSettings.Actioins[actionIdx];
            }
            else
            {
                return null;
            }
        }

        private async Task RunArmRequest()
        {
            try
            {
                this.ExecuteBtn.IsEnabled = false;
                string path = this.CmdText.Text;
                string subscriptionId = this.SubscriptionCB.SelectedValue as string;
                ConfigActioin action = this.GetSelectedAction();
                Uri uri = AuthUtils.EnsureAbsoluteUri(path, this._authHelper);
                var cacheInfo = await this._authHelper.GetToken(subscriptionId, null);
                var handler = new HttpLoggingHandler(new HttpClientHandler(), ConfigSettingFactory.ConfigSettings.Verbose);
                HttpContent payload = null;
                if (!string.Equals("get", action.HttpMethod, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("delete", action.HttpMethod, StringComparison.OrdinalIgnoreCase))
                {
                    payload = new StringContent(File.ReadAllText(_tmpPayloadFile), Encoding.UTF8, Constants.JsonContentType);
                }

                await AuthUtils.HttpInvoke(uri, cacheInfo, action.HttpMethod, handler, payload);
            }
            catch (Exception ex)
            {
                Logger.ErrorLn("{0} {1}", ex.Message, ex.StackTrace);
            }
            finally
            {
                this.ExecuteBtn.IsEnabled = true;
            }
        }

        private void InvokeEditorToEditPayload()
        {
            try
            {
                ConfigActioin action = this.GetSelectedAction();
                if (string.Equals("get", action.HttpMethod, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("delete", action.HttpMethod, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string payload = action.Payload;
                File.WriteAllText(_tmpPayloadFile, string.IsNullOrWhiteSpace(payload) ? "" : payload);
                Process.Start(ConfigSettingFactory.ConfigSettings.Editor, _tmpPayloadFile);
                Logger.InfoLn("Editing payload in {0} (Ctrl + W)", _tmpPayloadFile);
            }
            catch (Exception ex)
            {
                Logger.ErrorLn("Editor: '{0}'", ConfigSettingFactory.ConfigSettings.Editor);
                Logger.ErrorLn("{0} {1}", ex.Message, ex.StackTrace);
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            await this._authHelper.AcquireTokens();
            if (this.CheckIsLogin())
            {
                this.PopulateTenant();
            }
        }

        private void ActionCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfigActioin action = this.GetSelectedAction();
            this.PopulateParamsUI(action);
            this.UpdateCmdText();

            if (ConfigSettingFactory.ConfigSettings.AutoPromptEditor)
            {
                this.InvokeEditorToEditPayload();
            }
            else
            {
                Logger.WarnLn("Ctrl + W to edit payload.");
            }
        }

        private void TenantCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedTenantId = this.TenantCB.SelectedValue as string;

            if (!string.IsNullOrWhiteSpace(selectedTenantId))
            {
                Dictionary<string, TenantCacheInfo> tenants = this._authHelper.GetTenants();

                TenantCacheInfo tenant = tenants[selectedTenantId];
                this.SubscriptionCB.ItemsSource = tenant.subscriptions;
                this.SubscriptionCB.DisplayMemberPath = "displayName";
                this.SubscriptionCB.SelectedValuePath = "subscriptionId";

                if (tenant.subscriptions.Length > 0)
                {
                    this.SubscriptionCB.SelectedIndex = 0;
                }

                Logger.InfoLn("{0} subscription found under tenant '{1}'", tenant.subscriptions.Length, tenant.displayName);
            }

            this.UpdateCmdText();
        }

        private void SubscriptionCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.UpdateCmdText();
        }

        private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            this._authHelper.ClearTokenCache();
            this.CheckIsLogin();
            Logger.WarnLn("Goodbye!");
        }

        private async void ExecuteBtn_Click(object sender, RoutedEventArgs e)
        {
            await this.RunArmRequest();
        }

        private void ApiVersionCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.UpdateCmdText();
        }

        private void ExecuteEditPayloadCommand(object sender, ExecutedRoutedEventArgs e)
        {
            ConfigActioin action = this.GetSelectedAction();
            if (string.Equals("get", action.HttpMethod, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals("delete", action.HttpMethod, StringComparison.OrdinalIgnoreCase))
            {
                Logger.WarnLn("{0} request doesn`t require any payload", action.HttpMethod);
                return;
            }

            this.InvokeEditorToEditPayload();
        }

        private async void ExecuteRunArmRequestCommand(object sender, ExecutedRoutedEventArgs e)
        {
            await this.RunArmRequest();
        }

        private void ExecuteEditConfigCommand(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start(ConfigSettingFactory.ConfigSettings.Editor, ConfigSettingFactory.ConfigFilePath);
                Logger.InfoLn("Editing {0} (Ctrl + P)", ConfigSettingFactory.ConfigFilePath);
            }
            catch (Exception ex)
            {
                Logger.ErrorLn("Editor: '{0}'", ConfigSettingFactory.ConfigSettings.Editor);
                Logger.ErrorLn("Expecting Config.json in: '{0}'", ConfigSettingFactory.ConfigFilePath);
                Logger.ErrorLn("{0} {1}", ex.Message, ex.StackTrace);
            }
        }

        private void OnApplicationShutdown(object sender, EventArgs arg)
        {
            ConfigSettingFactory.Shutdown();
        }
    }
}
