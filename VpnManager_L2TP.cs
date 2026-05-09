using DotRas;
using System;
using System.Linq;
using System.Net;
using Microsoft.Win32;

namespace NetworkTroubleshooter
{
    public class VpnManager_L2TP
    {
        /// <summary>
        /// 修复 L2TP 注册表以允许 NAT 穿越
        /// </summary>
        public void FixL2tpRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\PolicyAgent", true))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AssumeUDPEncapsulationContextOnSendRule");
                        if (val == null || val.ToString() != "2")
                        {
                            key.SetValue("AssumeUDPEncapsulationContextOnSendRule", 2, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch { /* 忽略权限不足等异常 */ }
            //Empty catch block silently swallows all exceptions, making debugging difficult. Consider logging the exception even if
            //you choose to continue execution, or at minimum catch specific exception types (e.g., UnauthorizedAccessException, SecurityException) rather than catching all exceptions.
        }

        /// <summary>
        /// 创建 L2TP VPN 条目并拨号连接（仅使用用户名+密码，无预共享密钥）
        /// </summary>
        public bool ConnectVpn(string entryName, string serverAddress, 
            string userName, string password)
        {
            try
            {
                FixL2tpRegistry();

                string pbPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);
                using (RasPhoneBook phoneBook = new RasPhoneBook())
                {
                    phoneBook.Open(pbPath);

                    if (phoneBook.Entries.Contains(entryName))
                        phoneBook.Entries.Remove(entryName);

                    RasDevice device = RasDevice.GetDeviceByName(
                        "WAN Miniport (L2TP)", RasDeviceType.Vpn);

                    RasEntry vpnEntry = RasEntry.CreateVpnEntry(
                        entryName, serverAddress, RasVpnStrategy.L2tpOnly, device);

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();

                    using (RasDialer dialer = new RasDialer())
                    {
                        dialer.EntryName = entryName;
                        dialer.PhoneBookPath = pbPath;
                        dialer.Credentials = new NetworkCredential(userName, password);
                        dialer.Dial();
                        //The comment suggests using Task.Run externally, but this synchronous call is already inside a Task.Run at the call site (line 69 in MainWindow.xaml.cs shows await Task.Run(...)). The
                        //comment should either be removed or clarified to avoid confusion about the threading context.
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("L2TP VPN connection failed", ex);
                return false;
            }
        }

        public bool DisconnectVpn(string entryName)
        {
            try
            {
                bool disconnected = false;
                foreach (RasConnection connection in RasConnection.GetActiveConnections())
                {
                    if (connection.EntryName == entryName)
                    {
                        connection.HangUp();
                        disconnected = true;
                    }
                }
                return disconnected;
            }
            catch (Exception ex)
            {
                Logger.Error("L2TP VPN disconnect failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 检查指定名称的 VPN 当前是否已连接
        /// </summary>
        public bool IsVpnConnected(string entryName)
        {
            try
            {
                return RasConnection.GetActiveConnections()
                    .Any(c => c.EntryName == entryName);
            }
            catch
            {
                //Empty catch block swallows all exceptions without logging. Consider logging the exception before returning false to aid in troubleshooting connection status checks.
                return false;
            }
        }

        /// <summary>
        /// 断开并删除 VPN 条目（用于清理）
        /// </summary>
        public bool DeleteVpn(string entryName)
        {
            try
            {
                DisconnectVpn(entryName);
                string pbPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);
                using (RasPhoneBook phoneBook = new RasPhoneBook())
                {
                    phoneBook.Open(pbPath);
                    if (phoneBook.Entries.Contains(entryName))
                    {
                        phoneBook.Entries.Remove(entryName);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("L2TP VPN entry delete failed", ex);
                return false;
            }
        }
    }
}