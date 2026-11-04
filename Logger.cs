using System;
using System.Diagnostics;
using System.IO;

namespace NetworkTroubleshooter
{
    /// <summary>
    /// 诊断日志
    /// 为降低敏感性，日志只记录事件代号，不记录服务器地址、凭据、
    /// 条目名以及任何能暴露程序用途的字样。文件名同样去特征化。
    ///
    /// 代号对照（仅源码可见）：
    ///   B01 启动自检开始      B02 发现残留并已清除   B03 启动自检异常   B04 无残留
    ///   H01 前置检测未通过    K01 凭证获取失败
    ///   C00 通道建立成功      C01 通道建立失败       C02-xx 密钥写入失败(RAS错误码)
    ///   D01 通道断开失败      D02 配置删除失败       Q01 状态查询失败
    ///   P00 策略已应用        P01 策略应用失败       P02 策略清除失败    P03 策略已清除
    ///   R00 环境参数已调整    R01 参数调整失败       R02 参数还原失败    R09 参数已还原
    ///   G01 主流程未处理异常  W01 退出清理完成       W02 退出清理异常
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = ResolveLogPath();

        /// <summary>
        /// 默认写入 %APPDATA%\repair\diagcache.dat（即 AppData\Roaming\repair）。
        /// 不使用 AppDomain.BaseDirectory：单文件发布时它指向运行时临时解压
        /// 目录（%TEMP%\.net\...），日志会散落且被 Temp 清理。
        /// 目录不存在会自动创建；如需换位置，可用 DIAGCACHE_PATH 环境变量
        /// 指定完整文件路径（支持 %VAR% 形式的变量）。
        /// </summary>
        private static string ResolveLogPath()
        {
            string custom = Environment.GetEnvironmentVariable("DIAGCACHE_PATH");
            if (!string.IsNullOrWhiteSpace(custom))
                custom = Environment.ExpandEnvironmentVariables(custom);

            string path;
            if (!string.IsNullOrWhiteSpace(custom))
            {
                path = custom;
            }
            else
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                path = !string.IsNullOrEmpty(roaming)
                    ? Path.Combine(roaming, "repair", "diagcache.dat")
                    : Path.Combine(GetExeDirectory(), "diagcache.dat");
            }

            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }
            catch
            {
                // 目录创建失败由 WriteLog 的异常分支处理，不影响主流程
            }

            return path;
        }

        private static string GetExeDirectory()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir))
                    return dir;
            }
            catch
            {
                // 取不到宿主路径时退回下面的默认位置
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>记录一个普通事件代号。</summary>
        public static void Mark(string code)
        {
            WriteLog("M", code, null);
        }

        /// <summary>记录一个失败事件代号，可附带异常（仅保留 HResult 低 16 位）。</summary>
        public static void Fail(string code, Exception ex = null)
        {
            WriteLog("F", code, ex);
        }

        public static string LogPath => LogFilePath;

        private static void WriteLog(string kind, string code, Exception ex)
        {
            try
            {
                string detail = ex == null ? "" : $":{ex.HResult & 0xFFFF:X4}";
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{kind}] {code}{detail}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logEntry);
            }
            catch (Exception writeEx)
            {
                // 写入失败不影响主流程；仅输出到调试器，不落盘、不留痕迹
                System.Diagnostics.Debug.WriteLine($"log write skipped: {writeEx.HResult:X8}");
            }
        }
    }
}
