using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Behavior;
using TaleWorlds.Core;

namespace IronBloodSiege.Util
{
    /// <summary>
    /// 行为清理助手类
    /// </summary>
    public static class BehaviorCleanupHelper
    {
        private static readonly HashSet<Formation> _cleanedFormations = new HashSet<Formation>();
        private static readonly HashSet<Agent> _cleanedAgents = new HashSet<Agent>();
        private static readonly object _cleanLock = new object();
        private static bool _isCleaningUp = false;

        /// <summary>
        /// 检查是否正在进行清理
        /// </summary>
        public static bool IsCleaningUp
        {
            get
            {
                lock (_cleanLock)
                {
                    return _isCleaningUp;
                }
            }
        }

        /// <summary>
        /// 安全地获取Mission引用
        /// </summary>
        public static Mission GetSafeMission(Mission currentMission)
        {
            try
            {
                if (IsCleaningUp) return null;
                return currentMission ?? Mission.Current;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 安全地获取Team引用
        /// </summary>
        public static Team GetSafeTeam(Team team, Mission mission = null)
        {
            try
            {
                if (IsCleaningUp) return null;
                if (team != null) return team;
                mission = mission ?? Mission.Current;
                return mission?.AttackerTeam;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 清理Formation相关资源
        /// </summary>
        public static void CleanupFormations(HashSet<Formation> formations, Action<Formation> restoreAction)
        {
            if (formations == null) return;
            
            try
            {
                lock (_cleanLock)
                {
                    _isCleaningUp = true;
                    _cleanedFormations.Clear(); // 重置清理记录
                    
                    var formationsToRestore = formations.ToList();
                    foreach (var formation in formationsToRestore)
                    {
                        if (formation != null && !_cleanedFormations.Contains(formation))
                        {
                            try
                            {
                                restoreAction?.Invoke(formation);
                                _cleanedFormations.Add(formation);
                            }
                            catch (Exception ex)
                            {
                                #if DEBUG
                                Util.Logger.LogError("Formation清理", ex);
                                #endif
                            }
                        }
                    }
                    
                    formations.Clear();
                    _isCleaningUp = false;
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("Formations批量清理", ex);
                #endif
                _isCleaningUp = false;
            }
        }

        /// <summary>
        /// 清理行为组件的状态
        /// </summary>
        public static void CleanupBehaviorState(
            ref bool isCleanedUp,
            ref float lastRetreatMessageTime,
            ref int initialAttackerCount,
            ref Mission currentMission,
            ref string currentSceneName,
            ref Team attackerTeam,
            ref Team defenderTeam,
            ref SiegeFormationBehavior formationBehavior,
            ref SiegeMoraleManagerBehavior moraleManagerBehavior,
            ref SiegeReinforcementBehavior reinforcementBehavior)
        {
            try
            {
                if (isCleanedUp) return;

                lock (_cleanLock)
                {
                    _isCleaningUp = true;
                    _cleanedFormations.Clear();
                    _cleanedAgents.Clear();

                    // 确保其他行为组件先清理
                    if (formationBehavior != null)
                    {
                        formationBehavior.OnModDisabled();
                        formationBehavior = null;
                    }

                    if (moraleManagerBehavior != null)
                    {
                        moraleManagerBehavior.OnModDisabled();
                        moraleManagerBehavior = null;
                    }

                    if (reinforcementBehavior != null)
                    {
                        reinforcementBehavior.OnModDisabled();
                        reinforcementBehavior = null;
                    }

                    // 重置所有基础状态
                    lastRetreatMessageTime = 0f;
                    initialAttackerCount = 0;
                    
                    // 清理Team引用
                    attackerTeam = null;
                    defenderTeam = null;
                    
                    // 清理Mission引用
                    currentMission = null;
                    currentSceneName = null;

                    isCleanedUp = true;
                    _isCleaningUp = false;

                    #if DEBUG
                    Util.Logger.LogDebug("清理助手", "完成所有状态清理");
                    #endif
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("清理助手", ex);
                #endif
                _isCleaningUp = false;
            }
        }

        /// <summary>
        /// 清理任务结束状态
        /// </summary>
        public static void CleanupMissionEnd(
            ref bool missionEnding,
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
            try
            {
                lock (_cleanLock)
                {
                    _isCleaningUp = true;
                    _cleanedFormations.Clear();
                    _cleanedAgents.Clear();

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
                    
                    _isCleaningUp = false;
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("任务结束清理", ex);
                #endif
                _isCleaningUp = false;
            }
        }

        /// <summary>
        /// 恢复Formation的原始状态
        /// </summary>
        public static void RestoreFormation(Formation formation)
        {
            try
            {
                if (formation == null) return;

                lock (_cleanLock)
                {
                    if (_cleanedFormations.Contains(formation)) return;

                    _isCleaningUp = true;
                    // 重置Formation的AI行为权重
                    formation.AI.ResetBehaviorWeights();
                    
                    // 恢复AI控制
                    formation.SetControlledByAI(true, false);
                    
                    _cleanedAgents.Clear(); // 重置Agent清理记录
                    
                    // 处理Formation中的所有Agent
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        if (agent == null || !agent.IsActive()) return;
                        
                        if (!_cleanedAgents.Contains(agent))
                        {
                            RestoreAgent(agent);
                            _cleanedAgents.Add(agent);
                        }
                    });
                    
                    // 如果Formation处于停止状态，设置为前进
                    var currentOrder = formation.GetReadonlyMovementOrderReference();
                    if (currentOrder.OrderEnum == MovementOrder.MovementOrderEnum.Stop)
                    {
                        formation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
                    }
                    
                    _cleanedFormations.Add(formation);
                    _isCleaningUp = false;
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("恢复Formation", ex);
                #endif
                _isCleaningUp = false;
            }
        }

        /// <summary>
        /// 恢复Agent的原始状态
        /// </summary>
        public static void RestoreAgent(Agent agent)
        {
            try
            {
                if (agent == null || !agent.IsActive()) return;

                lock (_cleanLock)
                {
                    if (_cleanedAgents.Contains(agent)) return;

                    _isCleaningUp = true;
                    // 恢复撤退标志
                    agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanRetreat);
                    
                    // 重置行为集
                    agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
                    
                    // 重置士气（设为中等值）
                    agent.SetMorale(70f);
                    
                    // 更新缓存值
                    agent.UpdateCachedAndFormationValues(true, false);
                    
                    _cleanedAgents.Add(agent);
                    _isCleaningUp = false;
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("恢复Agent", ex);
                #endif
                _isCleaningUp = false;
            }
        }
    }
} 