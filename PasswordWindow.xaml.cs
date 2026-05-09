using System.Windows;

namespace NetworkTroubleshooter
{
    public partial class PasswordWindow : Window
    {
        private const string CorrectPassword = "admin123";

        public PasswordWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            string inputPassword = txtPassword.Password;

            if (string.IsNullOrEmpty(inputPassword))
            {
                MessageBox.Show("请输入密码。", "输入不能为空", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }

            if (inputPassword == CorrectPassword)
            {
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("密码错误，请重新输入。", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
