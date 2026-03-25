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
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // UI 状态切换
                SetUiState(isProcessing: true);

                txtStatus.Text = "正在扫描网络状态...";
                await Task.Delay(1000);

                txtStatus.Text = "正在尝试建立安全连接...";

                // 执行连接逻辑
                bool success = await Task.Run(() => vpn.CreateAndConnectVpn(
                    "WorkVPN",          // VPN 名称
                    "10.88.202.73",     // 服务器地址
                    "ps",               // 用户名
                    "123",              // 密码
                    "pysyzx"            // 预共享密钥
                ));

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
                    MessageBox.Show("网络验证失败。请确保：\n1. 以管理员身份运行此程序\n2. 账号密码及密钥正确\n3. 若是首次运行，请重启电脑生效注册表设置。",
                        "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetUi();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生非预期错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
        
        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
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
                    MessageBox.Show("网络验证失败。请确保：\n1. 以管理员身份运行此程序\n2. 账号密码及密钥正确\n3. 若是首次运行，请重启电脑以生效注册表设置。",
                        "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetUi();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生非预期错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}                if (success)
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
                    MessageBox.Show("网络验证失败。请确保：\n1. 以管理员身份运行此程序\n2. 账号密码及密钥正确\n3. 若是首次运行，请重启电脑以生效注册表设置。",
                        "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetUi();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生非预期错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void btnCancel_Click(object sender, RoutedEventArgs e) => this.Close();
        private void btnCloseTroubleshooter_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
