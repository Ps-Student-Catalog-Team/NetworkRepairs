using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

public class VpnManager
{
    private string vpnName = "System_Network_Fix";
    private string pbkPath = Path.Combine(Path.GetTempPath(), "network_fix.pbk");

    //初始化系统环境
    public void InitializeSystem()
    {
        try
        {
            // 允许 L2TP 穿透 NAT
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PolicyAgent",
                "AssumeUDPEncapsulationContextOnSendRule", 2, RegistryValueKind.DWord);
        }
        catch { /* 需要管理员权限 */ }
    }

    //创建隐藏的 PBK 配置文件
    private void CreatePbk()
    {
        string pbkContent = $@"
[{vpnName}]
MEDIA=rastapi
DEVICE=vpn
Port=VPN2-0
Device=WAN Miniport (L2TP)
SpecificAddress=10.88.202.73
Type=3
AuthRestrictions=0
VpnStrategy=2
IpPrioritizeRemote=1
PresharedKey=pysyzx
";
        File.WriteAllText(pbkPath, pbkContent);
    }

    //连接
    public void Connect(string user, string pass)
    {
        CreatePbk();
        ProcessStartInfo psi = new ProcessStartInfo("rasdial.exe");
        // 关键：静默参数
        psi.Arguments = $"\"{vpnName}\" {user} {pass} /phonebook:\"{pbkPath}\"";
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.UseShellExecute = false;

        Process.Start(psi);
    }

    //断开
    public void Disconnect()
    {
        ProcessStartInfo psi = new ProcessStartInfo("rasdial.exe");
        psi.Arguments = $"\"{vpnName}\" /disconnect /phonebook:\"{pbkPath}\"";
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;

        Process.Start(psi);

        // 顺手删掉临时文件，销毁证据
        if (File.Exists(pbkPath)) File.Delete(pbkPath);
    }
}