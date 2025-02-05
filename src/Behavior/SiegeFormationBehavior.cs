using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Behavior
{
    public class SiegeFormationBehavior : MissionBehavior, IModBehavior
    {
        #region Fields
        // 使用HashSet存储Formation
        private HashSet<Formation> _advancedFormations;
        
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        private Team _attackerTeam;
        
        private bool _isSiegeScene = false;      // 是否是攻城场景
        private bool _wasEnabledBefore = false;  // 添加字段跟踪之前的启用状态
        private bool _isDisabled = false;        // 是否禁用
        private bool _isCleanedUp = false;       // 清理标记
        private readonly object _cleanupLock = new object();
        #endregion

        #region Properties
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        #endregion

        #region Lifecycle Methods
        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
                // 记录初始启用状态
                _wasEnabledBefore = Settings.Instance.IsEnabled;
                if (!_wasEnabledBefore) return;

                _currentMission = Mission.Current;
                _attackerTeam = _currentMission?.AttackerTeam;
                _advancedFormations = new HashSet<Formation>();
                _isSiegeScene = CheckIfSiegeScene();
            }
            catch (Exception ex)
            {
                HandleError("初始化", ex);
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            try
            {
                // 如果mod未启用，直接返回
                if (!Settings.Instance.IsEnabled) return;
                
                if (!_isSiegeScene || 
                    !SafetyChecks.IsValidAgent(agent) || 
                    agent.Team == null ||
                    agent.Team != Mission.Current?.AttackerTeam ||
                    agent.Team.IsDefender ||
                    agent.IsPlayerControlled)
                    return;
                    
                agent.SetMorale(95f);
                
                // 禁用撤退标志
                agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRetreat);
                
                // 设置为进攻性AI行为
                // agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
                              
                // 如果mod未禁用，给新刷出的士兵设置进攻命令
                if (!_isDisabled && agent.Formation != null)
                {
                    PreventRetreat(agent.Formation);
                    
                    // 立即更新agent的缓存值
                    agent.UpdateCachedAndFormationValues(true, false);
                }
            }
            catch (Exception ex)
            {
                HandleError("agent build", ex);
            }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        if (_advancedFormations != null)
                        {
                            foreach (var formation in _advancedFormations)
                            {
                                RestoreFormation(formation);
                            }
                            _advancedFormations.Clear();
                        }
                        _isCleanedUp = true;
                    }
                }
                base.OnEndMission();
            }
            catch (Exception ex)
            {
                HandleError("任务结束", ex);
                base.OnEndMission();
            }
        }

        private bool CheckIfSiegeScene()
        {
            try
            {
                if (_currentMission == null) return false;
                
                bool isValidScene = SafetyChecks.IsSiegeSceneValid();
                
                #if DEBUG
                if (!isValidScene)
                {
                    Util.Logger.LogDebug("场景检查", 
                        $"场景检查失败 - " +
                        $"Mission: {_currentMission != null}, " +
                        $"Mode: {_currentMission?.Mode}, " +
                        $"Scene: {_currentMission?.SceneName}, " +
                        $"AttackerTeam: {_attackerTeam != null}");
                }
                #endif
                
                return isValidScene;
            }
            catch (Exception ex)
            {
                HandleError("场景检查", ex);
                return false;
            }
        }
        #endregion

        #region Formation Control
        /// <summary>
        /// 防止撤退并恢复Formation的移动
        /// </summary>
        private void PreventRetreat(Formation formation)
        {
            try
            {
                // 如果mod未启用或Formation无效，直接返回
                if (!Settings.Instance.IsEnabled || !SafetyChecks.IsValidFormation(formation)) 
                    return;
                
                // 严格检查是否为攻城方Formation
                if (formation.Team == null || 
                    formation.Team != _attackerTeam || 
                    formation.Team.IsDefender)
                {
                    return;
                }
                
                // 检查是否是玩家控制的Formation
                if (formation.PlayerOwner != null || 
                    formation.Captain?.IsPlayerControlled == true)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", 
                        $"跳过玩家控制的Formation: {formation.FormationIndex}");
                    #endif
                    return;
                }
                
                // 检查玩家是否是攻城方
                if (SafetyChecks.IsPlayerAttacker())
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", 
                        $"玩家是攻城方，跳过AI控制设置");
                    #endif
                    return;
                }
                
                if (!_advancedFormations.Contains(formation))
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", 
                        $"处理攻城方Formation: {formation.FormationIndex}");
                    #endif

                    // 重置Formation的AI行为
                    formation.AI.ResetBehaviorWeights();
                    
                    // 设置为进攻命令
                    // formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    
                    // 增加进攻性AI行为权重
                    formation.AI.SetBehaviorWeight<BehaviorCharge>(0.8f);
                    formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.8f);
                    
                    // 禁用可能导致停止的行为
                    formation.AI.SetBehaviorWeight<BehaviorStop>(0f);
                    formation.AI.SetBehaviorWeight<BehaviorRetreat>(0f);
                    
                    // 确保Formation的AI会在下一个tick重新评估行为
                    formation.IsAITickedAfterSplit = false;
                    
                    // 添加到已处理列表
                    _advancedFormations.Add(formation);
                    
                    // 处理所有单位
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        if (!SafetyChecks.IsValidAgent(agent) || 
                            agent.Team != _attackerTeam || 
                            agent.Team.IsDefender || 
                            agent.IsPlayerControlled || 
                            agent.Formation?.PlayerOwner != null)
                            return;

                        // 停止当前的撤退状态
                        if (agent.IsRetreating())
                        {
                            agent.StopRetreating();
                        }
                        
                        agent.SetMorale(95f);
                        
                        // 禁用撤退标志
                        agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRetreat);
                        
                        // 设置为进攻性AI行为
                        // agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
                    });
                    // formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                }
            }
            catch (Exception ex)
            {
                HandleError("prevent retreat", ex);
            }
        }

        /// <summary>
        /// 恢复原生系统控制
        /// </summary>
        private void RestoreFormation(Formation formation)
        {
            try
            {
                if (!SafetyChecks.IsValidFormation(formation)) return;
                
                // 确保只对攻城方生效
                if (formation.Team != _attackerTeam) return;
                
                // 跳过玩家控制的Formation
                if (formation.PlayerOwner != null || 
                    formation.Captain?.IsPlayerControlled == true)
                {
                    return;
                }
                
                // 重置所有行为权重
                formation.AI.ResetBehaviorWeights();
                
                // 恢复AI控制
                formation.SetControlledByAI(true, false);
                
                // 恢复所有单位的行为
                formation.ApplyActionOnEachUnit(agent =>
                {
                    if (!SafetyChecks.IsValidAgent(agent) || 
                        agent.IsPlayerControlled || 
                        agent.Formation?.PlayerOwner != null)
                        return;

                    // 恢复撤退标志
                    agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanRetreat);
                    
                    // 重置行为集
                    agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
                                     
                    // 重置士气（设为中等值）
                    agent.SetMorale(50f);
                    
                    // 更新缓存值
                    agent.UpdateCachedAndFormationValues(true, false);
                });
                
                // 如果Formation处于停止状态，设置为前进命令
                var currentOrder = formation.GetReadonlyMovementOrderReference();
                if (currentOrder.OrderEnum == MovementOrder.MovementOrderEnum.Stop)
                {
                    formation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
                }
                
                #if DEBUG
                Util.Logger.LogDebug("恢复Formation", 
                    $"Formation已恢复 - 编队类型: {formation.FormationIndex}, " +
                    $"单位数量: {formation.CountOfUnits}");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("restore formation", ex);
            }
        }

        /// <summary>
        /// 恢复所有Formation的原生系统控制
        /// </summary>
        private void RestoreAllFormations()
        {
            if (_advancedFormations == null) return;
            
            try
            {
                var formationsToRestore = _advancedFormations.ToList();
                foreach (var formation in formationsToRestore)
                {
                    if (formation != null)
                    {
                        RestoreFormation(formation);
                    }
                }
                _advancedFormations.Clear();
                
                #if DEBUG
                Util.Logger.LogDebug("恢复Formation", "所有Formation已恢复原生系统控制");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("restore all formations", ex);
            }
        }
        #endregion

        private void HandleError(string context, Exception ex)
        {
            try
            {
                #if DEBUG
                Util.Logger.LogError(context, ex);
                #endif
                
                Constants.ErrorMessage
                    .SetTextVariable("CONTEXT", context)
                    .SetTextVariable("MESSAGE", ex.Message);
                    
                InformationManager.DisplayMessage(new InformationMessage(
                    Constants.ErrorMessage.ToString(),
                    Constants.ErrorColor));
            }
            catch
            {
                #if DEBUG
                try
                {
                    Util.Logger.LogError("error handler", new Exception("Failed to handle error: " + context));
                }
                catch { }
                #endif
            }
        }

        public void OnModEnabled()
        {
            _isDisabled = false;
        }

        public void OnModDisabled()
        {
            _isDisabled = true;
            RestoreAllFormations();
        }

        public void OnSceneChanged()
        {
            RestoreAllFormations();
        }
    }
} 