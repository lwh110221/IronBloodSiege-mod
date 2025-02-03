using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System;
using System.Linq;
using System.Collections.Generic;

namespace IronBloodSiege.Util
{
    public static class SafetyChecks
    {
        private static bool? _cachedIsSiegeScene = null;
        private static int _sceneCheckCount = 0;
        private static readonly int MAX_SCENE_CHECKS = 3;
        private static string _lastCheckedSceneName = null;
        private static readonly Dictionary<int, int> _teamCountCache = new Dictionary<int, int>();
        private static float _lastCountUpdateTime = 0f;
        private const float COUNT_CACHE_DURATION = 1f; // 1秒缓存时间

        /// <summary>
        /// 检查Agent是否为有效的AI控制的战斗单位
        /// </summary>
        public static bool IsValidAgent(Agent agent)
        {
            try
            {
                if (agent == null || agent.IsPlayerControlled) return false;
                return agent.IsHuman && agent.IsActive();
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
                {
                    _cachedIsSiegeScene = null;
                    _sceneCheckCount = 0;
                    _lastCheckedSceneName = null;
                    return false;
                }

                var mission = Mission.Current;
                var currentSceneName = mission.SceneName;

                // 已经有缓存结果且场景名没变，直接返回
                if (_cachedIsSiegeScene.HasValue && 
                    _sceneCheckCount >= MAX_SCENE_CHECKS && 
                    _lastCheckedSceneName == currentSceneName)
                {
                    return _cachedIsSiegeScene.Value;
                }

                // 场景名变了，重置缓存
                if (_lastCheckedSceneName != currentSceneName)
                {
                    _cachedIsSiegeScene = null;
                    _sceneCheckCount = 0;
                }

                _lastCheckedSceneName = currentSceneName;
                _sceneCheckCount++;

                var sceneName = currentSceneName?.ToLowerInvariant() ?? string.Empty;
                
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
                    $"BattleInRetreat: {mission.CheckIfBattleInRetreat()}, " +
                    $"检查次数: {_sceneCheckCount}, " +
                    $"是否使用缓存: {_cachedIsSiegeScene.HasValue}");
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

                // 缓存结果
                if (_sceneCheckCount >= MAX_SCENE_CHECKS)
                {
                    _cachedIsSiegeScene = isValidSiegeScene;
                }

                return isValidSiegeScene;
            }
            catch
            {
                _cachedIsSiegeScene = null;
                _sceneCheckCount = 0;
                _lastCheckedSceneName = null;
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
        /// 检查玩家是否为攻城方
        /// </summary>
        public static bool IsPlayerAttacker()
        {
            try
            {
                if (!IsMissionValid()) return false;
                return Mission.Current.MainAgent?.Team == Mission.Current.AttackerTeam;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全获取士兵数量，缓存减少重复计算
        /// 获取攻城方和守城方的士兵数量
        /// </summary>
        public static int GetAttackerCount(Team team)
        {
            try
            {
                if (team == null || !IsMissionValid()) return 0;

                float currentTime = Mission.Current.CurrentTime;
                
                // 使用Team.Side作为缓存键，避免使用Team对象本身
                int teamKey = (int)team.Side;
                
                if (currentTime - _lastCountUpdateTime >= COUNT_CACHE_DURATION)
                {
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", $"缓存过期（{COUNT_CACHE_DURATION}秒），清空缓存");
                    #endif
                    _teamCountCache.Clear();
                    _lastCountUpdateTime = currentTime;
                }

                if (_teamCountCache.TryGetValue(teamKey, out int cachedCount))
                {
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", 
                        $"使用缓存值 - Team: {team.Side}, " +
                        $"Count: {cachedCount}, " +
                        $"缓存时间: {currentTime - _lastCountUpdateTime:F2}秒");
                    #endif
                    return cachedCount;
                }

                var activeAgents = team.ActiveAgents;
                if (activeAgents == null) return 0;

                int validCount = activeAgents.Count(agent => IsValidAgent(agent));
                
                _teamCountCache[teamKey] = validCount;

                #if DEBUG
                Logger.LogDebug("GetAttackerCount", 
                    $"计算新值 - Team: {team.Side}, " +
                    $"Count: {validCount}, " +
                    $"更新缓存时间: {currentTime:F2}");
                #endif

                return validCount;
            }
            catch (Exception)
            {
                #if DEBUG
                Logger.LogDebug("GetAttackerCount", "获取攻击方数量时发生错误");
                #endif
                return 0;
            }
        }
    }
} 