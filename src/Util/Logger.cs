using System;
using System.IO;
using System.Text;
using TaleWorlds.Library;

namespace IronBloodSiege.Util
{
    public static class Logger
    {
#if DEBUG
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "IronBloodSiege_Debug.txt"
        );
        
        private static readonly object LogLock = new object();

        public static void LogError(string context, Exception ex)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                StringBuilder message = new StringBuilder();
                message.AppendLine($"[{timestamp}] ERROR in {context}:");
                message.AppendLine($"Message: {ex.Message}");
                message.AppendLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    message.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                    message.AppendLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                message.AppendLine("----------------------------------------");

                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, message.ToString());
                }
            }
            catch
            {
                // 如果日志记录失败，至少尝试在游戏中显示错误
                try
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Failed to write to debug log: {context}", Color.FromUint(0xFF0000FF)));
                }
                catch
                {
                    // 忽略所有错误
                }
            }
        }

        public static void LogInfo(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] INFO: {message}\n";

                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, logMessage);
                }
            }
            catch
            {
                // 忽略日志记录错误
            }
        }

        public static void LogDebug(string context, string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] DEBUG [{context}]: {message}\n";

                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, logMessage);
                }
            }
            catch
            {
                // 忽略日志记录错误
            }
        }
#else
        // Release版本中的空方法实现
        public static void LogError(string context, Exception ex) { }
        public static void LogInfo(string message) { }
        public static void LogDebug(string context, string message) { }
#endif
    }
} 