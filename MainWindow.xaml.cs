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
        private const string ServerIp = "10.88.202.59";
        private const string EntryName = "以太网 4";
        private const string VpnServer = ServerIp;
        private const string UserName = "ps";
        private const string PreSharedKey = "pysyzx";

        private const string HealthCheckUrl = "http://" + ServerIp + ":3232/api/auth/check";
        private const string PasswordApiUrl = "http://" + ServerIp + ":3132/api/Vpn-Password";

        // 记录本次会话实际应用过的配置，清理时以它为准（而非复选框状态）
        private bool _vpnApplied;
        private bool _proxyApplied;
        private bool _cleanupDone;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;

            // 启动自检：若存在同名配置（上次异常退出的残留），直接删除
            _ = Task.Run(() =>
            {
                try
                {
                    Logger.Mark("B01");
                    bool removed = _vpn.DeleteVpn(EntryName);
                    Logger.Mark(removed ? "B02" : "B04");
                }
                catch (Exception ex)
                {
                    Logger.Fail("B03", ex);
                }
            });
        }

        #region 主流程：下一步
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            SetUiState(isProcessing: true);
            btnNext.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                if (chkAdvancedFix.IsChecked == true)
                {
                    txtStatus.Text = "正在设置代理...";
                    pBar.IsIndeterminate = false;
                    pBar.Value = 50;

                    bool success = await Task.Run(() =>
                        _vpn.SetSystemProxy(ServerIp + ":10002", ""));

                    if (success)
                    {
                        _proxyApplied = true;
                        pBar.Value = 100;
                        txtStatus.Text = "代理设置完成";
                        await Task.Delay(800);

                        pnlProgress.Visibility = Visibility.Collapsed;
                        pnlResult.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ResetUi();
                    }
                }
                else
                {
                    txtStatus.Text = "正在验证服务状态...";
                    pBar.IsIndeterminate = true;
                    (bool healthOk, string response) = await CheckHealth();
                    if (!healthOk)
                    {
                        Logger.Fail("H01");
                        ResetUi();
                        return;
                    }

                    txtStatus.Text = "正在获取安全凭证...";
                    string password = await FetchVpnPassword();
                    if (string.IsNullOrEmpty(password))
                    {
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
                        _vpnApplied = true;
                        pBar.Value = 100;
                        txtStatus.Text = "连接已建立";
                        await Task.Delay(800);

                        pnlProgress.Visibility = Visibility.Collapsed;
                        pnlResult.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ResetUi();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("G01", ex);
                ResetUi();
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }
        private async Task<(bool, string)> CheckHealth()
        {
            try
            {
                using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    string response = await client.GetStringAsync(HealthCheckUrl);
                    return (response.Trim().ToLower() == "true", response);
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("H01", ex);
                return (false, ex.Message);
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
                            return pwd;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("K01", ex);
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

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e)
        {
            // 统一走 Closing 的清理路径，只关窗，避免重复清理
            this.Close();
        }

        private async void btnBrowseOptions_Click(object sender, RoutedEventArgs e)
        {
            btnBrowseOptions.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    if (_vpnApplied)
                    {
                        _vpn.DisconnectVpn(EntryName);
                        _vpn.DeleteVpn(EntryName);
                        _vpnApplied = false;
                    }
                    if (_proxyApplied)
                    {
                        _vpn.ClearAndDisableSystemProxy();
                        _proxyApplied = false;
                    }
                });
            }
            finally
            {
                // 回到真正的初始选择状态
                chkAdvancedFix.IsChecked = false;
                chkAdvancedFix.Visibility = Visibility.Collapsed;
                ResetUi();
                btnBrowseOptions.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // 统一走 Closing 的清理路径，只关窗，避免重复清理
            this.Close();
        }

        private void txtAdvanced_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            chkAdvancedFix.Visibility = chkAdvancedFix.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        #endregion

        #region 窗口关闭清理
        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_cleanupDone)
                return;

            // 先拦住关闭，等清理完成后再真正关窗，防止进程提前退出留下痕迹
            e.Cancel = true;
            try
            {
                await Task.Run(() =>
                {
                    if (_vpnApplied)
                    {
                        _vpn.DisconnectVpn(EntryName);
                        _vpn.DeleteVpn(EntryName);
                        _vpnApplied = false;
                    }
                    if (_proxyApplied)
                    {
                        _vpn.ClearAndDisableSystemProxy();
                        _proxyApplied = false;
                    }
                    // 无论哪种模式，都还原 NAT-T 注册表改动
                    _vpn.RestoreL2tpRegistry();
                });
                Logger.Mark("W01");
            }
            catch (Exception ex)
            {
                Logger.Fail("W02", ex);
            }
            finally
            {
                _cleanupDone = true;
                this.Close();
            }
        }

        #endregion
    }
}
