using System;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using Debug = System.Diagnostics.Debug;

namespace IronBloodSiege.Util
{
    public static class Logger
    {
        public static void LogDebug(string context, string message)
        {
            #if DEBUG
            try
            {
                // MBDebug输出
                //MBDebug.EchoCommandWindow($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [调试] [{context}] {message}");
                
                // System.Diagnostics.Debug输出
                Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [调试] [{context}] {message}");
                
                // 控制台输出
                //Console.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [调试] [{context}] {message}");
            }
            catch (Exception ex)
            {
                // 输出异常信息
                Debug.WriteLine($"[铁血攻城] 日志错误: {ex.Message}");
                //Console.WriteLine($"[铁血攻城] 日志错误: {ex.Message}");
            }
            #endif
        }

        public static void LogError(string context, Exception ex)
        {
            #if DEBUG
            try
            {
                //MBDebug.EchoCommandWindow($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [错误] [{context}] {ex.Message}");
                Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [错误] [{context}] {ex.Message}");
                if (ex.StackTrace != null)
                {
                    //MBDebug.EchoCommandWindow($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [堆栈] [{context}] {ex.StackTrace}");
                    Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [堆栈] [{context}] {ex.StackTrace}");
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
                //MBDebug.EchoCommandWindow($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [信息] {message}");
                Debug.WriteLine($"[铁血攻城] [{DateTime.Now:HH:mm:ss}] [信息] {message}");
            }
            catch
            {
                // 忽略日志错误
            }
            #endif
        }
    }
} 