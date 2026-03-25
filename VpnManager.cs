using DotRas;
using System;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetworkTroubleshooter
{
    public class VpnManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct RASCREDENTIALS
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
        public static extern int RasSetCredentials(string lpszPhonebook, string lpszEntry, ref RASCREDENTIALS lpCredentials, bool fClearCredentials);

        public bool CreateAndConnectVpn(string entryName = "12343" , string serverAddress = "10.88.20.273",  string userName = "ps" , string password = "\\@(^O^)@/", string psk = "pysyzx")
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

                    RasDevice device = RasDevice.GetDeviceByName("WAN Miniport (L2TP)", RasDeviceType.Vpn);
                    RasEntry vpnEntry = RasEntry.CreateVpnEntry(entryName, serverAddress, RasVpnStrategy.L2tpOnly, device);

                    // 核心配置：仅保留不报错的选项
                    vpnEntry.Options.UsePreSharedKey = true;

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();

                    // 写入预共享密钥
                    RASCREDENTIALS creds = new RASCREDENTIALS();
                    creds.dwSize = Marshal.SizeOf(typeof(RASCREDENTIALS));
                    creds.dwMask = RASCM_PreSharedKey;
                    creds.szPassword = psk; 

                    int result = RasSetCredentials(pbPath, entryName, ref creds, false);
                    if (result != 0) return false;

                    using (RasDialer dialer = new RasDialer())
                    {
                        dialer.EntryName = entryName;
                        dialer.PhoneBookPath = pbPath;
                        dialer.Credentials = new NetworkCredential(userName, password);
                        
                        dialer.Dial(); // 同步拨号
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VPN Error: {ex.Message}");
                return false;
            }
        }

        private void FixL2tpRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PolicyAgent", true))
                {
                    if (key != null && key.GetValue("AssumeUDPEncapsulationContextOnSendRule")?.ToString() != "2")
                    {
                        key.SetValue("AssumeUDPEncapsulationContextOnSendRule", 2, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }
    }
}
