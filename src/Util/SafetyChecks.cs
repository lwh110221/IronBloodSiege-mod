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
                    Logger.LogDebug("IsValidAgent", 
                        $"Agent validation failed - IsHuman: {isHuman}, " +
                        $"IsActive: {isActive}, " +
                        $"IsNotPlayerControlled: {isNotPlayerControlled}");
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
                    Logger.LogDebug("IsValidFormation", 
                        $"Formation validation failed - " +
                        $"HasUnits: {hasUnits}, " +
                        $"Formation team: {formation.Team?.Side}, " +
                        $"Expected team: {Mission.Current?.AttackerTeam?.Side}");
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
                Logger.LogDebug("IsSiegeSceneValid", 
                    $"Checking scene - Mode: {mission.Mode}, " +
                    $"Scene: {sceneName}, " +
                    $"HasDefender: {mission.DefenderTeam != null}, " +
                    $"HasAttacker: {mission.AttackerTeam != null}, " +
                    $"IsSiegeBattle: {mission.IsSiegeBattle}, " +
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
                                        mission.IsSiegeBattle ||
                                        mission.IsSallyOutBattle);

                bool hasValidTeams = mission.DefenderTeam != null &&
                                   mission.AttackerTeam != null;

                bool isValidSiegeScene = isValidMode && hasValidSceneName && hasValidTeams;

                #if DEBUG
                if (!isValidSiegeScene)
                {
                    Logger.LogDebug("IsSiegeSceneValid", 
                        $"Scene validation failed - ValidMode: {isValidMode}, " +
                        $"ValidSceneName: {hasValidSceneName}, " +
                        $"ValidTeams: {hasValidTeams}, " +
                        $"CurrentMode: {mission.Mode}, " +
                        $"SceneName: {sceneName}");
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
                    $"Team: {team.Side}, " +
                    $"Total agents: {totalCount}, " +
                    $"Valid agents: {validCount}, " +
                    $"Invalid agents: {totalCount - validCount}");
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