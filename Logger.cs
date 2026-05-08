using System;
using System.IO;

namespace NetworkTroubleshooter
{
    public static class Logger
    {
        private static readonly string LogFilePath = 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "troubleshooter.log");

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            WriteLog("ERROR", $"{message} | Exception: {ex}");
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
            catch { /* 忽略日志写入错误 */ }
        }
    }
}