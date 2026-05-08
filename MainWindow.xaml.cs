using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NetworkTroubleshooter
{
    public partial class MainWindow : Window
    {
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

                    pnlProgress.Visibility = Visibility.Collapsed;
                    pnlResult.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("VPN 连接失败。请检查权限或配置。", "连接失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetUi();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error in btnNext_Click", ex);
                MessageBox.Show($"发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUi();
            }
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