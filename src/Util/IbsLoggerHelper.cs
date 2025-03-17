using System;
using Debug = System.Diagnostics.Debug;

namespace IronBloodSiege.Util
{
    public static class IbsLoggerHelper
    {
        public static void LogDebug(string context, string message)
        {
            Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [调试] [{context}] {message}");
        }

        public static void LogError(string context, Exception ex)
        {
            Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [错误] [{context}] {ex.Message}");
            if (ex.StackTrace != null)
            {
                Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [堆栈] [{context}] {ex.StackTrace}");
            }
        }

        public static void LogInfo(string message)
        {
            Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [信息] {message}");
        }
    }
} 