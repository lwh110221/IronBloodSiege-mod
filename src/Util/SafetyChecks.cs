using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System;
using System.Linq;

namespace IronBloodSiege.Util
{
    public static class SafetyChecks
    {
        /// <summary>
        /// 检查Agent是否有效
        /// </summary>
        public static bool IsValidAgent(Agent agent)
        {
            try
            {
                if (agent == null) return false;

                bool isHuman = agent.IsHuman;
                bool isActive = agent.IsActive();
                bool isNotPlayerControlled = !agent.IsPlayerControlled;
                bool isValid = isHuman && isActive && isNotPlayerControlled;

                #if DEBUG
                if (!isValid)
                {
                    Logger.LogDebug("Agent验证", 
                        $"Agent验证失败 - 是人类: {isHuman}, " +
                        $"是活跃的: {isActive}, " +
                        $"不是玩家控制的: {isNotPlayerControlled}");
                }
                #endif

                return isValid;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查Formation是否有效
        /// </summary>
        public static bool IsValidFormation(Formation formation)
        {
            try
            {
                if (formation == null) return false;
                if (!IsMissionValid()) return false;

                bool hasUnits = formation.CountOfUnits > 0;
                bool hasValidTeam = formation.Team == Mission.Current.AttackerTeam;

                #if DEBUG
                if (!hasValidTeam || !hasUnits)
                {
                    Logger.LogDebug("Formation验证", 
                        $"Formation验证失败 - " +
                        $"有单位: {hasUnits}, " +
                        $"Formation队伍: {formation.Team?.Side}, " +
                        $"Expected队伍: {Mission.Current?.AttackerTeam?.Side}");
                }
                #endif

                return hasUnits && hasValidTeam;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全检查Mission.Current是否有效
        /// </summary>
        public static bool IsMissionValid()
        {
            try
            {
                return Mission.Current != null && 
                       !Mission.Current.IsMissionEnding;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全检查攻城战场景
        /// </summary>
        public static bool IsSiegeSceneValid()
        {
            try
            {
                if (!IsMissionValid()) 
                    return false;

                var mission = Mission.Current;
                var sceneName = mission.SceneName?.ToLowerInvariant() ?? string.Empty;
                
                #if DEBUG
                Logger.LogDebug("场景验证", 
                    $"检查场景 - 模式: {mission.Mode}, " +
                    $"场景: {sceneName}, " +
                    $"有防守方: {mission.DefenderTeam != null}, " +
                    $"有进攻方: {mission.AttackerTeam != null}, " +
                    $"是攻城战: {mission.IsSiegeBattle}, " +
                    $"IsSallyOutBattle: {mission.IsSallyOutBattle}, " +
                    $"MissionTime: {mission.CurrentTime:F2}, " +
                    $"MissionEnded: {mission.MissionEnded}, " +
                    $"BattleInRetreat: {mission.CheckIfBattleInRetreat()}");
                #endif

                // 允许Battle和Deployment两种模式
                bool isValidMode = mission.Mode == MissionMode.Battle || 
                                 mission.Mode == MissionMode.Deployment;

                bool hasValidSceneName = !string.IsNullOrEmpty(sceneName) &&
                                       (sceneName.Contains("siege") || 
                                        sceneName.Contains("castle") ||
                                        sceneName.Contains("town") ||
                                        mission.IsSiegeBattle ||
                                        mission.IsSallyOutBattle);

                bool hasValidTeams = mission.DefenderTeam != null &&
                                   mission.AttackerTeam != null;

                bool isValidSiegeScene = isValidMode && hasValidSceneName && hasValidTeams;

                #if DEBUG
                if (!isValidSiegeScene)
                {
                    Logger.LogDebug("场景验证", 
                        $"场景验证失败 - 允许模式: {isValidMode}, " +
                        $"场景名称: {hasValidSceneName}, " +
                        $"队伍: {hasValidTeams}, " +
                        $"当前模式: {mission.Mode}, " +
                        $"场景名称: {sceneName}");
                }
                #endif

                return isValidSiegeScene;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全检查战斗是否结束
        /// </summary>
        public static bool IsBattleEnded()
        {
            try
            {
                if (!IsMissionValid()) return true;
                return Mission.Current.MissionEnded || 
                       Mission.Current.CheckIfBattleInRetreat();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全获取攻击方士兵数量
        /// </summary>
        public static int GetAttackerCount(Team team)
        {
            try
            {
                if (team == null || !IsMissionValid()) return 0;

                var activeAgents = team.ActiveAgents;
                if (activeAgents == null) return 0;

                int totalCount = activeAgents.Count;
                int validCount = activeAgents.Count(a => IsValidAgent(a));

                #if DEBUG
                Logger.LogDebug("GetAttackerCount", 
                    $"队伍: {team.Side}, " +
                    $"总人数: {totalCount}, " +
                    $"有效人数: {validCount}, " +
                    $"无效人数: {totalCount - validCount}");
                #endif

                return validCount;
            }
            catch
            {
                return 0;
            }
        }
    }
} 