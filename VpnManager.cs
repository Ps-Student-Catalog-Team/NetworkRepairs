using DotRas;
using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetworkTroubleshooter
{
    public class VpnManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct RASCREDENTIALS
        {
            public int dwSize;
            public int dwMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string szUserName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string szPassword;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            public string szDomain;
        }

        private const int RASCM_PreSharedKey = 0x10;

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        private static extern int RasSetCredentials(
            string lpszPhonebook, 
            string lpszEntry, 
            ref RASCREDENTIALS lpCredentials, 
            bool fClearCredentials);

        // NAT-T 注册表改动记录（用于退出时还原，不留痕迹）
        private bool _natRuleChanged;
        private bool _natRuleExisted;
        private object _natRuleOriginal;

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
                            _natRuleExisted = val != null;
                            _natRuleOriginal = val;
                            key.SetValue("AssumeUDPEncapsulationContextOnSendRule", 2, RegistryValueKind.DWord);
                            _natRuleChanged = true;
                            Logger.Mark("R00");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("R01", ex);
            }
        }

        /// <summary>
        /// 退出前还原 NAT-T 注册表：原来没有该值则删除，原来有则写回原值。
        /// </summary>
        public void RestoreL2tpRegistry()
        {
            if (!_natRuleChanged)
                return;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\PolicyAgent", true))
                {
                    if (key != null)
                    {
                        if (_natRuleExisted)
                            key.SetValue("AssumeUDPEncapsulationContextOnSendRule", _natRuleOriginal);
                        else
                            key.DeleteValue("AssumeUDPEncapsulationContextOnSendRule", false);
                        _natRuleChanged = false;
                        Logger.Mark("R09");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("R02", ex);
            }
        }

        public bool ConnectVpn(string entryName, string serverAddress,
            string userName, string password, string preSharedKey)
        {
            bool entryCreated = false;
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
                    vpnEntry.Options.UsePreSharedKey = true;

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();
                    entryCreated = true;

                    // 设置预共享密钥
                    RASCREDENTIALS creds = new RASCREDENTIALS
                    {
                        dwSize = Marshal.SizeOf(typeof(RASCREDENTIALS)),
                        dwMask = RASCM_PreSharedKey,
                        szPassword = preSharedKey
                    };

                    int result = RasSetCredentials(pbPath, entryName, ref creds, false);
                    if (result != 0)
                    {
                        Logger.Fail($"C02-{result}");
                        phoneBook.Entries.Remove(entryName); // 回滚，不留半成品条目
                        return false;
                    }

                    // 拨号
                    using (RasDialer dialer = new RasDialer())
                    {
                        dialer.EntryName = entryName;
                        dialer.PhoneBookPath = pbPath;
                        dialer.Credentials = new NetworkCredential(userName, password);
                        dialer.Dial();
                        Logger.Mark("C00");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fail("C01", ex);
                if (entryCreated)
                    DeleteVpn(entryName); // 失败回滚，不留下半成品配置
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
                Logger.Fail("D01", ex);
                return false;
            }
        }

        public bool IsVpnConnected(string entryName)
        {
            try
            {
                return RasConnection.GetActiveConnections()
                    .Any(c => c.EntryName == entryName);
            }
            catch (Exception ex)
            {
                Logger.Fail("Q01", ex);
                return false;
            }
        }

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
                Logger.Fail("D02", ex);
                return false;
            }
        }

        public bool SetSystemProxy(string proxyServer, string proxyOverride)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                        key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                        key.SetValue("ProxyOverride", proxyOverride, RegistryValueKind.String);
                        Logger.Mark("P00");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Fail("P01", ex);
                return false;
            }
        }

        public bool ClearAndDisableSystemProxy()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (key != null)
                    {
                        key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                        // 连值一起删除，不留痕迹
                        key.DeleteValue("ProxyServer", false);
                        key.DeleteValue("ProxyOverride", false);
                        Logger.Mark("P03");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Fail("P02", ex);
                return false;
            }
        }
    }
}