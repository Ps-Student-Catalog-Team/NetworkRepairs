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

                    // 如果条目已存在，先删除以更新配置
                    if (phoneBook.Entries.Contains(entryName))
                        phoneBook.Entries.Remove(entryName);

                    // 获取 WAN Miniport (L2TP) 设备
                    RasDevice device = RasDevice.GetDeviceByName(
                        "WAN Miniport (L2TP)", RasDeviceType.Vpn);

                    // 创建 L2TP 条目，UsePreSharedKey 默认为 false
                    RasEntry vpnEntry = RasEntry.CreateVpnEntry(
                        entryName, serverAddress, RasVpnStrategy.L2tpOnly, device);

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();

                    // 直接使用用户名和密码拨号
                    using (RasDialer dialer = new RasDialer())
                    {
                        dialer.EntryName = entryName;
                        dialer.PhoneBookPath = pbPath;
                        dialer.Credentials = new NetworkCredential(userName, password);
                        dialer.Dial(); // 同步拨号，建议在外层用 Task.Run 调用
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

        /// <summary>
        /// 断开指定名称的 VPN 连接（不删除条目）
        /// </summary>
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