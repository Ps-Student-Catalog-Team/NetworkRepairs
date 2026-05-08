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

        public bool ConnectVpn(string entryName, string serverAddress, 
            string userName, string password, string preSharedKey)
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
                    vpnEntry.Options.UsePreSharedKey = true;

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();

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
                        Logger.Error($"RasSetCredentials failed with code {result}");
                        return false;
                    }

                    // 拨号
                    using (RasDialer dialer = new RasDialer())
                    {
                        dialer.EntryName = entryName;
                        dialer.PhoneBookPath = pbPath;
                        dialer.Credentials = new NetworkCredential(userName, password);
                        dialer.Dial();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("VPN connection failed", ex);
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
                Logger.Error("VPN disconnect failed", ex);
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
            catch
            {
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
                Logger.Error("Delete VPN entry failed", ex);
                return false;
            }
        }
    }
}