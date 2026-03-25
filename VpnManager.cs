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

        [Obsolete]
        public bool CreateAndConnectVpn(string entryName, string serverAddress, string userName, string password, string psk)
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

                    vpnEntry.Options.UsePreSharedKey = true;

                    phoneBook.Entries.Add(vpnEntry);
                    vpnEntry.Update();

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
                        
                        dialer.Dial(); 
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
            catch { }
        }

        /// <summary>
        /// 删除指定的 VPN 条目。
        /// </summary>
        /// <param name="entryName">要删除的 VPN 条目名称。</param>
        /// <returns>如果删除成功返回 true，否则返回 false。</returns>
        public bool DeleteVpn(string entryName)
        {
            try
            {
                // 获取用户电话簿路径
                string pbPath = RasPhoneBook.GetPhoneBookPath(RasPhoneBookType.User);

                using (RasPhoneBook phoneBook = new RasPhoneBook())
                {
                    phoneBook.Open(pbPath);

                    // 检查条目是否存在
                    if (!phoneBook.Entries.Contains(entryName))
                        return false;

                    // 断开所有使用该条目的活动连接
                    foreach (RasConnection connection in RasConnection.GetActiveConnections())
                    {
                        if (connection.EntryName == entryName)
                        {
                            try
                            {
                                connection.HangUp();
                                System.Threading.Thread.Sleep(500);
                            }
                            catch
                            {
                                // 忽略单个连接断开时的异常，继续尝试删除
                            }
                        }
                    }

                    // 删除条目
                    phoneBook.Entries.Remove(entryName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("删除VPN条目失败", ex);
                return false;
            }
        }

    }
}
