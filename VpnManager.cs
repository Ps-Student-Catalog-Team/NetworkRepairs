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
            Logger.Info("test");
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

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        // 注册表路径（当前用户）
        private const string InternetSettingsRegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        /// <summary>
        /// 设置系统代理（仅适用于当前用户）
        /// </summary>
        /// <param name="enable">是否启用代理</param>
        /// <param name="proxyServer">代理服务器地址和端口，例如 "127.0.0.1:8080" 或 "http=127.0.0.1:8080;https=127.0.0.1:8080"</param>
        /// <param name="bypassList">不使用代理的地址列表，用分号分隔，例如 "localhost;127.*;192.168.*"</param>
        /// <returns>操作是否成功</returns>
        public bool SetSystemProxy(bool enable, string proxyServer, string bypassList = "")
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InternetSettingsRegPath, true))
                {
                    if (key == null)
                        return false;

                    // 设置代理启用标志
                    key.SetValue("ProxyEnable", enable ? 1 : 0, RegistryValueKind.DWord);

                    if (enable)
                    {
                        // 设置代理服务器地址
                        if (!string.IsNullOrEmpty(proxyServer))
                            key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);

                        // 设置绕过列表
                        if (bypassList != null)
                            key.SetValue("ProxyOverride", bypassList, RegistryValueKind.String);
                    }
                    else
                    {
                        // 清除代理相关设置
                        // key.DeleteValue("ProxyServer", false);
                        // key.DeleteValue("ProxyOverride", false);
                    }
                }

                // 通知系统代理设置已更改
                NotifyProxyChange();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetSystemProxy error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 禁用系统代理（等同于调用 SetSystemProxy(false, null)）
        /// </summary>
        /// <returns>操作是否成功</returns>
        public bool DisableSystemProxy()
        {
            return SetSystemProxy(false, null);
        }

        /// <summary>
        /// 获取当前系统代理设置（仅用于调试/显示）
        /// </summary>
        /// <param name="enabled">是否启用代理</param>
        /// <param name="proxyServer">代理服务器字符串</param>
        /// <param name="bypassList">绕过列表</param>
        public void GetSystemProxySettings(out bool enabled, out string proxyServer, out string bypassList)
        {
            enabled = false;
            proxyServer = string.Empty;
            bypassList = string.Empty;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InternetSettingsRegPath))
                {
                    if (key != null)
                    {
                        enabled = (key.GetValue("ProxyEnable", 0) as int?) == 1;
                        proxyServer = key.GetValue("ProxyServer", string.Empty) as string;
                        bypassList = key.GetValue("ProxyOverride", string.Empty) as string;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSystemProxySettings error: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知 Windows 代理设置已更改，使新设置立即生效
        /// </summary>
        private static void NotifyProxyChange()
        {
            try
            {
                // 通知所有应用程序代理设置已更改
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                // 刷新代理设置
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotifyProxyChange error: {ex.Message}");
            }
        }
        /// <summary>
        /// 清空系统代理设置并禁用代理（删除代理服务器地址和绕过列表）
        /// </summary>
        /// <returns>操作是否成功</returns>
        public bool ClearAndDisableSystemProxy()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(InternetSettingsRegPath, true))
                {
                    if (key == null)
                        return false;

                    // 禁用代理
                    key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);

                    // 删除代理服务器地址和绕过列表（如果存在）
                    key.DeleteValue("ProxyServer", false);  // false 表示如果值不存在也不抛出异常
                    key.DeleteValue("ProxyOverride", false);
                }

                // 通知系统代理设置已更改
                NotifyProxyChange();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearAndDisableSystemProxy error: {ex.Message}");
                return false;
            }
        }
    }
}
