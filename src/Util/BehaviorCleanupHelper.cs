using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace IronBloodSiege.Util
{
    /// <summary>
    /// 行为清理助手类
    /// </summary>
    public static class BehaviorCleanupHelper
    {
        /// <summary>
        /// 清理Formation相关资源
        /// </summary>
        public static void CleanupFormations(HashSet<Formation> formations, Action<Formation> restoreAction)
        {
            if (formations == null) return;
            
            try
            {
                var formationsToRestore = formations.ToList();
                foreach (var formation in formationsToRestore)
                {
                    if (formation != null)
                    {
                        try
                        {
                            restoreAction?.Invoke(formation);
                        }
                        catch
                        {
                            // 忽略单个Formation的错误
                        }
                    }
                }
            }
            catch
            {
                // 忽略Formation清理的错误
            }
            finally
            {
                formations.Clear();
            }
        }

        /// <summary>
        /// 清理行为状态
        /// </summary>
        public static void CleanupBehaviorState(ref bool isCleanedUp, 
                                              ref float lastMoraleUpdateTime,
                                              ref float lastMessageTime, 
                                              ref float lastRetreatMessageTime,
                                              ref int initialAttackerCount,
                                              ref Mission currentMission,
                                              ref string currentSceneName,
                                              ref Team attackerTeam,
                                              ref Team defenderTeam,
                                              ref HashSet<Formation> formations)
        {
            if (!isCleanedUp)
            {
                lastMoraleUpdateTime = 0f;
                lastMessageTime = 0f;
                lastRetreatMessageTime = 0f;
                initialAttackerCount = 0;
                currentMission = null;
                currentSceneName = null;
                attackerTeam = null;
                defenderTeam = null;
                
                if (formations != null)
                {
                    formations.Clear();
                    formations = null;
                }
                
                isCleanedUp = true;
            }
        }

        /// <summary>
        /// 清理任务结束状态
        /// </summary>
        public static void CleanupMissionEnd(ref bool missionEnding,
                                           ref bool isDisabled,
                                           ref bool pendingDisable,
                                           ref float disableTimer,
                                           ref bool isSiegeScene,
                                           ref int initialAttackerCount,
                                           ref float lastMoraleUpdateTime,
                                           ref float lastMessageTime,
                                           ref float lastRetreatMessageTime,
                                           ref Team attackerTeam,
                                           ref Team defenderTeam,
                                           ref Mission currentMission,
                                           ref string currentSceneName)
        {
            missionEnding = true;
            isDisabled = true;
            pendingDisable = false;
            disableTimer = 0f;
            isSiegeScene = false;
            initialAttackerCount = 0;
            lastMoraleUpdateTime = 0f;
            lastMessageTime = 0f;
            lastRetreatMessageTime = 0f;
            attackerTeam = null;
            defenderTeam = null;
            currentMission = null;
            currentSceneName = null;
        }
    }
} 