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
    public class SiegeMoraleBehavior : MissionBehavior, IMissionAgentSpawnLogic
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
        private int _lastCheckedAttackerCount = 0; // 上一次检测到的攻击方数量
        private float _lastMoraleUpdateTime = 0f; // 最后一次更新士气的时间
        private float _lastMessageTime = 0f; // 消息时间
        private float _lastRetreatMessageTime = 0f; // 撤退消息时间
        private bool _isBeingRemoved = false;  // 添加标记，防止重复执行
        private bool _missionEnding = false; // 战斗结束标记
        private bool _isCleanedUp = false;  // 添加标记，确保只清理一次
        private readonly object _cleanupLock = new object();  // 线程锁
        private float _battleStartTime = 0f; // 战斗开始时间

        // 添加缓存相关字段
        private readonly Dictionary<int, int> _teamCountCache = new Dictionary<int, int>();
        private const float CACHE_UPDATE_INTERVAL = 5f;

        // 援军生成相关
        private float _nextSpawnTime = 0f;                     // 下次生成援军的时间
        private const float SPAWN_INTERVAL = 5f;               // 援军生成的时间间隔
        private const float SPAWN_CHECK_INTERVAL = 0.5f;       // 检查是否生成援军的时间间隔
        private const int SPAWN_BATCH_SIZE = 50;               // 每次生成援军的数量
        private const int MAX_TOTAL_ATTACKERS = 999999999;     // 攻击方最大数量限制
        private bool _isSpawnerEnabled = true;                 // 标记是否启用援军修改

        // 撤退控制相关
        private const float MAX_DISABLE_WAIT_TIME = 120f; // 最大等待时间
        private const float MIN_CHECK_INTERVAL = 1f; // 最小检查间隔
        private float _lastCheckTime = 0f;
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

                // 设置初始生成时间
                _nextSpawnTime = _battleStartTime;
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

                // 处理援军生成
                if (_isSiegeScene && !_isDisabled && _isSpawnerEnabled)
                {
                    ProcessReinforcements(dt);
                }
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
                    
                // 设置最高士气
                agent.SetMorale(100f);
                
                // 禁用撤退标志
                agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRetreat);
                
                // 设置为进攻性AI行为
                agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefensiveArrangementMove);
                
                // 设置防御性为0
                agent.Defensiveness = 0f;
                
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
            catch (Exception)  // 移除未使用的ex变量
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

                _teamCountCache?.Clear();

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
            catch (Exception)  // 移除未使用的ex变量
            {
                #if DEBUG
                Util.Logger.LogDebug("任务结束", "任务结束时发生错误");
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
            _lastCheckedAttackerCount = 0;
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
                // 确保只对攻城方生效
                if (team == null || team != _attackerTeam || team.IsDefender) return;

                float currentTime = _currentMission.CurrentTime;
                
                if (currentTime - _lastMoraleUpdateTime < Constants.MORALE_UPDATE_INTERVAL)
                    return;

                bool isPlayerAttacker = SafetyChecks.IsPlayerAttacker();
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

                int boostedCount = UpdateAgentsMorale(team, Settings.Instance.MoraleThreshold, Settings.Instance.MoraleBoostRate);
                
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
                
                bool hasRetreatingUnits = false;
                foreach (Agent agent in formationGroup)
                {
                    try
                    {
                        float oldMorale = agent.GetMorale();
                        if (oldMorale < moraleThreshold || agent.IsRetreating())
                        {
                            // 直接使用设置的提升值
                            float targetMorale = oldMorale + moraleBoostRate;
                            targetMorale = Math.Min(targetMorale, 100f);
                            agent.SetMorale(targetMorale);

                            if (targetMorale > oldMorale)
                            {
                                boostedCount++;
                            }

                            if (agent.IsRetreating())
                            {
                                hasRetreatingUnits = true;
                                agent.StopRetreating();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleError("AI士气更新", ex);
                    }
                }

                // 对每个Formation只设置一次命令
                if (formation != null && hasRetreatingUnits)
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

                float currentTime = Mission.Current.CurrentTime;
                
                // 检查最大等待时间
                if (_pendingDisable && currentTime - _disableTimer > MAX_DISABLE_WAIT_TIME)
                {
                    return true;
                }

                // 检查最小间隔
                if (currentTime - _lastCheckTime < MIN_CHECK_INTERVAL)
                {
                    return false;
                }
                _lastCheckTime = currentTime;

                #if DEBUG
                Util.Logger.LogDebug("撤退检查", $"检查撤退条件 - 攻击方数量: {attackerCount}, " +
                    $"上一次检测数量: {_lastCheckedAttackerCount}, " +
                    $"启用固定撤退: {Settings.Instance.EnableFixedRetreat}, " +
                    $"撤退阈值: {Settings.Instance.RetreatThreshold}, " +
                    $"启用比例撤退: {Settings.Instance.EnableRatioRetreat}");
                #endif

                // 如果正在等待禁用，检查是否有援军到达
                if (_pendingDisable)
                {
                    // 只要当前数量比上次检测的数量多，就认为有援军到达
                    if (attackerCount > _lastCheckedAttackerCount)
                    {
                        #if DEBUG
                        Util.Logger.LogDebug("撤退检查", 
                            $"检测到援军到达 - 当前数量: {attackerCount}, " +
                            $"上一次检测数量: {_lastCheckedAttackerCount}, " +
                            $"增加数量: {attackerCount - _lastCheckedAttackerCount}");
                        #endif
                        _pendingDisable = false;
                        _disableTimer = 0f;
                        InformationManager.DisplayMessage(new InformationMessage(
                            Constants.ReinforcementMessage.ToString(),
                            Constants.InfoColor));
                        _lastCheckedAttackerCount = attackerCount;
                        return false;
                    }
                }

                // 更新上一次检测的数量
                _lastCheckedAttackerCount = attackerCount;

                // 优先使用固定数量触发
                if (Settings.Instance.EnableFixedRetreat)
                {
                    bool shouldRetreat = attackerCount <= Settings.Instance.RetreatThreshold;
                    #if DEBUG
                    Util.Logger.LogDebug("撤退检查", $"固定阈值检查 - 攻击方数量: {attackerCount} <= 阈值: {Settings.Instance.RetreatThreshold} = {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat && !_pendingDisable)
                    {
                        StartDisableTimer("达到固定阈值");
                    }
                }
                // 如果没有启用固定数量，才检查比例触发
                else if (Settings.Instance.EnableRatioRetreat && _defenderTeam != null)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                    bool shouldRetreat = defenderCount > 0 && 
                        attackerCount < defenderCount * Settings.Instance.RatioThreshold;
                    
                    #if DEBUG
                    Util.Logger.LogDebug("撤退检查", $"比例阈值检查 - 攻击方数量: {attackerCount}, 防守方数量: {defenderCount}, " +
                        $"比例: {(defenderCount > 0 ? (float)attackerCount / defenderCount : 0):F2}, " +
                        $"阈值: {Settings.Instance.RatioThreshold:F2}, " +
                        $"是否撤退: {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat && !_pendingDisable)
                    {
                        StartDisableTimer("达到比例阈值");
                    }
                }

                // 只有在倒计时结束且没有检测到援军时才允许撤退
                return _pendingDisable && _disableTimer >= Settings.Instance.DisableDelay;
            }
            catch (Exception ex)
            {
                HandleError("撤退检查", ex);
                return false; // 出错时不允许撤退
            }
        }
        #endregion

        #region Formation Methods
        /// <summary>
        /// 防止撤退并恢复Formation的移动
        /// </summary>
        private void PreventRetreat(Formation formation)
        {
            try
            {
                if (!SafetyChecks.IsValidFormation(formation)) return;
                
                if (formation.Team != _attackerTeam)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("防止撤退", 
                        $"跳过非攻城方Formation: {formation.FormationIndex}");
                    #endif
                    return;
                }
                         
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
                        $"编队类型: {formation.FormationIndex}");
                    #endif

                    // 重置Formation的AI行为
                    formation.AI.ResetBehaviorWeights();
                    
                    // 设置为进攻命令
                    formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    
                    // 确保Formation的AI会在下一个tick重新评估行为
                    formation.IsAITickedAfterSplit = false;
                    
                    // 添加到已处理列表
                    _advancedFormations.Add(formation);
                    
                    // 处理所有单位
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        // 停止当前的撤退状态
                        if (agent.IsRetreating())
                        {
                            agent.StopRetreating();
                        }
                        
                        // 设置最高士气
                        agent.SetMorale(100f);
                        
                        // 禁用撤退标志
                        agent.SetAgentFlags(agent.GetAgentFlags() & ~AgentFlag.CanRetreat);
                        
                        // 设置为进攻性AI行为
                        agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefensiveArrangementMove);
                        
                        // 设置防御性为0,确保进攻
                        agent.Defensiveness = 0f;
                    });
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
                if (formation == null) return;
                
                // 确保只对攻城方生效
                if (formation.Team != _attackerTeam) return;
                
                // 重置所有行为权重
                formation.AI.ResetBehaviorWeights();
                
                // 恢复AI控制
                formation.SetControlledByAI(true, false);
                
                // 如果Formation处于停止状态，设置为前进命令
                var currentOrder = formation.GetReadonlyMovementOrderReference();
                if (currentOrder.OrderEnum == MovementOrder.MovementOrderEnum.Stop)
                {
                    formation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
                }
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
                if (!_pendingDisable || !SafetyChecks.IsMissionValid()) return;

                _disableTimer += dt;
                
                // 每秒检查一次是否有援军到达
                if (_disableTimer % 1f < dt)
                {
                    int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    // 只要当前数量比上次检测的数量多，就认为有援军到达
                    if (currentAttackerCount > _lastCheckedAttackerCount)
                    {
                        #if DEBUG
                        Util.Logger.LogDebug("禁用计时", 
                            $"检测到援军到达 - 当前数量: {currentAttackerCount}, " +
                            $"上一次检测数量: {_lastCheckedAttackerCount}, " +
                            $"增加数量: {currentAttackerCount - _lastCheckedAttackerCount}");
                        #endif
                        _pendingDisable = false;
                        _disableTimer = 0f;
                        InformationManager.DisplayMessage(new InformationMessage(
                            Constants.ReinforcementMessage.ToString(),
                            Constants.InfoColor));
                        _lastCheckedAttackerCount = currentAttackerCount;
                        
                        // 给所有Formation下达进攻命令
                        foreach (var formation in _attackerTeam.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0))
                        {
                            PreventRetreat(formation);  // 使用PreventRetreat来处理每个Formation
                        }
                        return;
                    }
                    // 更新上一次检测的数量
                    _lastCheckedAttackerCount = currentAttackerCount;
                }

                // 只有在倒计时结束且没有检测到援军时才禁用mod
                if (_disableTimer >= Settings.Instance.DisableDelay)
                {
                    DisableMod("禁用计时结束，无援军到达");
                }
            }
            catch (Exception ex)
            {
                HandleError("禁用计时处理", ex);
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

        #region Reinforcement Methods
        private void ProcessReinforcements(float dt)
        {
            try
            {
                // 如果未启用激进援军设置，直接返回
                if (!Settings.Instance.EnableAggressiveReinforcement) return;

                if (_attackerTeam == null || _currentMission == null) return;

                float currentTime = _currentMission.CurrentTime;
                if (currentTime - _lastCheckTime < SPAWN_CHECK_INTERVAL) return;
                _lastCheckTime = currentTime;

                if (currentTime < _nextSpawnTime) return;

                // 获取生成逻辑组件
                var spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
                if (spawnLogic == null) return;

                // 获取当前战场信息
                int currentAttackerCount = spawnLogic.NumberOfActiveAttackerTroops;
                int remainingAttackers = spawnLogic.NumberOfRemainingAttackerTroops;
                int battleSize = spawnLogic.BattleSize;
                
                // 如果没有剩余可生成的士兵,直接返回
                if (remainingAttackers <= 0) return;

                // 计算当前战场还能容纳多少士兵
                int maxAttackers = battleSize / 2; // 假设双方各占一半
                int availableSpace = maxAttackers - currentAttackerCount;

                // 如果没有可用空间,直接返回
                if (availableSpace <= 0) return;

                // 计算这次应该生成多少援军
                int spawnCount = Math.Min(Math.Min(availableSpace, SPAWN_BATCH_SIZE), remainingAttackers);

                if (spawnCount > 0)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("援军生成", 
                        $"正在生成援军 - 当前数量: {currentAttackerCount}, " +
                        $"本次生成: {spawnCount}, " +
                        $"剩余可生成: {remainingAttackers}, " +
                        $"战场容量: {battleSize}, " +
                        $"可用空间: {availableSpace}");
                    #endif

                    // 启动生成器
                    spawnLogic.StartSpawner(BattleSideEnum.Attacker);
                    _nextSpawnTime = currentTime + SPAWN_INTERVAL;
                }
            }
            catch (Exception ex)
            {
                HandleError("援军生成处理", ex);
            }
        }

        #region IMissionAgentSpawnLogic Implementation
        public void StartSpawner(BattleSideEnum side)
        {
            if (side == BattleSideEnum.Attacker)
            {
                _isSpawnerEnabled = true;
            }
        }

        public void StopSpawner(BattleSideEnum side)
        {
            if (side == BattleSideEnum.Attacker)
            {
                _isSpawnerEnabled = false;
            }
        }

        public bool IsSideSpawnEnabled(BattleSideEnum side)
        {
            if (side == BattleSideEnum.Attacker)
            {
                return _isSpawnerEnabled;
            }
            return false;
        }

        public bool IsSideDepleted(BattleSideEnum side)
        {
            if (side == BattleSideEnum.Attacker)
            {
                int currentAttackerCount = SafetyChecks.GetAttackerCount(_attackerTeam);
                return currentAttackerCount >= MAX_TOTAL_ATTACKERS;
            }
            return false;
        }

        public float GetReinforcementInterval()
        {
            return SPAWN_INTERVAL;
        }
        #endregion
        #endregion
    }
} 