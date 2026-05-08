using System.Windows;

namespace NetworkTroubleshooter
{
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            // 退出时清理 VPN 条目 (可选)
        }
    }
}