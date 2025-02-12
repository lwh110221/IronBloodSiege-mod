using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;
using IronBloodSiege.Logic;

namespace IronBloodSiege.Behavior
{
    public class SiegeFormationBehavior : MissionBehavior, IModBehavior
    {
        #region Fields
        // 使用HashSet存储Formation
        private HashSet<Formation> _atkFormations;
        
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        private Team _attackerTeam;
        private Team _defenderTeam;  // 缓存守城方Team引用，用于快速判断
        
        private bool _isSiegeScene = false;      // 是否是攻城场景
        private bool _wasEnabledBefore = false;  // 添加字段跟踪之前的启用状态
        private bool _isDisabled = false;        // 是否禁用
        private bool _isCleanedUp = false;       // 清理标记
        private readonly object _cleanupLock = new object();
        
        // 用于控制检查频率
        private const float CHECK_INTERVAL = 1f;  // 每秒检查一次
        private float _lastCheckTime = 0f;   
        private bool _isOpenPreventRetreat = false;
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
                _wasEnabledBefore = Settings.Instance.IsEnabled;
                if (!_wasEnabledBefore) return;

                _currentMission = Mission.Current;
                if (_currentMission == null) return;

                // 缓存双方Team引用
                _attackerTeam = _currentMission.AttackerTeam;
                _defenderTeam = _currentMission.DefenderTeam;
                
                // 如果任一Team无效，直接禁用
                if (_attackerTeam == null || _defenderTeam == null)
                {
                    OnModDisabled();
                    return;
                }

                _atkFormations = new HashSet<Formation>();

                // 等待场景检查完全完成
                int checkCount = 0;
                const int MAX_WAIT_CHECKS = 3;
                bool isValidScene = false;

                while (checkCount < MAX_WAIT_CHECKS)
                {
                    isValidScene = SafetyChecks.IsSiegeSceneValid();
                    if (isValidScene)
                    {
                        break;
                    }
                    checkCount++;
                    System.Threading.Thread.Sleep(100);
                }

                _isSiegeScene = isValidScene;

                // 如果最终确认不是攻城战场景，才禁用
                if (!_isSiegeScene)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "非攻城战场景，禁用Formation控制器");
                    #endif
                    OnModDisabled();
                    return;
                }
            }
            catch (Exception ex)
            {
                HandleError("初始化", ex);
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
                        if (_atkFormations != null)
                        {
                            // 创建一个副本来遍历，避免集合修改异常
                            var formationsToRestore = _atkFormations.ToList();
                            foreach (var formation in formationsToRestore)
                            {
                                if (formation != null)
                                {
                                    BehaviorCleanupHelper.RestoreFormation(formation);
                                }
                            }
                            _atkFormations.Clear();
                        }

                        // 重置所有状态
                        _currentMission = null;
                        _attackerTeam = null;
                        _defenderTeam = null;
                        _isSiegeScene = false;
                        _wasEnabledBefore = false;
                        _isDisabled = true;
                        _isCleanedUp = true;

                        #if DEBUG
                        Util.Logger.LogDebug("任务结束", "Formation行为完成清理");
                        #endif
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

        public override void OnRemoveBehavior()
        {
            try
            {
                if (!_isCleanedUp)
                {
                    RestoreAllFormations();
                    
                    // 重置所有状态
                    _currentMission = null;
                    _attackerTeam = null;
                    _defenderTeam = null;
                    _isSiegeScene = false;
                    _wasEnabledBefore = false;
                    _isDisabled = true;
                    _isCleanedUp = true;
                }
                
                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                HandleError("移除行为", ex);
                base.OnRemoveBehavior();
            }
        }
        #endregion

        #region Formation Control
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
                
                try
                {
                    // 重置所有行为权重
                    formation.AI.ResetBehaviorWeights();
                    
                    // 恢复AI控制
                    formation.SetControlledByAI(true, false);
                    
                    // 恢复所有单位的行为
                    var unitsToProcess = formation.Arrangement.GetAllUnits().Select(unit => unit as Agent).Where(agent => agent != null).ToList();
                    foreach (var agent in unitsToProcess)
                    {
                        try
                        {
                            if (!SafetyChecks.IsValidAgent(agent) || 
                                agent.IsPlayerControlled || 
                                agent.Formation?.PlayerOwner != null)
                                continue;
                                         
                            // 重置士气为游戏原生默认值
                            agent.SetMorale(45f);

                            // 清除所有AI目标和缓存
                            try
                            {
                                agent.ClearTargetFrame();
                                agent.InvalidateTargetAgent();
                            }
                            catch
                            {
                                // 忽略目标清除失败
                            }
                            
                            // 更新缓存值和Formation值
                            try
                            {
                                agent.UpdateCachedAndFormationValues(true, true);
                            }
                            catch
                            {
                                // 忽略缓存更新失败
                            }
                        }
                        catch (Exception)
                        {
                            // 单个Agent恢复失败不影响其他Agent
                            continue;
                        }
                    }

                    // 重置Formation的移动命令为默认值
                    try
                    {
                        // formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                    }
                    catch
                    {
                        // 忽略移动命令设置失败
                    }
                    
                    // 重置Formation的AI行为权重
                    try
                    {
                        formation.AI.ResetBehaviorWeights();
                        formation.AI.SetBehaviorWeight<BehaviorStop>(0f);
                        formation.AI.SetBehaviorWeight<BehaviorRetreat>(0f);
                    }
                    catch
                    {
                        // 忽略AI权重设置失败
                    }
                    
                    // 确保Formation的AI会在下一个tick重新评估行为
                    formation.IsAITickedAfterSplit = false;
                }
                catch (Exception)
                {
                    // Formation恢复失败时尝试使用清理助手
                    try
                    {
                        BehaviorCleanupHelper.RestoreFormation(formation);
                    }
                    catch
                    {
                        // 即使清理助手也失败，继续执行
                    }
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
            if (_atkFormations == null) return;
            
            try
            {
                lock (_cleanupLock)
                {
                    // 创建一个副本来遍历，避免集合修改异常
                    var formationsToRestore = _atkFormations.ToList();
                    foreach (var formation in formationsToRestore)
                    {
                        if (formation != null)
                        {
                            try
                            {
                                RestoreFormation(formation);
                            }
                            catch (Exception)
                            {
                                // 单个Formation恢复失败不影响其他Formation
                                continue;
                            }
                        }
                    }
                    
                    try
                    {
                        _atkFormations.Clear();
                    }
                    catch
                    {
                        // 忽略清理失败
                    }
                    
                    #if DEBUG
                    Util.Logger.LogDebug("恢复Formation", "所有Formation已恢复原生系统控制");
                    #endif
                }
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
            if (!_isDisabled)
            {
                try
                {
                    _isDisabled = true;
                    RestoreAllFormations();
                }
                catch (Exception ex)
                {
                    HandleError("mod disabled", ex);
                }
            }
        }

        public void OnSceneChanged()
        {
            RestoreAllFormations();
            _isSiegeScene = false;
            _isDisabled = true;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            
            try
            {
                // 快速检查
                if (!Settings.Instance.IsEnabled || 
                    _isDisabled || 
                    !_isSiegeScene || 
                    _currentMission == null || 
                    _attackerTeam == null) return;

                // 控制检查频率
                _lastCheckTime += dt;
                if (_lastCheckTime < CHECK_INTERVAL) return;
                _lastCheckTime = 0f;

                // 获取所有攻城方的Formation
                var attackerFormations = _attackerTeam.FormationsIncludingEmpty
                    .Where(f => f != null && 
                           f.CountOfUnits > 0 && 
                           f.PlayerOwner == null &&
                           f.Captain?.IsPlayerControlled != true)
                    .ToList();

                foreach (var formation in attackerFormations)
                {
                    try
                    {
                        if (formation.AI == null) continue;

                        // 检查Formation是否在撤退
                        bool isRetreating = false;
                        
                        // 检查当前激活的行为
                        var activeBehavior = formation.AI.ActiveBehavior;
                        if (activeBehavior != null)
                        {
                            // 检查是否是任何类型的撤退行为
                            var behaviorType = activeBehavior.GetType();
                            isRetreating = behaviorType == typeof(BehaviorRetreat) ||
                                         behaviorType == typeof(BehaviorRetreatToKeep);
                        }

                        // 如果检测到撤退，开启防撤退功能
                        if (isRetreating && !_isOpenPreventRetreat)
                        {
                            _isOpenPreventRetreat = true;
                            #if DEBUG
                            Util.Logger.LogDebug("撤退检测",
                                $"首次检测到Formation撤退，开启防撤退功能 - 编队类型: {formation.FormationIndex}, " +
                                $"兵种: {formation.RepresentativeClass}, " +
                                $"当前行为: {formation.AI.ActiveBehavior?.GetType().Name ?? "无"}, " +
                                $"单位数量: {formation.CountOfUnits}");
                            #endif
                        }

                        // 如果防撤退功能已开启，则持续执行
                        if (_isOpenPreventRetreat)
                        {
                            SiegeAIBehavior.ApplyAttackBehavior(formation);
                            // 使用本地变量缓存Team引用，减少属性访问
                            Team formationTeam = formation.Team;

                            // formation.ApplyActionOnEachUnit(agent =>
                            // {
                            //     try
                            //     {
                            //         // 使用缓存的Team引用进行快速检查
                            //         if (agent == null ||
                            //             agent.Team != formationTeam ||
                            //             agent.IsPlayerControlled) return;

                            //         // 设置士气
                            //         agent.SetMorale(90f);
                            //     }
                            //     catch
                            //     {
                            //         // 单个Agent处理失败不影响其他Agent
                            //     }
                            // });
                        }
                    }
                    catch (Exception)
                    {
                        // 单个Formation处理失败不影响其他Formation
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("tick", ex);
            }
        }
    }
} 