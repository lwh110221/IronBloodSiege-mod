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
    public class SiegeMoraleBehavior : MissionBehavior
    {
        // 使用HashSet存储Formation
        private HashSet<Formation> _chargedFormations;
        
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

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

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
            
            if (_chargedFormations == null)
            {
                _chargedFormations = new HashSet<Formation>();
            }
            else
            {
                _chargedFormations.Clear();
            }
        }

        private bool CheckIfSiegeScene()
        {
            if (_currentMission == null) 
                return false;
            
            return _currentMission.Mode == MissionMode.Battle && 
                   !string.IsNullOrEmpty(_currentSceneName) &&
                   (_currentSceneName.Contains("siege") || _currentSceneName.Contains("castle")) &&
                   _defenderTeam != null &&
                   _attackerTeam != null;
        }

        private void PreventRetreat(Formation formation)
        {
            try
            {
                if (!SafetyChecks.IsValidFormation(formation)) return;
                
                if (!_chargedFormations.Contains(formation))
                {
                    formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    _chargedFormations.Add(formation);
                }
            }
            catch (Exception ex)
            {
                HandleError("prevent retreat", ex);
            }
        }

        private void RestoreFormation(Formation formation)
        {
            try
            {
                if (formation != null)
                {
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                }
            }
            catch (Exception ex)
            {
                HandleError("restore formation", ex);
            }
        }

        private void RestoreAllFormations()
        {
            if (_chargedFormations == null) return;
            
            try
            {
                var formationsToRestore = _chargedFormations.ToList();
                foreach (Formation formation in formationsToRestore)
                {
                    RestoreFormation(formation);
                }
                _chargedFormations.Clear();
            }
            catch (Exception ex)
            {
                HandleError("formations restore", ex);
            }
        }

        private bool ShouldAllowRetreat(Team attackerTeam, int attackerCount)
        {
            try
            {
                if (_isDisabled || !SafetyChecks.IsMissionValid() || attackerTeam == null)
                    return true;

                #if DEBUG
                Util.Logger.LogDebug("ShouldAllowRetreat", $"Checking retreat conditions - AttackerCount: {attackerCount}, " +
                    $"EnableFixedRetreat: {Settings.Instance.EnableFixedRetreat}, RetreatThreshold: {Settings.Instance.RetreatThreshold}, " +
                    $"EnableRatioRetreat: {Settings.Instance.EnableRatioRetreat}");
                #endif

                // 优先使用固定数量触发
                if (Settings.Instance.EnableFixedRetreat)
                {
                    bool shouldRetreat = attackerCount <= Settings.Instance.RetreatThreshold;
                    #if DEBUG
                    Util.Logger.LogDebug("ShouldAllowRetreat", $"Fixed threshold check - AttackerCount: {attackerCount} <= Threshold: {Settings.Instance.RetreatThreshold} = {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat)
                    {
                        StartDisableTimer("Fixed threshold reached");
                        return true;
                    }
                }
                // 如果没有启用固定数量，才检查比例触发
                else if (Settings.Instance.EnableRatioRetreat && _defenderTeam != null)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                    bool shouldRetreat = defenderCount > 0 && attackerCount < defenderCount * 0.7f;
                    
                    #if DEBUG
                    Util.Logger.LogDebug("ShouldAllowRetreat", $"Ratio threshold check - AttackerCount: {attackerCount}, DefenderCount: {defenderCount}, " +
                        $"Ratio: {(defenderCount > 0 ? (float)attackerCount / defenderCount : 0):F2}, ShouldRetreat: {shouldRetreat}");
                    #endif
                    
                    if (shouldRetreat)
                    {
                        StartDisableTimer("Ratio threshold reached");
                        return true;
                    }
                }

                // 如果条件不满足，取消待禁用状态
                if (_pendingDisable)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("ShouldAllowRetreat", "Cancelling pending disable state");
                    #endif
                    _pendingDisable = false;
                    _disableTimer = 0f;
                }
                return false;
            }
            catch (Exception ex)
            {
                HandleError("retreat check", ex);
                return true; // 出错时允许撤退
            }
        }

        private void StartDisableTimer(string reason)
        {
            try
            {
                if (!_pendingDisable && SafetyChecks.IsMissionValid())
                {
                    #if DEBUG
                    Util.Logger.LogDebug("StartDisableTimer", $"Starting disable timer with reason: {reason}");
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
                        Util.Logger.LogDebug("StartDisableTimer", "Retreat message displayed");
                        #endif
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("start disable timer", ex);
            }
        }

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
                HandleError("show retreat message", ex);
            }
        }

        private void DisableMod(string reason)
        {
            try
            {
                if (!_isDisabled)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("DisableMod", $"Disabling mod with reason: {reason}");
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
                HandleError("disable mod", ex);
            }
        }

        private void AdjustTeamMorale(Team team, float dt)
        {
            try
            {
                if (team == null || team != _attackerTeam) return;

                float currentTime = _currentMission.CurrentTime;
                
                if (currentTime - _lastMoraleUpdateTime < Constants.MORALE_UPDATE_INTERVAL)
                    return;

                _lastMoraleUpdateTime = currentTime;
                
                float moraleThreshold = Settings.Instance.MoraleThreshold;
                float moraleBoostRate = Settings.Instance.MoraleBoostRate;
                
                int attackerCount = SafetyChecks.GetAttackerCount(team);
                
                #if DEBUG
                Util.Logger.LogDebug("AdjustTeamMorale", $"Current attacker count: {attackerCount}");
                #endif

                if (ShouldAllowRetreat(team, attackerCount))
                {
                    #if DEBUG
                    Util.Logger.LogDebug("AdjustTeamMorale", "Retreat conditions met, skipping morale adjustment");
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
                    Util.Logger.LogDebug("AdjustTeamMorale", $"Displayed morale boost message for {boostedCount} troops");
                    #endif
                }
            }
            catch (Exception ex)
            {
                HandleError("adjust team morale", ex);
            }
        }

        private int UpdateAgentsMorale(Team team, float moraleThreshold, float moraleBoostRate)
        {
            int boostedCount = 0;
            var agentsToUpdate = team.ActiveAgents
                .Where(agent => SafetyChecks.IsValidAgent(agent) && 
                               (agent.GetMorale() < moraleThreshold || agent.IsRetreating()))
                .ToList();

            foreach (Agent agent in agentsToUpdate)
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
                        Formation formation = agent.Formation;
                        if (formation != null)
                        {
                            PreventRetreat(formation);
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleError("agent morale update", ex);
                }
            }
            return boostedCount;
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

        public override void OnAgentFleeing(Agent agent)
        {
            base.OnAgentFleeing(agent);
            try
            {
                if (!_isSiegeScene || !SafetyChecks.IsValidAgent(agent) || 
                    agent.Team != Mission.Current?.AttackerTeam) return;

                int attackerCount = SafetyChecks.GetAttackerCount(agent.Team);
                if (ShouldAllowRetreat(agent.Team, attackerCount))
                {
                    return;
                }

                float targetMorale = Settings.Instance.MoraleThreshold + Settings.Instance.MoraleBoostRate;
                if (targetMorale > 100f) targetMorale = 100f;
                
                agent.SetMorale(targetMorale);
                agent.StopRetreating();
                
                if (agent.Formation != null)
                {
                    PreventRetreat(agent.Formation);
                }
                
                if (Mission.Current.CurrentTime - _lastMessageTime >= Constants.MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = Mission.Current.CurrentTime;
                    InformationManager.DisplayMessage(new InformationMessage(
                        Constants.PreventRetreatMessage.ToString(),
                        Constants.WarningColor));
                }
            }
            catch (Exception ex)
            {
                HandleError("agent fleeing", ex);
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
                        Util.Logger.LogDebug("MissionTick", "Battle ended detected");
                        #endif
                        _missionEnding = true;
                        DisableMod("Battle ended");
                    }
                    return;
                }

                if (_pendingDisable)
                {
                    ProcessDisableTimer(dt);
                    return;
                }

                if (_currentMission.CurrentTime % 0.5f < dt)
                {
                    bool previousSceneState = _isSiegeScene;
                    _isSiegeScene = SafetyChecks.IsSiegeSceneValid();
                    
                    #if DEBUG
                    if (_currentMission.CurrentTime % 10 < dt)
                    {
                        Util.Logger.LogDebug("MissionTick", $"Scene state - IsSiege: {_isSiegeScene}, IsEnabled: {Settings.Instance.IsEnabled}, " +
                            $"EnableFixedRetreat: {Settings.Instance.EnableFixedRetreat}, RetreatThreshold: {Settings.Instance.RetreatThreshold}");
                        
                        if (_attackerTeam != null)
                        {
                            int currentAttackerCount = SafetyChecks.GetAttackerCount(_attackerTeam);
                            Util.Logger.LogDebug("MissionTick", $"Current attacker count: {currentAttackerCount}");
                        }
                    }
                    #endif
                    
                    if (previousSceneState && !_isSiegeScene)
                    {
                        DisableMod("Siege scene ended");
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
                HandleError("mission tick", ex);
                DisableMod("Mission tick error");
            }
        }

        private void ProcessDisableTimer(float dt)
        {
            try
            {
                _disableTimer += dt;
                if (_disableTimer >= Settings.Instance.DisableDelay)
                {
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

                    if (ShouldAllowRetreat(Mission.Current.AttackerTeam, currentAttackerCount))
                    {
                        DisableMod("Disable timer expired");
                    }
                    
                    _pendingDisable = false;
                    _disableTimer = 0f;
                }
                else
                {
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
                    }
                }
            }
            catch
            {
                _pendingDisable = false;
                _disableTimer = 0f;
            }
        }

        public override void OnClearScene()
        {
            try
            {
                if (!_missionEnding)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("OnClearScene", "Scene clearing started");
                    #endif
                    _missionEnding = true;
                    DisableMod("Scene clearing");
                }
                base.OnClearScene();
            }
            catch
            {
                #if DEBUG
                Util.Logger.LogDebug("OnClearScene", "Error during scene clearing");
                #endif
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

        protected override void OnEndMission()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("OnEndMission", "Starting mission end cleanup");
                #endif

                _missionEnding = true;
                _isDisabled = true;

                if (_chargedFormations != null)
                {
                    try
                    {
                        foreach (var formation in _chargedFormations.ToList())
                        {
                            if (formation != null)
                            {
                                try
                                {
                                    RestoreFormation(formation);
                                }
                                catch
                                {
                                    // 忽略单个Formation的错误
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略Formation清理的错误
                    }
                    finally
                    {
                        _chargedFormations.Clear();
                        _chargedFormations = null;
                    }
                }

                _pendingDisable = false;
                _disableTimer = 0f;
                _isSiegeScene = false;
                _initialAttackerCount = 0;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _lastRetreatMessageTime = 0f;

                _attackerTeam = null;
                _defenderTeam = null;
                _currentMission = null;
                _currentSceneName = null;

                #if DEBUG
                Util.Logger.LogDebug("OnEndMission", "Cleanup completed, calling base OnEndMission");
                #endif

                base.OnEndMission();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("OnEndMission", ex);
                #endif
                
                base.OnEndMission();
            }
            finally
            {
                _isCleanedUp = true;
                _isBeingRemoved = false;
                
                #if DEBUG
                Util.Logger.LogDebug("OnEndMission", "Mission end cleanup finished");
                #endif
            }
        }

        public override void OnEndMissionInternal()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("OnEndMissionInternal", "Starting internal mission end cleanup");
                #endif

                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        if (!_isCleanedUp)
                        {
                            _lastMoraleUpdateTime = 0f;
                            _lastMessageTime = 0f;
                            _lastRetreatMessageTime = 0f;
                            _initialAttackerCount = 0;
                            _currentMission = null;
                            _currentSceneName = null;
                            _attackerTeam = null;
                            _defenderTeam = null;
                            
                            if (_chargedFormations != null)
                            {
                                _chargedFormations.Clear();
                                _chargedFormations = null;
                            }
                            
                            _isCleanedUp = true;
                        }
                    }
                }

                #if DEBUG
                Util.Logger.LogDebug("OnEndMissionInternal", "Calling base OnEndMissionInternal");
                #endif

                base.OnEndMissionInternal();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("OnEndMissionInternal", ex);
                #endif
                
                base.OnEndMissionInternal();
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_isBeingRemoved)
            {
                #if DEBUG
                Util.Logger.LogDebug("OnRemoveBehavior", "Behavior already being removed, skipping");
                #endif
                return;
            }

            try
            {
                #if DEBUG
                Util.Logger.LogDebug("OnRemoveBehavior", "Starting behavior removal");
                #endif

                _isBeingRemoved = true;
                
                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        if (!_isCleanedUp)
                        {
                            if (_chargedFormations != null)
                            {
                                _chargedFormations.Clear();
                                _chargedFormations = null;
                            }
                            _currentMission = null;
                            _attackerTeam = null;
                            _defenderTeam = null;
                            _currentSceneName = null;
                            _isCleanedUp = true;
                        }
                    }
                }

                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("OnRemoveBehavior", ex);
                #endif
                
                base.OnRemoveBehavior();
            }
            finally
            {
                _isBeingRemoved = false;
                
                #if DEBUG
                Util.Logger.LogDebug("OnRemoveBehavior", "Behavior removal completed");
                #endif
            }
        }
    }
} 