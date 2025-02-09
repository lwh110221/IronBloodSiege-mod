using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Behavior
{
    /// <summary>
    /// Main 管理其他行为类之间的通信和状态同步
    /// </summary>
    public class MainBehavior : MissionBehavior
    {
        #region Fields
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        // 缓存攻防双方的Team引用
        private Team _attackerTeam;
        private Team _defenderTeam;
        
        // 缓存当前场景名称
        private string _currentSceneName;
        
        private bool _isSiegeScene = false;      // 是否是攻城场景
        private bool _isDisabled = false;        // 是否禁用
        private bool _pendingDisable = false;    // 用于标记是否处于等待禁用状态
        private float _disableTimer = 0f;        // 禁用计时器
        private int _initialAttackerCount = 0;   // 开始计时时的攻击方士兵数量
        private int _lastCheckedAttackerCount = 0; // 上一次检测到的攻击方数量
        private float _lastRetreatMessageTime = 0f; // 撤退消息时间
        private bool _isBeingRemoved = false;    // 添加标记，防止重复执行
        private bool _missionEnding = false;     // 战斗结束标记
        private bool _isCleanedUp = false;       // 添加标记，确保只清理一次
        private readonly object _cleanupLock = new object();  // 线程锁
        private bool _wasEnabledBefore = false;  // 添加字段跟踪之前的启用状态

        // 撤退控制相关
        private const float MAX_DISABLE_WAIT_TIME = 120f; // 最大等待时间
        private const float MIN_CHECK_INTERVAL = 1f;      // 最小检查间隔
        private float _lastCheckTime = 0f;

        // 缓存其他行为组件的引用
        private SiegeFormationBehavior _formationBehavior;
        private SiegeMoraleManagerBehavior _moraleManagerBehavior;
        private SiegeReinforcementBehavior _reinforcementBehavior;
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
                
                // 如果mod未启用，直接返回
                if (!_wasEnabledBefore)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "Mod未启用，跳过初始化");
                    #endif
                    return;
                }
                
                // 初始化基本状态
                _currentMission = Mission.Current;
                _attackerTeam = _currentMission?.AttackerTeam;
                _defenderTeam = _currentMission?.DefenderTeam;
                _currentSceneName = _currentMission?.SceneName?.ToLowerInvariant() ?? string.Empty;

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

                // 如果最终确认不是攻城战场景，才禁用所有功能
                if (!_isSiegeScene)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "非攻城战场景，禁用所有功能");
                    #endif
                    DisableMod("非攻城战场景");
                    return;
                }

                // 获取其他行为组件的引用,添加重试机制
                int retryCount = 0;
                const int MAX_RETRY = 3;
                while (retryCount < MAX_RETRY)
                {
                    _formationBehavior = _currentMission?.GetMissionBehavior<SiegeFormationBehavior>();
                    _moraleManagerBehavior = _currentMission?.GetMissionBehavior<SiegeMoraleManagerBehavior>();
                    _reinforcementBehavior = _currentMission?.GetMissionBehavior<SiegeReinforcementBehavior>();

                    if (_formationBehavior != null && 
                        _moraleManagerBehavior != null && 
                        _reinforcementBehavior != null)
                    {
                        break;
                    }

                    retryCount++;
                    System.Threading.Thread.Sleep(100);
                }

                #if DEBUG
                Util.Logger.LogDebug("初始化", 
                    $"Mod初始化完成 - 场景: {_isSiegeScene}, " +
                    $"Formation行为: {_formationBehavior != null}, " +
                    $"士气管理: {_moraleManagerBehavior != null}, " +
                    $"援军管理: {_reinforcementBehavior != null}, " +
                    $"重试次数: {retryCount}");
                #endif

                if (_formationBehavior == null || 
                    _moraleManagerBehavior == null || 
                    _reinforcementBehavior == null)
                {
                    throw new Exception("无法获取所有必需的行为组件");
                }
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
                // 检查mod是否被禁用
                if (_missionEnding || _isDisabled)
                {
                    return;
                }

                // 检查总开关状态变化
                bool isCurrentlyEnabled = Settings.Instance.IsEnabled;
                if (_wasEnabledBefore != isCurrentlyEnabled)
                {
                    if (!isCurrentlyEnabled)
                    {
                        // 如果mod被关闭，执行清理
                        #if DEBUG
                        Util.Logger.LogDebug("Mod状态", "Mod被禁用，执行清理");
                        #endif
                        DisableMod("Mod总开关关闭");
                    }
                    _wasEnabledBefore = isCurrentlyEnabled;
                }

                // 如果mod未启用或任务无效，直接返回
                if (!isCurrentlyEnabled || !SafetyChecks.IsMissionValid())
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

                _attackerTeam = _currentMission.AttackerTeam;
                _defenderTeam = _currentMission.DefenderTeam;
                
                if (_attackerTeam == null)
                {
                    return;
                }

                // 检查是否需要允许撤退
                if (ShouldAllowRetreat(_attackerTeam, SafetyChecks.GetAttackerCount(_attackerTeam)))
                {
                    if (!_pendingDisable)
                    {
                        StartDisableTimer("达到撤退条件");
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("任务更新", ex);
                DisableMod("任务更新错误");
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
            catch (Exception)
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

                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        BehaviorCleanupHelper.CleanupBehaviorState(
                            ref _isCleanedUp,
                            ref _lastRetreatMessageTime,
                            ref _initialAttackerCount,
                            ref _currentMission,
                            ref _currentSceneName,
                            ref _attackerTeam,
                            ref _defenderTeam,
                            ref _formationBehavior,
                            ref _moraleManagerBehavior,
                            ref _reinforcementBehavior
                        );
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
                            ref _lastRetreatMessageTime,
                            ref _initialAttackerCount,
                            ref _currentMission,
                            ref _currentSceneName,
                            ref _attackerTeam,
                            ref _defenderTeam,
                            ref _formationBehavior,
                            ref _moraleManagerBehavior,
                            ref _reinforcementBehavior
                        );
                    }
                }

                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                HandleError("移除行为", ex);
                base.OnRemoveBehavior();
            }
            finally
            {
                _isBeingRemoved = false;
            }
        }
        #endregion

        #region Core Logic
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
                bool allowRetreat = _pendingDisable && _disableTimer >= Settings.Instance.DisableDelay;          
                return allowRetreat;
            }
            catch (Exception ex)
            {
                HandleError("撤退检查", ex);
                return false; // 出错时不允许撤退
            }
        }

        private void StartDisableTimer(string reason)
        {
            try
            {
                if (!_pendingDisable && SafetyChecks.IsMissionValid())
                {
                    #if DEBUG
                    Util.Logger.LogDebug("开始禁用计时", 
                        $"开始禁用计时 - 原因: {reason}, " +
                        $"当前攻击方数量: {SafetyChecks.GetAttackerCount(_attackerTeam)}, " +
                        $"设定延迟: {Settings.Instance.DisableDelay:F2}, " +
                        $"最大等待时间: {MAX_DISABLE_WAIT_TIME}");
                    #endif
                    
                    _pendingDisable = true;
                    _disableTimer = 0f;
                    _initialAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    _lastCheckedAttackerCount = _initialAttackerCount;
                    
                    if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= Constants.RETREAT_MESSAGE_COOLDOWN)
                    {
                        _lastRetreatMessageTime = Mission.Current.CurrentTime;
                        InformationManager.DisplayMessage(new InformationMessage(
                            Constants.RetreatMessage.ToString(),
                            Constants.WarningColor));
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

                // 更新计时器
                float previousTimer = _disableTimer;
                _disableTimer += dt;
                
                #if DEBUG
                // 每秒记录一次计时状态
                if (Math.Floor(_disableTimer) > Math.Floor(previousTimer))
                {
                    Util.Logger.LogDebug("禁用计时", 
                        $"禁用计时进行中 - 计时器: {_disableTimer:F2}/{Settings.Instance.DisableDelay:F2}, " +
                        $"等待状态: {_pendingDisable}, " +
                        $"最大等待时间: {MAX_DISABLE_WAIT_TIME}");
                }
                #endif
                
                // 检查是否达到最大等待时间
                if (_disableTimer >= MAX_DISABLE_WAIT_TIME)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("禁用计时", 
                        $"达到最大等待时间 - 计时器: {_disableTimer:F2}, " +
                        $"最大等待时间: {MAX_DISABLE_WAIT_TIME}");
                    #endif
                    DisableMod("达到最大等待时间");
                    return;
                }
                
                // 每秒检查一次是否有援军到达
                if (Math.Floor(_disableTimer) > Math.Floor(previousTimer))
                {
                    int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    
                    #if DEBUG
                    Util.Logger.LogDebug("禁用计时", 
                        $"检查援军 - 当前数量: {currentAttackerCount}, " +
                        $"上次数量: {_lastCheckedAttackerCount}, " +
                        $"初始数量: {_initialAttackerCount}");
                    #endif
                    
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
                        return;
                    }
                    
                    // 更新上一次检测的数量
                    _lastCheckedAttackerCount = currentAttackerCount;
                }

                // 只有在倒计时结束且没有检测到援军时才禁用mod
                if (_disableTimer >= Settings.Instance.DisableDelay)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("禁用计时", 
                        $"禁用计时结束 - 计时器: {_disableTimer:F2}, " +
                        $"设定延迟: {Settings.Instance.DisableDelay:F2}, " +
                        $"当前攻击方数量: {SafetyChecks.GetAttackerCount(_attackerTeam)}");
                    #endif
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
                    Util.Logger.LogDebug("禁用Mod", 
                        $"正在禁用mod - 原因: {reason}, " +
                        $"当前攻击方数量: {SafetyChecks.GetAttackerCount(_attackerTeam)}, " +
                        $"计时器状态: {_disableTimer:F2}, " +
                        $"等待禁用状态: {_pendingDisable}, " +
                        $"战斗结束状态: {_missionEnding}");
                    #endif

                    _isDisabled = true;
                    _pendingDisable = false;
                    _disableTimer = 0f;

                    // 通知其他行为组件
                    try
                    {
                        // Formation行为
                        if (_formationBehavior != null)
                        {
                            _formationBehavior.OnModDisabled();
                        }

                        // 士气管理行为
                        if (_moraleManagerBehavior != null)
                        {
                            _moraleManagerBehavior.OnModDisabled();
                        }

                        // 援军生成行为
                        if (_reinforcementBehavior != null)
                        {
                            _reinforcementBehavior.OnModDisabled();
                        }

                        #if DEBUG
                        Util.Logger.LogDebug("禁用Mod", "所有行为组件已通知禁用");
                        #endif
                    }
                    catch (Exception ex)
                    {
                        #if DEBUG
                        Util.Logger.LogError("禁用行为组件", ex);
                        #endif
                    }

                    if (!_missionEnding)
                    {
                        ShowRetreatMessage(reason);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("禁用Mod", ex);
            }
        }

        public void OnModDisabled()
        {
            try
            {
                _isDisabled = true;
                
                // 同步禁用所有子行为
                if (_formationBehavior != null)
                {
                    _formationBehavior.OnModDisabled();
                }
                
                if (_moraleManagerBehavior != null)
                {
                    _moraleManagerBehavior.OnModDisabled();
                }
                
                if (_reinforcementBehavior != null)
                {
                    _reinforcementBehavior.OnModDisabled();
                }
                
                // 清理状态
                CleanupState();
                
                #if DEBUG
                Util.Logger.LogDebug("禁用Mod", "所有行为组件已禁用");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("mod disabled", ex);
            }
        }

        public void OnModEnabled()
        {
            try
            {
                _isDisabled = false;
                
                // 同步启用所有子行为
                if (_formationBehavior != null)
                {
                    _formationBehavior.OnModEnabled();
                }
                
                if (_moraleManagerBehavior != null)
                {
                    _moraleManagerBehavior.OnModEnabled();
                }
                
                if (_reinforcementBehavior != null)
                {
                    _reinforcementBehavior.OnModEnabled();
                }
                
                #if DEBUG
                Util.Logger.LogDebug("启用Mod", "所有行为组件已启用");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("mod enabled", ex);
            }
        }

        private void CleanupState()
        {
            try
            {
                // 重置所有状态
                _isSiegeScene = false;
                _currentMission = null;
                _attackerTeam = null;
                _defenderTeam = null;
                _currentSceneName = string.Empty;
                
                // 清除行为组件引用
                _formationBehavior = null;
                _moraleManagerBehavior = null;
                _reinforcementBehavior = null;
                
                #if DEBUG
                Util.Logger.LogDebug("清理状态", "所有状态已重置");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("cleanup state", ex);
            }
        }

        public void OnSceneChanged()
        {
            try
            {
                // 通知所有子行为场景变化
                if (_formationBehavior != null)
                {
                    _formationBehavior.OnSceneChanged();
                }
                
                if (_moraleManagerBehavior != null)
                {
                    _moraleManagerBehavior.OnSceneChanged();
                }
                
                if (_reinforcementBehavior != null)
                {
                    _reinforcementBehavior.OnSceneChanged();
                }
                
                // 重置场景相关状态
                _isSiegeScene = false;
                _currentSceneName = string.Empty;
                
                #if DEBUG
                Util.Logger.LogDebug("场景变化", "所有行为组件已通知场景变化");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("scene changed", ex);
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
                catch { }
                #endif
            }
        }
        #endregion
    }
} 