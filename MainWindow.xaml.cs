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
        }

        // 处理“下一步”点击
        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            pnlWelcome.Visibility = Visibility.Collapsed;
            pnlProgress.Visibility = Visibility.Visible;
            btnNext.IsEnabled = false;
            pBar.IsIndeterminate = true;

            txtStatus.Text = "正在扫描本地网络状态...";
            await Task.Delay(1500);

            txtStatus.Text = "正在尝试重新同步...";
            await Task.Delay(1000);
            
            txtStatus.Text = "正在尝试Internet连接......";
            await Task.Delay(1000);

            // 执行真正的 VPN 连接
            await Task.Run(() => vpn.Connect("ps", "123456"));

            pBar.IsIndeterminate = false;
            pBar.Value = 100;
            pnlProgress.Visibility = Visibility.Collapsed;
            pnlResult.Visibility = Visibility.Visible;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) { this.Close(); }

        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}