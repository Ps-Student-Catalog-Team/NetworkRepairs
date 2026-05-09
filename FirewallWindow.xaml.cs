using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NetworkTroubleshooter
{
    public partial class FirewallWindow : Window
    {
        private readonly VpnManager_L2TP _vpn = new VpnManager_L2TP();
        private const string FirewallEntryName = "防火墙+++";
        private const string FirewallServer = "10.88.193.112";
        private const string FirewallUser = "admin";
        private const string FirewallPassword = "adm1n5";

        public FirewallWindow()
        {
            InitializeComponent();
            this.Loaded += async (s, e) => await UpdateStatus();
        }

        private async Task UpdateStatus()
        {
            bool connected = await Task.Run(() => _vpn.IsVpnConnected(FirewallEntryName));
            if (connected)
            {
                statusLed.Fill = new SolidColorBrush(Colors.LimeGreen);
                txtStatus.Text = "已连接";
                btnCancel.Content = "断开连接";
            }
            else
            {
                statusLed.Fill = new SolidColorBrush(Colors.Red);
                txtStatus.Text = "未连接";
                btnCancel.Content = "取消";
            }
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            btnConnect.IsEnabled = false;
            btnCancel.IsEnabled = false;
            try
            {
                bool connected = await Task.Run(() =>
                    _vpn.ConnectVpn(FirewallEntryName, FirewallServer,
                        FirewallUser, FirewallPassword));

                if (connected)
                {
                    await UpdateStatus();
                }
                else
                {
                    MessageBox.Show("防火墙 VPN 连接失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnConnect.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private async void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            bool connected = await Task.Run(() => _vpn.IsVpnConnected(FirewallEntryName));
            if (connected)
            {
                // 断开连接
                await Task.Run(() => _vpn.DisconnectVpn(FirewallEntryName));
                await UpdateStatus();
            }
            else
            {
                // 关闭窗口
                this.Close();
            }
        }
    }
}