using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NetworkTroubleshooter
{
    public partial class MainWindow : Window
    {
        private VpnManager vpn = new VpnManager();
        private bool _isCleaningUp = false; // 防止重复清理

        public MainWindow() 
        {
            InitializeComponent();
            Logger.Info("\n应用程序启动");
            DelProxyandVPN(0);
            this.Closing += MainWindow_Closing; // 注册关闭事件
        }

        // 窗口关闭前的清理操作
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DelProxyandVPN(1);
        }

        private void DelProxyandVPN(int num)
        {
            if(num == 1)
            {
                if (_isCleaningUp) return; // 避免递归
                _isCleaningUp = true;

                try
                {
                    if (chkCreateProxy.IsChecked != true)
                        vpn.DeleteVpn("以太网 4");
                    else
                        vpn.ClearAndDisableSystemProxy();
                }
                catch (Exception ex)
                {
                    Logger.Error("删除设置时发生错误", ex);
                }
            }
            else
            {
                vpn.ClearAndDisableSystemProxy();
                vpn.DeleteVpn("以太网 4");
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
            chkCreateProxy.Visibility = chkCreateProxy.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (chkCreateProxy.Visibility != Visibility.Visible)
            {
                chkAdvancedSettings.Visibility = Visibility.Collapsed;
                chkAdvancedSettings.IsChecked = false;
            }
            else
            {
                if (chkCreateProxy.IsChecked == true)
                {
                    chkAdvancedSettings.Visibility = Visibility.Visible;
                }
            }
        }
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetUiState(isProcessing: true);

                if (chkCreateProxy.IsChecked == true)
                {
                    txtStatus.Text = "正在进行快速修复...";
                    await Task.Delay(1000);
                    string proxyServer;
                    if (chkAdvancedSettings.IsChecked == true)
                        proxyServer = $"10.88.202.73:10002";
                    else
                        proxyServer = $"10.88.202.73:10001";
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
                        MessageBox.Show("设置系统代理失败。请检查是否有权限修改注册表。",
                            "代理设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ResetUi();
                    }
                }
                else
                {
                    txtStatus.Text = "正在扫描网络状态...";
                    await Task.Delay(1000);

                    txtStatus.Text = "正在尝试建立安全连接...";

                    bool success = await Task.Run(() => vpn.CreateAndConnectVpn(
                        "以太网 4", "10.88.202.73", "ps", @"\@(^O^)@/", "pysyzx"));

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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生非预期错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error("操作发生非预期错误", ex);
                ResetUi();
            }
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
                ResetUi(); // 即使出错也尝试返回主页
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }
    }
}