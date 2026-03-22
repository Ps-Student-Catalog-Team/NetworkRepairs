using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NetworkTroubleshooter
{
    public partial class MainWindow : Window
    {
        private VpnManager vpn = new VpnManager();

        public MainWindow()
        {
            InitializeComponent();
            // 程序启动时初始化系统设置（可放在后台）
            Task.Run(() => vpn.InitializeSystem());
        }

        // 窗口关闭前断开 VPN
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!vpn.Disconnect())
            {
                //实则不然
                MessageBox.Show("未知错误", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            base.OnClosing(e);
        }

        // 处理“下一步”点击
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // UI 过渡
                pnlWelcome.Visibility = Visibility.Collapsed;
                pnlProgress.Visibility = Visibility.Visible;
                btnNext.IsEnabled = false;
                pBar.IsIndeterminate = true;

                txtStatus.Text = "正在扫描本地网络状态...";
                await Task.Delay(1500);

                txtStatus.Text = "正在尝试重新同步...";
                await Task.Delay(1000);

                txtStatus.Text = "正在尝试 Internet 连接...";
                await Task.Delay(1000);

                // 执行 VPN 连接
                txtStatus.Text = "正在建立安全连接...";
                bool success = await Task.Run(() => vpn.Connect("ps", "\\@(^O^)@/"));

                if (success)
                {
                    pBar.IsIndeterminate = false;
                    pBar.Value = 100;
                    pnlProgress.Visibility = Visibility.Collapsed;
                    pnlResult.Visibility = Visibility.Visible;
                }
                else
                {
                    // VPN连接失败，回退到欢迎界面或显示错误（这下看懂了）
                    MessageBox.Show("网络验证失败，请检查网络或联系管理员。", "网络验证失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    pnlProgress.Visibility = Visibility.Collapsed;
                    pnlWelcome.Visibility = Visibility.Visible;
                    btnNext.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("VPN 连接过程发生未处理异常", ex);
                MessageBox.Show("程序发生错误，请查看日志或联系技术支持。", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // 恢复界面
                pnlProgress.Visibility = Visibility.Collapsed;
                pnlWelcome.Visibility = Visibility.Visible;
                btnNext.IsEnabled = true;
                pBar.IsIndeterminate = false;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}