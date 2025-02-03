using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.Objects.Siege;
using TaleWorlds.MountAndBlade.Objects;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Behavior
{
    public class SiegeMoraleBehavior : MissionBehavior
    {
        #region Fields
        // 使用HashSet存储Formation
        private HashSet<Formation> _advancedFormations;
        
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        // 缓存攻防双方的Team引用
        private Team _attackerTeam;
        private Team _defenderTeam;
        
        // 缓存当前场景名称的小写形式
        private string _currentSceneName;
        
        private bool _isSiegeScene = false;      // 是否是攻城场景
        private bool _isDisabled = false;        // 是否禁用
        private bool _pendingDisable = false;    // 用于标记是否处于等待禁用状态
        private float _disableTimer = 0f;        // 禁用计时器
        private int _initialAttackerCount = 0;   // 开始计时时的攻击方士兵数量
        private float _lastMoraleUpdateTime = 0f; // 最后一次更新士气的时间
        private float _lastMessageTime = 0f; // 消息时间
        private float _lastRetreatMessageTime = 0f; // 撤退消息时间
        private bool _isBeingRemoved = false;  // 添加标记，防止重复执行
        private bool _missionEnding = false; // 战斗结束标记
        private bool _isCleanedUp = false;  // 添加标记，确保只清理一次
        private readonly object _cleanupLock = new object();  // 线程锁
        private float _battleStartTime = 0f; // 战斗开始时间
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
                // 记录战斗开始时间
                _battleStartTime = Mission.Current?.CurrentTime ?? 0f;
                
                // 重置状态
                ResetState();
                
                // 初始化缓存
                InitializeCache();
                
                // 检查场景
                _isSiegeScene = CheckIfSiegeScene();
            }
            catch (Exception ex)
            {
                HandleError("initialization", ex);
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                if (_missionEnding || _isDisabled)
                {
                    return;
                }

                if (!Settings.Instance.IsEnabled || !SafetyChecks.IsMissionValid())
                {
                    return;
                }

                _currentMission = Mission.Current;
                if (_currentMission == null) return;

                if (SafetyChecks.IsBattleEnded())
                {
                    if (!_missionEnding)
                    {
                        #if DEBUG
                        Util.Logger.LogDebug("任务检查", "检测到战斗结束");
                        #endif
                        _missionEnding = true;
                        DisableMod("战斗结束");
                    }
                    return;
                }

                if (_pendingDisable)
                {
                    ProcessDisableTimer(dt);
                    return;
                }

                if (_currentMission.CurrentTime % 10f < dt)
                {
                    bool previousSceneState = _isSiegeScene;
                    _isSiegeScene = SafetyChecks.IsSiegeSceneValid();
                    
                    #if DEBUG
                    if (previousSceneState != _isSiegeScene)
                    {
                        Util.Logger.LogDebug("任务检查", 
                            $"场景状态改变 - 之前: {previousSceneState}, 现在: {_isSiegeScene}, " +
                            $"是否启用: {Settings.Instance.IsEnabled}, " +
                            $"启用固定撤退: {Settings.Instance.EnableFixedRetreat}, " +
                            $"撤退阈值: {Settings.Instance.RetreatThreshold}");
                    }
                    #endif
                    
                    if (previousSceneState && !_isSiegeScene)
                    {
                        DisableMod("攻城场景结束");
                        return;
                    }
                }
                
                if (!_isSiegeScene)
                {
                    return;
                }

                _attackerTeam = _currentMission.AttackerTeam;
                _defenderTeam = _currentMission.DefenderTeam;
                
                if (_attackerTeam == null)
                {
                    return;
                }

                AdjustTeamMorale(_attackerTeam, dt);
            }
            catch (Exception ex)
            {
                HandleError("任务更新", ex);
                DisableMod("任务更新错误");
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            try
            {
                if (!_isSiegeScene || !SafetyChecks.IsValidAgent(agent) || 
                    agent.Team != Mission.Current?.AttackerTeam) return;
                    
                agent.SetMorale(Settings.Instance.MoraleThreshold);
            }
            catch (Exception ex)
            {
                HandleError("agent build", ex);
            }
        }

        public override void OnClearScene()
        {
            try
            {
                if (!_missionEnding)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("场景清理", "开始清理场景");
                    #endif
                    _missionEnding = true;
                    DisableMod("Scene clearing");
                }
                base.OnClearScene();
            }
            catch
            {
                #if DEBUG
                Util.Logger.LogDebug("场景清理", "场景清理时发生错误");
                #endif
            }
        }
        protected override void OnEndMission()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("任务结束", "开始任务结束清理");
                #endif

                if (_advancedFormations != null)
                {
                    BehaviorCleanupHelper.CleanupFormations(_advancedFormations, RestoreFormation);
                    _advancedFormations = null;
                }

                BehaviorCleanupHelper.CleanupMissionEnd(
                    ref _missionEnding,
                    ref _isDisabled,
                    ref _pendingDisable,
                    ref _disableTimer,
                    ref _isSiegeScene,
                    ref _initialAttackerCount,
                    ref _lastMoraleUpdateTime,
                    ref _lastMessageTime,
                    ref _lastRetreatMessageTime,
                    ref _attackerTeam,
                    ref _defenderTeam,
                    ref _currentMission,
                    ref _currentSceneName
                );

                #if DEBUG
                Util.Logger.LogDebug("任务结束", "清理完成，调用基类OnEndMission");
                #endif

                base.OnEndMission();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("任务结束", ex);
                #endif
                
                base.OnEndMission();
            }
            finally
            {
                _isCleanedUp = true;
                _isBeingRemoved = false;
                
                #if DEBUG
                Util.Logger.LogDebug("任务结束", "任务结束清理完成");
                #endif
            }

            _teamCountCache?.Clear();
            _advancedFormations?.Clear();
        }

        public override void OnEndMissionInternal()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("任务内部结束", "开始内部任务结束清理");
                #endif

                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        BehaviorCleanupHelper.CleanupBehaviorState(
                            ref _isCleanedUp,
                            ref _lastMoraleUpdateTime,
                            ref _lastMessageTime,
                            ref _lastRetreatMessageTime,
                            ref _initialAttackerCount,
                            ref _currentMission,
                            ref _currentSceneName,
                            ref _attackerTeam,
                            ref _defenderTeam,
                            ref _advancedFormations
                        );
                    }
                }

                #if DEBUG
                Util.Logger.LogDebug("任务内部结束", "调用基类OnEndMissionInternal");
                #endif

                base.OnEndMissionInternal();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("任务内部结束", ex);
                #endif
                
                base.OnEndMissionInternal();
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_isBeingRemoved)
            {
                #if DEBUG
                Util.Logger.LogDebug("移除行为", "行为已在移除中，跳过");
                #endif
                return;
            }

            try
            {
                #if DEBUG
                Util.Logger.LogDebug("移除行为", "开始移除行为");
                #endif

                _isBeingRemoved = true;
                
                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        BehaviorCleanupHelper.CleanupBehaviorState(
                            ref _isCleanedUp,
                            ref _lastMoraleUpdateTime,
                            ref _lastMessageTime,
                            ref _lastRetreatMessageTime,
                            ref _initialAttackerCount,
                            ref _currentMission,
                            ref _currentSceneName,
                            ref _attackerTeam,
                            ref _defenderTeam,
                            ref _advancedFormations
                        );
                    }
                }

                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("移除行为", ex);
                #endif
                
                base.OnRemoveBehavior();
            }
            finally
            {
                _isBeingRemoved = false;
                
                #if DEBUG
                Util.Logger.LogDebug("移除行为", "行为移除完成");
                #endif
            }
        }
        #endregion

        #region Initialization Methods
        private void InitializeCache()
        {
            _currentMission = Mission.Current;
            if (_currentMission != null)
            {
                _attackerTeam = _currentMission.AttackerTeam;
                _defenderTeam = _currentMission.DefenderTeam;
                _currentSceneName = _currentMission.SceneName?.ToLowerInvariant() ?? string.Empty;
            }
        }

        private void ResetState()
        {
            _isSiegeScene = false;
            _isDisabled = false;
            _pendingDisable = false;
            _disableTimer = 0f;
            _initialAttackerCount = 0;
            _lastMoraleUpdateTime = 0f;
            _lastMessageTime = 0f;
            _lastRetreatMessageTime = 0f;
            
            if (_advancedFormations == null)
            {
                _advancedFormations = new HashSet<Formation>();
            }
            else
            {
                _advancedFormations.Clear();
            }
        }

        private bool CheckIfSiegeScene()
        {
            if (_currentMission == null) 
                return false;
            
            return _currentMission.Mode == MissionMode.Battle && 
                   !string.IsNullOrEmpty(_currentSceneName) &&
                   (_currentSceneName.Contains("siege") || _currentSceneName.Contains("castle") || _currentSceneName.Contains("town")) &&
                   _defenderTeam != null &&
                   _attackerTeam != null;
        }
        #endregion

        #region Core Business Logic
        private void AdjustTeamMorale(Team team, float dt)
        {
            try
            {
                if (team == null || team != _attackerTeam) return;

                float currentTime = _currentMission.CurrentTime;
                
                if (currentTime - _lastMoraleUpdateTime < Constants.MORALE_UPDATE_INTERVAL)
                    return;

                bool isPlayerAttacker = _currentMission.MainAgent?.Team == _attackerTeam;
                if (isPlayerAttacker && !Settings.Instance.EnableWhenPlayerAttacker)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("调整士气", 
                        $"玩家是攻方且设置禁用 - 玩家是攻方: {isPlayerAttacker}, " +
                        $"攻方启用: {Settings.Instance.EnableWhenPlayerAttacker}");
                    #endif
                    return;
                }

                _lastMoraleUpdateTime = currentTime;
                
                float moraleThreshold = Settings.Instance.MoraleThreshold;
                float moraleBoostRate = Settings.Instance.MoraleBoostRate;
                
                int attackerCount = SafetyChecks.GetAttackerCount(team);
                
                #if DEBUG
                Util.Logger.LogDebug("调整士气", $"当前攻击方数量: {attackerCount}");
                #endif

                if (ShouldAllowRetreat(team, attackerCount))
                {
                    #if DEBUG
                    Util.Logger.LogDebug("调整士气", "满足撤退条件，跳过士气调整");
                    #endif
                    return;
                }

                float strengthRatio = 2.0f;
                if (_defenderTeam != null)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                    if (defenderCount > 0)
                    {
                        strengthRatio = (float)attackerCount / defenderCount;
                    }
                }

                if (strengthRatio >= 1.5f)
                {
                    moraleThreshold *= 0.6f;
                }

                int boostedCount = UpdateAgentsMorale(team, moraleThreshold, moraleBoostRate);
                
                if (boostedCount >= 10 && currentTime - _lastMessageTime >= Constants.MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = currentTime;
                    Constants.MoraleBoostMessage.SetTextVariable("COUNT", boostedCount);
                    InformationManager.DisplayMessage(new InformationMessage(
                        Constants.MoraleBoostMessage.ToString(),
                        Constants.WarningColor));
                    
                    #if DEBUG
                    Util.Logger.LogDebug("调整士气", $"已显示士气提升消息，提升数量: {boostedCount}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                HandleError("调整士气", ex);
            }
        }

        private int UpdateAgentsMorale(Team team, float moraleThreshold, float moraleBoostRate)
        {
            int boostedCount = 0;
            var agentsToUpdate = team.ActiveAgents
                .Where(agent => SafetyChecks.IsValidAgent(agent) && 
                               !agent.IsPlayerControlled &&
                               (agent.GetMorale() < moraleThreshold || agent.IsRetreating()))
                .ToList();

            // 按Formation分组处理，避免重复设置Formation命令
            var formationGroups = agentsToUpdate.GroupBy(agent => agent.Formation);
            foreach (var formationGroup in formationGroups)
            {
                Formation formation = formationGroup.Key;
                if (formation?.PlayerOwner != null) continue;
                foreach (Agent agent in formationGroup)
                {
                    try
                    {
                        float oldMorale = agent.GetMorale();
                        if (oldMorale < moraleThreshold || agent.IsRetreating())
                        {
                            float targetMorale = oldMorale + moraleBoostRate;
                            if (agent.IsRetreating())
                            {
                                targetMorale += moraleBoostRate * 0.5f;
                            }
                            
                            targetMorale = Math.Min(targetMorale, 100f);
                            agent.SetMorale(targetMorale);

                            if (targetMorale > oldMorale)
                            {
                                boostedCount++;
                            }
                        }

                        if (agent.IsRetreating())
                        {
                            agent.StopRetreating();
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleError("AI士气更新", ex);
                    }
                }

                // 对每个Formation只设置一次命令
                if (formation != null && formationGroup.Any(a => a.IsRetreating()))
                {
                    PreventRetreat(formation);
                }
            }
            return boostedCount;
        }

        private bool ShouldAllowRetreat(Team attackerTeam, int attackerCount)
        {
            try
            {
                if (_isDisabled || !SafetyChecks.IsMissionValid() || attackerTeam == null)
                    return true;

                #if DEBUG
                Util.Logger.LogDebug("撤退检查", $"检查撤退条件 - 攻击方数量: {attackerCount}, " +
                    $"启用固定撤退: {Settings.Instance.EnableFixedRetreat}, 撤退阈值: {Settings.Instance.RetreatThreshold}, " +
                    $"启用比例撤退: {Settings.Instance.EnableRatioRetreat}");
                #endif

                // 优先使用固定数量触发
                if (Settings.Instance.EnableFixedRetreat)
                {
                    bool shouldRetreat = attackerCount <= Settings.Instance.RetreatThreshold;
                    #if DEBUG
                    Util.Logger.LogDebug("撤退检查", $"固定阈值检查 - 攻击方数量: {attackerCount} <= 阈值: {Settings.Instance.RetreatThreshold} = {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat)
                    {
                        StartDisableTimer("达到固定阈值");
                        return true;
                    }
                }
                // 如果没有启用固定数量，才检查比例触发
                else if (Settings.Instance.EnableRatioRetreat && _defenderTeam != null)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                    bool shouldRetreat = defenderCount > 0 && attackerCount < defenderCount * 0.7f;
                    
                    #if DEBUG
                    Util.Logger.LogDebug("撤退检查", $"比例阈值检查 - 攻击方数量: {attackerCount}, 防守方数量: {defenderCount}, " +
                        $"比例: {(defenderCount > 0 ? (float)attackerCount / defenderCount : 0):F2}, 是否撤退: {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat)
                    {
                        StartDisableTimer("达到比例阈值");
                        return true;
                    }
                }

                if (_pendingDisable)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("撤退检查", "取消待禁用状态");
                    #endif
                    _pendingDisable = false;
                    _disableTimer = 0f;
                }
                return false;
            }
            catch (Exception ex)
            {
                HandleError("撤退检查", ex);
                return true; // 出错时允许撤退
            }
        }
        #endregion

        #region Formation Methods
        /// <summary>
        /// 防止撤退
        /// </summary>
        private void PreventRetreat(Formation formation)
        {
            try
            {
                if (!SafetyChecks.IsValidFormation(formation)) return;
                
                // 检查是否是玩家控制的编队
                if (formation.PlayerOwner != null)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", "跳过玩家控制的编队");
                    #endif
                    return;
                }
                
                if (!_advancedFormations.Contains(formation))
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", 
                        $"编队类型: {formation.FormationIndex}");
                    #endif

                    formation.SetControlledByAI(true, false);
                    _advancedFormations.Add(formation);
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
                formation?.SetControlledByAI(true, false);
            }
            catch (Exception ex)
            {
                HandleError("restore formation", ex);
            }
        }

        private void RestoreAllFormations()
        {
            if (_advancedFormations == null) return;
            
            try
            {
                BehaviorCleanupHelper.CleanupFormations(_advancedFormations, RestoreFormation);
            }
            catch (Exception ex)
            {
                HandleError("formations restore", ex);
            }
        }
        #endregion

        #region State Management Methods
        private void StartDisableTimer(string reason)
        {
            try
            {
                if (!_pendingDisable && SafetyChecks.IsMissionValid())
                {
                    #if DEBUG
                    Util.Logger.LogDebug("开始禁用计时", $"开始禁用计时，原因: {reason}");
                    #endif
                    
                    _pendingDisable = true;
                    _disableTimer = 0f;
                    _initialAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    
                    if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= Constants.RETREAT_MESSAGE_COOLDOWN)
                    {
                        _lastRetreatMessageTime = Mission.Current.CurrentTime;
                        InformationManager.DisplayMessage(new InformationMessage(
                            Constants.RetreatMessage.ToString(),
                            Constants.WarningColor));
                        
                        #if DEBUG
                        Util.Logger.LogDebug("禁用计时", "显示撤退消息");
                        #endif
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("禁用计时", ex);
            }
        }

        private void ProcessDisableTimer(float dt)
        {
            try
            {
                _disableTimer += dt;
                
                // 检查增援情况
                int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                if (currentAttackerCount > _initialAttackerCount)
                {
                    _pendingDisable = false;
                    _disableTimer = 0f;
                    
                    if (Mission.Current.CurrentTime - _battleStartTime > Constants.BATTLE_START_GRACE_PERIOD)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            Constants.ReinforcementMessage.ToString(),
                            Constants.InfoColor));
                    }
                    return;
                }

                // 检查是否达到禁用延迟
                if (_disableTimer >= Settings.Instance.DisableDelay)
                {
                    if (ShouldAllowRetreat(Mission.Current.AttackerTeam, currentAttackerCount))
                    {
                        DisableMod("禁用计时过期");
                    }
                    
                    _pendingDisable = false;
                    _disableTimer = 0f;
                }
            }
            catch
            {
                _pendingDisable = false;
                _disableTimer = 0f;
            }
        }

        private void DisableMod(string reason)
        {
            try
            {
                if (!_isDisabled)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("禁用Mod", $"正在禁用mod，原因: {reason}");
                    #endif
                    _isDisabled = true;
                    _pendingDisable = false;
                    _disableTimer = 0f;
                    
                    if (!_missionEnding)
                    {
                        RestoreAllFormations();
                        ShowRetreatMessage(reason);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("禁用Mod", ex);
            }
        }
        #endregion

        #region Helper Methods
        private void ShowRetreatMessage(string reason)
        {
            try
            {
                if (!SafetyChecks.IsMissionValid()) return;
                
                if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= Constants.RETREAT_MESSAGE_COOLDOWN)
                {
                    _lastRetreatMessageTime = Mission.Current.CurrentTime;
                    InformationManager.DisplayMessage(new InformationMessage(
                        Constants.RetreatMessage.ToString(),
                        Constants.WarningColor));
                }
            }
            catch (Exception ex)
            {
                HandleError("显示撤退消息", ex);
            }
        }

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
                catch
                {
                    // 完全忽略
                }
                #endif
            }
        }
        #endregion
    }
} 