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
        private static readonly int MAX_SCENE_CHECKS = 2;  // 减少检查次数
        private static string _lastCheckedSceneName = null;
        private static float _lastSceneCheckTime = 0f;
        private static readonly Dictionary<int, int> _teamCountCache = new Dictionary<int, int>();
        private static float _lastCountUpdateTime = -1f;  // 初始化为-1
        private const float COUNT_CACHE_DURATION = 1f;    // 缓存时间
        private const float SCENE_CHECK_TIMEOUT = 5f;     // 场景检查超时时间
        private static bool _isFirstCheck = true;         // 添加首次检查标记
        private static bool _isNewBattle = true;          // 添加新战斗标记

        /// <summary>
        /// 重置场景检查状态
        /// </summary>
        private static void ResetSceneCheck()
        {
            _cachedIsSiegeScene = null;
            _sceneCheckCount = 0;
            _lastCheckedSceneName = null;
            _lastSceneCheckTime = 0f;
            _isFirstCheck = true;
            _isNewBattle = true;
            ResetCountCache();
        }

        /// <summary>
        /// 重置数量缓存
        /// </summary>
        private static void ResetCountCache()
        {
            _teamCountCache.Clear();
            _lastCountUpdateTime = -1f;
            
            #if DEBUG
            Logger.LogDebug("缓存重置", "重置数量缓存和时间");
            #endif
        }

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
                    ResetSceneCheck();
                    #if DEBUG
                    Logger.LogDebug("场景验证", "Mission无效");
                    #endif
                    return false;
                }

                var mission = Mission.Current;
                var currentSceneName = mission.SceneName;
                var currentTime = mission.CurrentTime;

                // 场景名变了或者超时，重置缓存
                if (_lastCheckedSceneName != currentSceneName || 
                    (currentTime - _lastSceneCheckTime) > SCENE_CHECK_TIMEOUT ||
                    _isFirstCheck)  // 首次检查时也重置
                {
                    ResetSceneCheck();
                    #if DEBUG
                    if (_isFirstCheck)
                    {
                        Logger.LogDebug("场景验证", "首次检查，重置所有状态");
                    }
                    else if (_lastCheckedSceneName != currentSceneName)
                    {
                        Logger.LogDebug("场景验证", $"场景名称变化 - 旧: {_lastCheckedSceneName}, 新: {currentSceneName}");
                    }
                    else
                    {
                        Logger.LogDebug("场景验证", $"场景检查超时 - 上次检查时间: {_lastSceneCheckTime:F2}, 当前时间: {currentTime:F2}");
                    }
                    #endif
                }

                _lastCheckedSceneName = currentSceneName;
                _lastSceneCheckTime = currentTime;
                _sceneCheckCount++;
                _isFirstCheck = false;

                var sceneName = currentSceneName?.ToLowerInvariant() ?? string.Empty;
                
                #if DEBUG
                Logger.LogDebug("场景验证", 
                    $"检查场景 - " +
                    $"模式: {mission.Mode}, " +
                    $"场景: {sceneName}, " +
                    $"有防守方: {mission.DefenderTeam != null}, " +
                    $"有进攻方: {mission.AttackerTeam != null}, " +
                    $"战斗类型: {mission.CombatType}, " +
                    $"MissionTime: {currentTime:F2}, " +
                    $"MissionEnded: {mission.MissionEnded}, " +
                    $"BattleInRetreat: {mission.CheckIfBattleInRetreat()}, " +
                    $"检查次数: {_sceneCheckCount}, " +
                    $"是否使用缓存: {_cachedIsSiegeScene.HasValue}");
                #endif

                // 检查场景名称
                bool hasValidSceneName = !string.IsNullOrEmpty(sceneName);
                
                // 明确排除竞技场场景
                if (hasValidSceneName && sceneName.Contains("arena"))
                {
                    #if DEBUG
                    Logger.LogDebug("场景验证", "竞技场场景，跳过验证");
                    #endif
                    ResetSceneCheck();
                    return false;
                }
                
                // 检查场景名称是否包含攻城战关键字
                bool hasSiegeKeyword = sceneName.Contains("siege");
                bool hasCastleKeyword = sceneName.Contains("castle");
                bool hasTownKeyword = sceneName.Contains("town");
                
                // 检查是否为战斗场景
                bool isValidBattleType = hasValidSceneName && 
                                       (hasSiegeKeyword || hasCastleKeyword || hasTownKeyword) &&
                                       mission.CombatType == Mission.MissionCombatType.Combat;

                // 如果是StartUp模式，需要等待一段时间让场景完全加载
                if (mission.Mode == MissionMode.StartUp)
                {
                    // 在StartUp模式下，等待更少的检查次数
                    if (_sceneCheckCount < MAX_SCENE_CHECKS)
                    {
                        #if DEBUG
                        Logger.LogDebug("场景验证", 
                            $"StartUp模式等待中 - 检查次数: {_sceneCheckCount}, " +
                            $"目标次数: {MAX_SCENE_CHECKS}, " +
                            $"场景名称: {sceneName}, " +
                            $"战斗类型: {mission.CombatType}, " +
                            $"有防守方: {mission.DefenderTeam != null}, " +
                            $"有进攻方: {mission.AttackerTeam != null}");
                        #endif
                        return false;
                    }

                    // 在StartUp模式下，检查场景名称、战斗类型和队伍状态
                    bool hasTeams = mission.DefenderTeam != null && mission.AttackerTeam != null;
                    bool isValidStartup = isValidBattleType && hasTeams;

                    #if DEBUG
                    if (!isValidStartup)
                    {
                        Logger.LogDebug("场景验证", 
                            $"StartUp模式验证失败 - " +
                            $"有效战斗场景: {isValidBattleType}, " +
                            $"战斗类型: {mission.CombatType}, " +
                            $"有队伍: {hasTeams}, " +
                            $"防守方: {mission.DefenderTeam != null}, " +
                            $"进攻方: {mission.AttackerTeam != null}");
                    }
                    else
                    {
                        Logger.LogDebug("场景验证", 
                            $"StartUp模式验证成功 - " +
                            $"场景名称: {sceneName}, " +
                            $"战斗类型: {mission.CombatType}, " +
                            $"检查次数: {_sceneCheckCount}");
                    }
                    #endif

                    // 缓存结果
                    _cachedIsSiegeScene = isValidStartup;
                    return isValidStartup;
                }

                // 允许Battle和Deployment模式
                bool isValidMode = mission.Mode == MissionMode.Battle || 
                                  mission.Mode == MissionMode.Deployment;

                // 检查双方队伍
                bool hasValidTeams = mission.DefenderTeam != null &&
                                   mission.AttackerTeam != null;

                // 检查战斗状态
                bool isValidBattleState = !mission.MissionEnded && 
                                        !mission.CheckIfBattleInRetreat();

                bool isValidSiegeScene = isValidMode && 
                                       isValidBattleType &&
                                       hasValidTeams && 
                                       isValidBattleState;

                #if DEBUG
                if (!isValidSiegeScene)
                {
                    Logger.LogDebug("场景验证", 
                        $"场景验证失败 - " +
                        $"允许模式: {isValidMode}, " +
                        $"有效战斗场景: {isValidBattleType}, " +
                        $"队伍: {hasValidTeams}, " +
                        $"战斗状态: {isValidBattleState}, " +
                        $"当前模式: {mission.Mode}, " +
                        $"场景名称: {sceneName}, " +
                        $"战斗类型: {mission.CombatType}");
                }
                else
                {
                    Logger.LogDebug("场景验证", 
                        $"场景验证成功 - " +
                        $"场景名称: {sceneName}, " +
                        $"模式: {mission.Mode}, " +
                        $"战斗类型: {mission.CombatType}");
                }
                #endif

                // 缓存结果
                if (_sceneCheckCount >= MAX_SCENE_CHECKS)
                {
                    _cachedIsSiegeScene = isValidSiegeScene;
                }

                return isValidSiegeScene;
            }
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("场景验证", ex);
                #endif
                ResetSceneCheck();
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
        /// </summary>
        public static int GetAttackerCount(Team team)
        {
            try
            {
                if (team == null || !IsMissionValid()) return 0;

                float currentTime = Mission.Current.CurrentTime;
                
                // 检查时间是否有效
                if (currentTime < 0f)
                {
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", $"无效的时间值: {currentTime:F2}");
                    #endif
                    return 0;
                }

                // 战斗开始的前60秒，禁用缓存并等待所有部队生成完成
                bool isInitialPhase = currentTime < 60f;
                if (isInitialPhase)
                {
                    // 如果是新战斗，重置缓存
                    if (_isNewBattle)
                    {
                        ResetCountCache();
                        _isNewBattle = false;
                    }

                    var initialAgents = team.ActiveAgents;
                    if (initialAgents == null) return 0;

                    int initialCount = initialAgents.Count(agent => IsValidAgent(agent));
                    
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", 
                        $"战斗初期计算 - Team: {team.Side}, " +
                        $"Count: {initialCount}, " +
                        $"Time: {currentTime:F2}");
                    #endif

                    // 初期阶段数量异常，返回一个安全值
                    if (initialCount < 50)
                    {
                        #if DEBUG
                        Logger.LogDebug("GetAttackerCount", 
                            $"战斗初期数量异常，返回安全值 - Team: {team.Side}, " +
                            $"原始Count: {initialCount}");
                        #endif
                        return 999;
                    }

                    // 在初期阶段即将结束时，更新缓存
                    if (currentTime >= 59f)
                    {
                        _teamCountCache.Clear();
                        _lastCountUpdateTime = currentTime;
                        _teamCountCache[(int)team.Side] = initialCount;
                        
                        #if DEBUG
                        Logger.LogDebug("GetAttackerCount", 
                            $"初期阶段结束，更新缓存 - Team: {team.Side}, " +
                            $"Count: {initialCount}, " +
                            $"Time: {currentTime:F2}");
                        #endif
                    }

                    return initialCount;
                }

                // 使用Team.Side作为缓存键
                int teamKey = (int)team.Side;
                
                // 检查缓存是否有效
                bool isCacheValid = _lastCountUpdateTime > 0f && 
                                  currentTime >= _lastCountUpdateTime && 
                                  (currentTime - _lastCountUpdateTime) <= COUNT_CACHE_DURATION;

                // 如果缓存无效，重置缓存
                if (!isCacheValid)
                {
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", 
                        $"缓存无效，重置 - " +
                        $"当前时间: {currentTime:F2}, " +
                        $"上次更新: {_lastCountUpdateTime:F2}, " +
                        $"间隔: {currentTime - _lastCountUpdateTime:F2}");
                    #endif
                    
                    ResetCountCache();
                }
                // 如果缓存有效且存在缓存值，使用缓存
                else if (_teamCountCache.TryGetValue(teamKey, out int cachedCount))
                {
                    #if DEBUG
                    Logger.LogDebug("GetAttackerCount", 
                        $"使用缓存值 - Team: {team.Side}, " +
                        $"Count: {cachedCount}, " +
                        $"缓存时间: {currentTime - _lastCountUpdateTime:F2}秒");
                    #endif
                    return cachedCount;
                }

                // 计算新值
                var activeAgents = team.ActiveAgents;
                if (activeAgents == null) return 0;

                int validCount = activeAgents.Count(agent => IsValidAgent(agent));
                
                // 更新缓存
                _teamCountCache[teamKey] = validCount;
                _lastCountUpdateTime = currentTime;

                #if DEBUG
                Logger.LogDebug("GetAttackerCount", 
                    $"计算新值 - Team: {team.Side}, " +
                    $"Count: {validCount}, " +
                    $"更新时间: {currentTime:F2}");
                #endif

                return validCount;
            }
            catch (Exception)
            {
                #if DEBUG
                Logger.LogDebug("GetAttackerCount", "获取攻击方数量时发生错误");
                #endif
                return 999; // 发生错误时返回安全值
            }
        }
    }
} 