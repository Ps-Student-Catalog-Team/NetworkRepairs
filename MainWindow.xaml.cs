using System;
using System.Threading.Tasks;
using System.Windows;

namespace NetworkTroubleshooter
{
    public partial class MainWindow : Window
    {
        private VpnManager vpn = new VpnManager();

        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("应用程序启动");
        }

        private void txtAdvanced_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 显示复选框
            chkCreateProxy.Visibility = Visibility.Visible;
            // 可选：点击高级后隐藏文本本身或改变样式，根据需求可保留
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetUiState(isProcessing: true);

                // 如果用户勾选了“创建代理”
                if (chkCreateProxy.IsChecked == true)
                {
                    txtStatus.Text = "正在设置系统代理...";
                    await Task.Delay(1000);

                    // 代理服务器地址使用 VPN 服务器地址，端口固定为 1080（可根据需要调整）
                    string proxyServer = $"10.88.202.73:10001";
                    bool success = await Task.Run(() => vpn.SetSystemProxy(true, proxyServer, ""));

                    if (success)
                    {
                        pBar.IsIndeterminate = false;
                        pBar.Value = 100;
                        txtStatus.Text = "系统代理已启用。";
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
            pBar.IsIndeterminate = false;
            pBar.Value = 0;
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DeleteVpnAndClose();
        }

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e)
        {
            DeleteVpnAndClose();
        }

        private void DeleteVpnAndClose()
        {
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
            finally
            {
                // 关闭窗口
                this.Close(); 
            }
        }

    }
}
