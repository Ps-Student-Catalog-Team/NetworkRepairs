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

        public static string LogPath => LogFilePath;
        
        private static void WriteLog(string level, string message)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入日志失败: {ex.Message}");
            }
        }
    }
}