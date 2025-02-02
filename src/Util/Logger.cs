using System;
using TaleWorlds.Library;

namespace IronBloodSiege.Util
{
    public static class Logger
    {
        public static void LogDebug(string context, string message)
        {
            #if DEBUG
            try
            {
                TaleWorlds.Library.Debug.Print($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [调试] [{context}] {message}");
            }
            catch
            {
                // 忽略日志错误
            }
            #endif
        }

        public static void LogError(string context, Exception ex)
        {
            #if DEBUG
            try
            {
                TaleWorlds.Library.Debug.Print($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [错误] [{context}] {ex.Message}");
                if (ex.StackTrace != null)
                {
                    TaleWorlds.Library.Debug.Print($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [堆栈] [{context}] {ex.StackTrace}");
                }
            }
            catch
            {
                // 忽略日志错误
            }
            #endif
        }

        public static void LogInfo(string message)
        {
            #if DEBUG
            try
            {
                TaleWorlds.Library.Debug.Print($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [信息] {message}");
            }
            catch
            {
                // 忽略日志错误
            }
            #endif
        }
    }
} 