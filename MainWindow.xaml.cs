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

        // 四角点击序列
        private enum Corner { TopLeft, TopRight, BottomRight, BottomLeft }
        private Corner _expectedCorner = Corner.TopLeft;

        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("应用程序启动");
            this.Closing += MainWindow_Closing;
        }

        #region 主流程：下一步
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
                    Logger.Info("健康检查返回 false，UI 将卡死。");
                    await Task.Run(() => System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite));
                    //This intentionally freezes the UI thread indefinitely when health check fails.
                    //This creates an unresponsive application that users cannot close normally.
                    //Consider showing an error message and either allowing the user to retry or gracefully closing the application instead.
                    return;
                }

                txtStatus.Text = "正在获取安全凭证...";
                string password = await FetchVpnPassword();
                if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("无法从服务器获取 VPN 密码。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetUi();
                    return;
                }

                txtStatus.Text = "正在建立安全连接...";
                pBar.IsIndeterminate = false;
                pBar.Value = 50;

                bool connected = await Task.Run(() =>
                    _vpn.ConnectVpn(EntryName, VpnServer, UserName, password, PreSharedKey));

                if (connected)
                {
                    pBar.Value = 100;
                    txtStatus.Text = "连接已建立";
                    await Task.Delay(800);

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
                Logger.Error("btnNext_Click 异常", ex);
                MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Logger.Info($"健康检查响应：{response}");
                    return response.Trim().ToLower() == "true";
                }
            }
            catch (Exception ex)
            {
                Logger.Error("健康检查失败", ex);
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
                            Logger.Info($"获取到 VPN 密码：{pwd}");
                            return pwd;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("获取 VPN 密码失败", ex);
            }
            return null;
        }

        private void SetUiState(bool isProcessing)
        {
            pnlWelcome.Visibility = isProcessing ? Visibility.Collapsed : Visibility.Visible;
            pnlProgress.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            pnlResult.Visibility = Visibility.Collapsed;
            pBar.IsIndeterminate = isProcessing;
            pBar.Value = 0;
        }

        private void ResetUi()
        {
            SetUiState(isProcessing: false);
            btnNext.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }

        private async void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e)
        {

            btnCloseTroubleshooter.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    _vpn.DisconnectVpn(EntryName);
                    _vpn.DeleteVpn(EntryName);
                });
                await Task.Delay(2000); // 这里其实不需要，后面改
                //Using an arbitrary 2-second delay to 'ensure operation completes' is unreliable. The DisconnectVpn and DeleteVpn methods already run synchronously in Task.Run, so the delay is unnecessary.
                //If there's a specific async operation that needs completion, await it directly rather than using a fixed delay.
                Logger.Info("已清理 VPN 连接，即将关闭程序。");
            }
            catch (Exception ex)
            {
                Logger.Error("关闭时清理 VPN 失败", ex);
            }
            finally
            {
                this.Close();
            }
        }

        private async void btnBrowseOptions_Click(object sender, RoutedEventArgs e)
        {
            // 断开并清理 VPN，返回欢迎首页
            btnBrowseOptions.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    _vpn.DisconnectVpn(EntryName);
                    _vpn.DeleteVpn(EntryName);
                });
                await Task.Delay(1500);
                //Similar to Comment 6, this arbitrary 1.5-second delay is unreliable and unnecessary since the cleanup operations in Task.Run are already synchronous.
                //Remove this delay and rely on the Task.Run completion.
                Logger.Info("已清理 VPN 连接，返回首页。");
            }
            catch (Exception ex)
            {
                Logger.Error("返回首页清理 VPN 失败", ex);
            }
            finally
            {
                ResetUi();
                btnBrowseOptions.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region 窗口关闭清理
        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //The async void event handler performs await Task.Run but the window will close before cleanup completes.
        //Consider setting e.Cancel = true, performing cleanup, then manually closing the window, or use a synchronous blocking approach to ensure cleanup finishes before the window closes.
        {
            try
            {
                await Task.Run(() =>
                {
                    _vpn.DisconnectVpn(EntryName);
                    _vpn.DeleteVpn(EntryName);
                });
                Logger.Info("窗口关闭时已清理 VPN。");
            }
            catch (Exception ex)
            {
                Logger.Error("窗口关闭清理异常", ex);
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            double cornerSize = 60;

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
                _expectedCorner = Corner.TopLeft;
                return;
            }

            if (clickedCorner == _expectedCorner)
            {
                if (_expectedCorner == Corner.BottomLeft)
                {
                    _expectedCorner = Corner.TopLeft;
                    OpenFirewallWindow();
                }
                else
                {
                    _expectedCorner = (Corner)((int)_expectedCorner + 1);
                }
            }
            else
            {
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