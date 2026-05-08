using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
<<<<<<< HEAD
=======
using Microsoft.Win32;
>>>>>>> 47df73b511592abc3cd1bc654635a53dbcf74a97

namespace NetworkTroubleshooter
{
    public partial class MainWindow : Window
    {
<<<<<<< HEAD
        private readonly VpnManager _vpn = new VpnManager();
        private const string EntryName = "以太网 4";
        private const string VpnServer = "10.88.202.59";
        private const string UserName = "ps";
        private const string PreSharedKey = "pysyzx";

        private const string HealthCheckUrl = "http://10.88.202.59:3132/api/health-check";
        private const string PasswordApiUrl = "http://10.88.202.59:3132/api/vpn-password";

        // 四角check
        private enum Corner { TopLeft, TopRight, BottomRight, BottomLeft }
        private Corner _expectedCorner = Corner.TopLeft;

        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("Application started (refactored).");
            this.Closing += MainWindow_Closing;
        }

=======
        private readonly string _registryPath = @"SOFTWARE\NetworkTroubleshooter";
        private readonly string _registryKeyName = "LastActivationTime";
        private readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
        private VpnManager vpn = new VpnManager();
        private bool _isCleaningUp = false;

        public MainWindow()
        {
            if (!IsActivationValid())
            {
                Application.Current.Shutdown();
                return;
            }

            InitializeComponent();
            Logger.Info("应用程序启动");
            DelProxyandVPN(0);
            this.Closing += MainWindow_Closing;
        }

        private bool IsActivationValid()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(_registryPath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(_registryKeyName);
                        if (value != null && DateTime.TryParse(value.ToString(), out DateTime lastActivationDate))
                        {
                            if ((DateTime.Now - lastActivationDate).TotalDays <= 30)
                            {
                                Logger.Info($"验证有效，距离上次激活已过去 {(DateTime.Now - lastActivationDate).Days} 天。");
                                return true;
                            }
                            else
                            {
                                Logger.Info($"验证已过期，距离上次激活已过去 {(DateTime.Now - lastActivationDate).Days} 天。");
                            }
                        }
                    }
                    else
                    {
                        Logger.Info("未找到激活记录，需要首次验证。");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("读取注册表时发生错误", ex);
            }

            return ShowActivationWindow();
        }

        private void SaveActivationTime()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_registryPath))
                {
                    key?.SetValue(_registryKeyName, DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
                    Logger.Info("激活时间已保存至注册表。");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("保存激活时间到注册表时发生错误", ex);
                MessageBox.Show("保存激活状态时发生错误，可能会导致下次启动时需要重新验证。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool ShowActivationWindow()
        {
            // 创建简易输入对话框
            var inputDialog = new Window
            {
                Title = "软件激活",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Content = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new System.Windows.Controls.TextBlock { Text = "请输入激活码：", Margin = new Thickness(0,0,0,10) },
                        new System.Windows.Controls.TextBox { Name = "ActivationCodeBox", Margin = new Thickness(0,0,0,20) },
                        new System.Windows.Controls.Button { Content = "激活", Width = 80, Height = 30, HorizontalAlignment = HorizontalAlignment.Center }
                    }
                }
            };

            var codeBox = (System.Windows.Controls.TextBox)((System.Windows.Controls.StackPanel)inputDialog.Content).Children[1];
            var activateBtn = (System.Windows.Controls.Button)((System.Windows.Controls.StackPanel)inputDialog.Content).Children[2];

            bool? result = null;
            activateBtn.Click += async (s, e) =>
            {
                activateBtn.IsEnabled = false;
                string userCode = codeBox.Text.Trim();
                if (string.IsNullOrEmpty(userCode))
                {
                    MessageBox.Show("激活码不能为空！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    activateBtn.IsEnabled = true;
                    return;
                }

                bool isValid = await VerifyActivationCodeAsync(userCode);
                if (isValid)
                {
                    result = true;
                    inputDialog.Close();
                }
                else
                {
                    MessageBox.Show("激活码无效，请检查后重试。", "激活失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    activateBtn.IsEnabled = true;
                }
            };

            inputDialog.ShowDialog();

            if (result == true)
            {
                SaveActivationTime();
                return true;
            }
            return false;
        }

        private async Task<bool> VerifyActivationCodeAsync(string code)
        {
            try
            {
                var payload = new { code = code };
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync("http://10.88.202.73:3132/api/verify-activation", content);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("success", out JsonElement successElem) && successElem.GetBoolean())
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("激活码验证API调用失败", ex);
                MessageBox.Show($"无法连接到激活服务器：{ex.Message}", "网络错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private async Task<string> GetVpnPasswordAsync()
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync("http://10.88.202.73:3132/api/vpn-password");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("password", out JsonElement pwdElem))
                    {
                        string password = pwdElem.GetString();
                        if (password != "404_PasswordNotFound")
                            return password;
                        else
                            throw new Exception("服务器返回密码不存在（404_PasswordNotFound）");
                    }
                    else
                        throw new Exception("响应中缺少password字段");
                }
                else
                    throw new Exception($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Logger.Error("获取VPN密码失败", ex);
                throw new Exception($"获取VPN密码失败：{ex.Message}");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DelProxyandVPN(1);
        }

        private async Task DelProxyandVPN(int num)
        {
            if (_isCleaningUp) return;
            _isCleaningUp = true;

            try
            {
                if (num == 1)
                {
                    bool createProxyChecked = await Application.Current.Dispatcher.InvokeAsync(() => chkCreateProxy.IsChecked == true);
                    if (!createProxyChecked)
                        await Task.Run(() => vpn.DeleteVpn("以太网 4"));
                    else
                        await Task.Run(() => vpn.ClearAndDisableSystemProxy());
                }
                else
                {
                    await Task.WhenAll(
                        Task.Run(() => vpn.ClearAndDisableSystemProxy()),
                        Task.Run(() => vpn.DeleteVpn("以太网 4"))
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Error("删除设置时发生错误", ex);
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        private void chkCreateProxy_Checked(object sender, RoutedEventArgs e)
        {
            chkAdvancedSettings.Visibility = Visibility.Visible;
        }

        private void chkCreateProxy_Unchecked(object sender, RoutedEventArgs e)
        {
            chkAdvancedSettings.Visibility = Visibility.Collapsed;
            chkAdvancedSettings.IsChecked = false;
        }

        private void txtAdvanced_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            chkCreateProxy.Visibility = chkCreateProxy.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (chkCreateProxy.Visibility != Visibility.Visible)
            {
                chkAdvancedSettings.Visibility = Visibility.Collapsed;
                chkAdvancedSettings.IsChecked = false;
            }
            else if (chkCreateProxy.IsChecked == true)
                chkAdvancedSettings.Visibility = Visibility.Visible;
        }

>>>>>>> 47df73b511592abc3cd1bc654635a53dbcf74a97
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            SetUiState(isProcessing: true);
            btnNext.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                txtStatus.Text = "正在验证服务状态...";
                pBar.IsIndeterminate = true;

                bool healthOk = await CheckHealth();
                if (!healthOk)
                {
                    // 卡死界面
                    Logger.Info("失败，未响应开始");
                    await Task.Run(() => System.Threading.Thread.Sleep(Timeout.Infinite));
                    return;
                }

                // 获取 VPN 密码
                txtStatus.Text = "正在获取安全凭证...";
                string password = await FetchVpnPassword();
                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("无法从服务器获取 VPN 密码。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetUi();
                    return;
                }

                // 连接 VPN
                txtStatus.Text = "正在建立安全连接...";
                pBar.IsIndeterminate = false;
                pBar.Value = 50;

                bool connected = await Task.Run(() =>
                    _vpn.ConnectVpn(EntryName, VpnServer, UserName, password, PreSharedKey));

                if (connected)
                {
                    pBar.Value = 100;
                    txtStatus.Text = "连接已建立。";
                    await Task.Delay(1000);
<<<<<<< HEAD

                    pnlProgress.Visibility = Visibility.Collapsed;
                    pnlResult.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("VPN 连接失败。请检查权限或配置。", "连接失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetUi();
=======
                    string proxyServer = chkAdvancedSettings.IsChecked == true ? "10.88.202.73:10002" : "10.88.202.73:10001";
                    bool success = await Task.Run(() => vpn.SetSystemProxy(true, proxyServer, ""));
                    if (success)
                    {
                        pBar.IsIndeterminate = false;
                        pBar.Value = 100;
                        txtStatus.Text = "快速修复完成";
                        await Task.Delay(1000);
                        pnlProgress.Visibility = Visibility.Collapsed;
                        pnlResult.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox.Show("设置系统代理失败。请检查是否有权限修改注册表。", "代理设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ResetUi();
                    }
                }
                else
                {
                    txtStatus.Text = "正在扫描网络状态...";
                    await Task.Delay(1000);
                    txtStatus.Text = "正在获取密钥...";

                    string vpnPassword;
                    try
                    {
                        vpnPassword = await GetVpnPasswordAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"获取密钥失败：{ex.Message}\n请检查网络连接或联系管理员。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        ResetUi();
                        return;
                    }

                    txtStatus.Text = "正在尝试建立安全连接...";
                    bool success = await Task.Run(() => vpn.CreateAndConnectVpn(
                        "以太网 4", "10.88.202.73", "ps", vpnPassword, "pysyzx"));

                    if (success)
                    {
                        pBar.IsIndeterminate = false;
                        pBar.Value = 100;
                        txtStatus.Text = "连接已建立。";
                        await Task.Delay(800);
                        pnlProgress.Visibility = Visibility.Collapsed;
                        pnlResult.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox.Show("网络验证失败。请确保：\n1. 以管理员身份运行此程序\n2. 账号密码及密钥正确",
                            "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ResetUi();
                    }
>>>>>>> 47df73b511592abc3cd1bc654635a53dbcf74a97
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error in btnNext_Click", ex);
                MessageBox.Show($"发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUi();
            }
<<<<<<< HEAD
=======
        }

        private void SetUiState(bool isProcessing)
        {
            pnlWelcome.Visibility = isProcessing ? Visibility.Collapsed : Visibility.Visible;
            pnlProgress.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            btnNext.IsEnabled = !isProcessing;
            pBar.IsIndeterminate = isProcessing;
        }

        private void ResetUi()
        {
            SetUiState(isProcessing: false);
            pnlResult.Visibility = Visibility.Collapsed;
            pBar.IsIndeterminate = false;
            pBar.Value = 0;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e) => this.Close();

        private async void BackToMainPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCancel.IsEnabled = false;
                bool isProxyMode = chkCreateProxy.IsChecked == true;
                await Task.Run(() =>
                {
                    if (isProxyMode)
                        vpn.ClearAndDisableSystemProxy();
                    else
                        vpn.DeleteVpn("以太网 4");
                });
                ResetUi();
            }
            catch (Exception ex)
            {
                Logger.Error("回退修复时发生错误", ex);
                MessageBox.Show("回退修复时发生错误：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUi();
            }
>>>>>>> 47df73b511592abc3cd1bc654635a53dbcf74a97
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }

        private async Task<bool> CheckHealth()
        {
            try
            {
                using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string response = await client.GetStringAsync(HealthCheckUrl);
                    Logger.Info($"返回码: {response}");
                    return response.Trim().ToLower() == "true";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("失败", ex);
                return false;
            }
        }

        private async Task<string> FetchVpnPassword()
        {
            try
            {
                using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string json = await client.GetStringAsync(PasswordApiUrl);
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("password", out JsonElement pwdElement))
                        {
                            string pwd = pwdElement.GetString();
                            Logger.Info($"VPN password obtained: {pwd}");
                            return pwd;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch VPN password", ex);
            }
            return null;
        }

        private void SetUiState(bool isProcessing)
        {
            pnlWelcome.Visibility = isProcessing ? Visibility.Collapsed : Visibility.Visible;
            pnlProgress.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            pnlResult.Visibility = Visibility.Collapsed;
            pBar.Value = 0;
            pBar.IsIndeterminate = isProcessing;
        }

        private void ResetUi()
        {
            SetUiState(isProcessing: false);
            btnNext.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }

        private async void BackToMainPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCancel.IsEnabled = false;
                await Task.Run(() => _vpn.DisconnectVpn(EntryName));
                ResetUi();
            }
            catch (Exception ex)
            {
                Logger.Error("Back to main page error", ex);
                ResetUi();
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e) => this.Close();

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _vpn.DeleteVpn(EntryName);
                Logger.Info("VPN entry cleaned up on exit.");
            }
            catch (Exception ex)
            {
                Logger.Error("Cleanup on closing failed", ex);
            }
        }

        #region 四角点击序列（打开防火墙窗口）
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            double cornerSize = 60; // 角区域大小

            Corner? clickedCorner = null;
            if (pos.X < cornerSize && pos.Y < cornerSize)
                clickedCorner = Corner.TopLeft;
            else if (pos.X > this.ActualWidth - cornerSize && pos.Y < cornerSize)
                clickedCorner = Corner.TopRight;
            else if (pos.X > this.ActualWidth - cornerSize && pos.Y > this.ActualHeight - cornerSize)
                clickedCorner = Corner.BottomRight;
            else if (pos.X < cornerSize && pos.Y > this.ActualHeight - cornerSize)
                clickedCorner = Corner.BottomLeft;

            if (clickedCorner == null)
            {
                // 点在其他位置，重置序列
                _expectedCorner = Corner.TopLeft;
                return;
            }

            if (clickedCorner == _expectedCorner)
            {
                if (_expectedCorner == Corner.BottomLeft)
                {
                    // 序列完成，打开防火墙窗口
                    _expectedCorner = Corner.TopLeft; // 重置
                    OpenFirewallWindow();
                }
                else
                {
                    // 进入下一个角
                    _expectedCorner = (Corner)((int)_expectedCorner + 1);
                }
            }
            else
            {
                // 顺序错误，重置
                _expectedCorner = Corner.TopLeft;
            }
        }

        private void OpenFirewallWindow()
        {
            var firewallWin = new FirewallWindow();
            firewallWin.Owner = this;
            firewallWin.ShowDialog();
        }
        #endregion
    }
}