using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

public class VpnManager
{
    private string vpnName = "System_Network_Fix";
    private string pbkPath = Path.Combine(Path.GetTempPath(), "network_fix.pbk");

    // 初始化系统环境（允许 L2TP 穿透 NAT）
    public void InitializeSystem()
    {
        Logger.Info("\n\n程序启动成功ヾ(≧▽≦*)o  初始化系统环境====================");
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PolicyAgent",
                "AssumeUDPEncapsulationContextOnSendRule", 2, RegistryValueKind.DWord);
            Logger.Info("注册表键值 AssumeUDPEncapsulationContextOnSendRule 已设置为 2");
        }
        catch (Exception ex)
        {
            Logger.Warning("设置注册表失败，可能需要管理员权限。错误：" + ex.Message);
        }
    }

    // 创建隐藏的 PBK 配置文件
    private void CreatePbk()
    {
        try
        {
            string pbkContent = @"
[System_Network_Fix]
Encoding=1
PBVersion=8
Type=3
VpnStrategy=2
AuthRestrictions=640
IpPrioritizeRemote=1
MEDIA=rastapi
DEVICE=vpn
PhoneNumber=10.88.202.73
PresharedKey=pysyzx
".TrimStart(); // TrimStart 去除开头空行

            File.WriteAllText(pbkPath, pbkContent, Encoding.Default);
            Logger.Info($"PBK 配置文件已创建: {pbkPath}");
            Logger.Info($"PBK 文件内容:\n{pbkContent}");
        }
        catch (Exception ex)
        {
            Logger.Error("创建 PBK 文件失败", ex);
            throw;
        }
    }
    private static string EscapeCommandLineArg(string arg)
    {
        // 如果包含空格或引号，用引号包裹并转义内部引号
        if (string.IsNullOrEmpty(arg))
            return "\"\"";
        if (arg.Contains('"') || arg.Contains(' '))
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        return arg;
    }

    // 连接 VPN，返回是否成功
    public bool Connect(string user, string pass)
    {
        try
        {
            CreatePbk();

            string args = $"\"{vpnName}\" {EscapeCommandLineArg(user)} {EscapeCommandLineArg(pass)} /phonebook:\"{pbkPath}\"";
            Logger.Info($"准备执行 rasdial，参数: {args}");

            ProcessStartInfo psi = new ProcessStartInfo("rasdial.exe")
            {
                Arguments = args,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(psi))
            {
                if (process == null)
                {
                    Logger.Error("启动 rasdial.exe 失败，进程为空");
                    return false;
                }

                // 异步读取输出避免死锁
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                bool success = process.ExitCode == 0;
                if (success)
                {
                    Logger.Info($"VPN 连接成功: {vpnName}");
                }
                else
                {
                    Logger.Warning($"VPN 连接失败，退出码: {process.ExitCode}\n输出: {output}\n错误: {error}");
                }
                return success;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("VPN 连接过程中发生异常", ex);
            return false;
        }
    }
    // 断开 VPN，返回是否成功
    public bool Disconnect()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("rasdial.exe")
            {
                Arguments = $"\"{vpnName}\" /disconnect /phonebook:\"{pbkPath}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(psi))
            {
                if (process == null)
                {
                    Logger.Error("启动 rasdial.exe 失败，进程为空");
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                bool success = process.ExitCode == 0;
                if (success)
                {
                    Logger.Info($"VPN 已断开: {vpnName}");
                }
                else
                {
                    Logger.Warning($"断开 VPN 失败，退出码: {process.ExitCode}\n输出: {output}\n错误: {error}");
                }

                // 尝试删除临时文件（无论断开是否成功）
                DeletePbkFile();
                return success;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("断开 VPN 过程中发生异常", ex);
            DeletePbkFile(); // 仍尝试清理文件
            return false;
        }
    }

    private void DeletePbkFile()
    {
        try
        {
            if (File.Exists(pbkPath))
            {
                File.Delete(pbkPath);
                Logger.Info($"已删除临时 PBK 文件: {pbkPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"删除临时文件失败: {pbkPath}，错误: {ex.Message}");
        }
    }
}