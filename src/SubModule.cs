// 铁血攻城
// 作者：Ahao
// 版本：1.0.0
// email ：2285813721@qq.com，ahao221x@gmail.com
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using System;
using System.Linq;
using System.Collections.Generic;

namespace IronBloodSiege
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        public override void OnInitialState()
        {
            base.OnInitialState();
            ShowModInfo();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            ShowModInfo();
        }

        private void ShowModInfo()
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_mod_loaded}IronBlood Siege - Loaded Successfully! Author: Ahao").ToString(), 
                    Color.FromUint(0x0000FFFF)));
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_mod_email}email: ahao221x@gmail.com").ToString(), 
                    Color.FromUint(0x0000FFFF)));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_error_display}IronBlood Siege display error: {MESSAGE}")
                        .SetTextVariable("MESSAGE", ex.Message)
                        .ToString(),
                    Color.FromUint(0xFF0000FF)));
            }
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            try
            {
                if (mission != null)
                {
                    #if DEBUG
                    Logger.LogDebug("OnBeforeMissionBehaviorInitialize", 
                        $"Mission Mode: {mission.Mode}, " +
                        $"Scene Name: {mission.SceneName}, " +
                        $"IsSiegeBattle: {mission.IsSiegeBattle}, " +
                        $"IsSallyOutBattle: {mission.IsSallyOutBattle}");
                    #endif

                    mission.AddMissionBehavior(new SiegeMoraleBehavior());
                    if (Settings.Instance.IsEnabled)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=ibs_mod_enabled}IronBlood Siege is enabled").ToString(), 
                            Color.FromUint(0x00FF00FF)));
                    }
                    else 
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=ibs_mod_disabled}IronBlood Siege is disabled").ToString(), 
                            Color.FromUint(0xFF0000FF)));
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_error_behavior}IronBlood Siege behavior error: {MESSAGE}")
                        .SetTextVariable("MESSAGE", ex.Message)
                        .ToString()));
            }
        }
    }

    public class SiegeMoraleBehavior : MissionBehavior
    {
        // 缓存常用的颜色值
        private static readonly Color WarningColor = Color.FromUint(0xFFFF00FF);
        private static readonly Color ErrorColor = Color.FromUint(0xFF0000FF);
        private static readonly Color InfoColor = Color.FromUint(0x00FF00FF);
        private static readonly Color NormalColor = Color.FromUint(0x000000FF);

        // 缓存常用的消息模板
        private static readonly TextObject MoraleBoostMessage = new TextObject("{=ibs_morale_boost}IronBlood Siege: {COUNT} troops were inspired");
        private static readonly TextObject PreventRetreatMessage = new TextObject("{=ibs_prevent_retreat}IronBlood Siege: Prevented troops from retreating!");
        private static readonly TextObject RetreatMessage = new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled");
        private static readonly TextObject ErrorMessage = new TextObject("{=ibs_error_general}IronBlood Siege {CONTEXT} error: {MESSAGE}");

        // 使用更高效的HashSet存储Formation
        private HashSet<Formation> _chargedFormations;
        
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        // 缓存攻防双方的Team引用
        private Team _attackerTeam;
        private Team _defenderTeam;
        
        // 缓存当前场景名称的小写形式
        private string _currentSceneName;
        
        private bool _isSiegeScene = false;
        private bool _isDisabled = false;
        private bool _pendingDisable = false;    // 用于标记是否处于等待禁用状态
        private float _disableTimer = 0f;        // 禁用计时器
        //private const float DISABLE_DELAY = 15f; // 禁用延迟时间
        private int _initialAttackerCount = 0;   // 开始计时时的攻击方士兵数量
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;
        private float _lastMessageTime = 0f;
        private const float MESSAGE_COOLDOWN = 10f;
        private const float RETREAT_MESSAGE_COOLDOWN = 25f;  // 不再铁血消息的冷却时间
        private float _lastRetreatMessageTime = 0f;
        private bool _isBeingRemoved = false;  // 添加标记，防止重复执行
        private bool _missionEnding = false;
        private bool _isCleanedUp = false;  // 添加标记，确保只清理一次
        private readonly object _cleanupLock = new object();  // 线程锁
        private const float BATTLE_START_GRACE_PERIOD = 30f;  // 援兵消息30秒缓冲
        private float _battleStartTime = 0f;

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
            
            // 重用HashSet而不是创建新的
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

        private bool ShouldAllowRetreat(Team attackerTeam, int attackerCount)
        {
            try
            {
                if (_isDisabled || !SafetyChecks.IsMissionValid() || attackerTeam == null)
                    return true;

                #if DEBUG
                Logger.LogDebug("ShouldAllowRetreat", $"Checking retreat conditions - AttackerCount: {attackerCount}, " +
                    $"EnableFixedRetreat: {Settings.Instance.EnableFixedRetreat}, RetreatThreshold: {Settings.Instance.RetreatThreshold}, " +
                    $"EnableRatioRetreat: {Settings.Instance.EnableRatioRetreat}");
                #endif

                // 优先使用固定数量触发
                if (Settings.Instance.EnableFixedRetreat)
                {
                    bool shouldRetreat = attackerCount <= Settings.Instance.RetreatThreshold;
                    #if DEBUG
                    Logger.LogDebug("ShouldAllowRetreat", $"Fixed threshold check - AttackerCount: {attackerCount} <= Threshold: {Settings.Instance.RetreatThreshold} = {shouldRetreat}");
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
                    Logger.LogDebug("ShouldAllowRetreat", $"Ratio threshold check - AttackerCount: {attackerCount}, DefenderCount: {defenderCount}, " +
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
                    Logger.LogDebug("ShouldAllowRetreat", "Cancelling pending disable state");
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
                    Logger.LogDebug("StartDisableTimer", $"Starting disable timer with reason: {reason}");
                    #endif
                    
                    _pendingDisable = true;
                    _disableTimer = 0f;
                    _initialAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    
                    // 确保消息显示
                    if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= RETREAT_MESSAGE_COOLDOWN)
                    {
                        _lastRetreatMessageTime = Mission.Current.CurrentTime;
                        var message = new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled");
                        InformationManager.DisplayMessage(new InformationMessage(
                            message.ToString(),
                            Color.FromUint(0xFFFF00FF)));
                        
                        #if DEBUG
                        Logger.LogDebug("StartDisableTimer", "Retreat message displayed");
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
                
                if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= RETREAT_MESSAGE_COOLDOWN)
                {
                    _lastRetreatMessageTime = Mission.Current.CurrentTime;
                    var message = new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled");
                    InformationManager.DisplayMessage(new InformationMessage(
                        message.ToString(),
                        Color.FromUint(0xFFFF00FF)));
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
                    Logger.LogDebug("DisableMod", $"Disabling mod with reason: {reason}");
                    #endif
                    _isDisabled = true;
                    _pendingDisable = false;
                    _disableTimer = 0f;
                    
                    // 只在非结束状态下执行清理
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

        private void RestoreAllFormations()
        {
            if (_chargedFormations == null) return;
            
            try
            {
                var formationsToRestore = _chargedFormations.ToList(); // 创建副本避免集合修改异常
                foreach (Formation formation in formationsToRestore)
                {
                    try
                    {
                        if (SafetyChecks.IsValidFormation(formation))
                        {
                            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleError("formation restore", ex);
                        continue;
                    }
                }
                _chargedFormations.Clear();
            }
            catch (Exception ex)
            {
                HandleError("formations restore", ex);
            }
        }

        private void AdjustTeamMorale(Team team, float dt)
        {
            try
            {
                if (team == null || team != _attackerTeam) return;

                float currentTime = _currentMission.CurrentTime;
                
                // 限制更新频率
                if (currentTime - _lastMoraleUpdateTime < MORALE_UPDATE_INTERVAL)
                    return;

                _lastMoraleUpdateTime = currentTime;
                
                // 使用本地变量缓存设置值，避免多次访问
                float moraleThreshold = Settings.Instance.MoraleThreshold;
                float moraleBoostRate = Settings.Instance.MoraleBoostRate;
                
                // 计算当前攻城方人数
                int attackerCount = SafetyChecks.GetAttackerCount(team);
                
                #if DEBUG
                Logger.LogDebug("AdjustTeamMorale", $"Current attacker count: {attackerCount}");
                #endif

                // 检查是否应该允许撤退
                if (ShouldAllowRetreat(team, attackerCount))
                {
                    #if DEBUG
                    Logger.LogDebug("AdjustTeamMorale", "Retreat conditions met, skipping morale adjustment");
                    #endif
                    return;
                }

                // 计算战场优势比率
                float strengthRatio = 2.0f;
                if (_defenderTeam != null)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                    if (defenderCount > 0)
                    {
                        strengthRatio = (float)attackerCount / defenderCount;
                    }
                }

                // 根据优势情况调整士气阈值
                if (strengthRatio >= 1.5f)
                {
                    moraleThreshold *= 0.6f;
                }

                // 更新士兵士气
                int boostedCount = UpdateAgentsMorale(team, moraleThreshold, moraleBoostRate);
                
                // 显示鼓舞消息
                if (boostedCount >= 10 && currentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = currentTime;
                    var message = new TextObject("{=ibs_morale_boost}IronBlood Siege: {COUNT} troops were inspired")
                        .SetTextVariable("COUNT", boostedCount);
                    InformationManager.DisplayMessage(new InformationMessage(
                        message.ToString(),
                        Color.FromUint(0xFFFF00FF)));
                    
                    #if DEBUG
                    Logger.LogDebug("AdjustTeamMorale", $"Displayed morale boost message for {boostedCount} troops");
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

                // 检查是否应该禁用mod功能
                int attackerCount = SafetyChecks.GetAttackerCount(agent.Team);
                if (ShouldAllowRetreat(agent.Team, attackerCount))
                {
                    return; // 如果应该禁用，不干预士兵的逃跑行为
                }

                float targetMorale = Settings.Instance.MoraleThreshold + Settings.Instance.MoraleBoostRate;
                if (targetMorale > 100f) targetMorale = 100f;
                
                agent.SetMorale(targetMorale);
                agent.StopRetreating();
                
                if (agent.Formation != null)
                {
                    PreventRetreat(agent.Formation);
                }
                
                // 只在消息冷却时间过后显示消息
                if (Mission.Current.CurrentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = Mission.Current.CurrentTime;
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ibs_prevent_retreat}IronBlood Siege: Prevented troops from retreating!").ToString(),
                        Color.FromUint(0xFFFF00FF)));
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
                // 如果任务已经结束或mod已禁用，直接返回
                if (_missionEnding || _isDisabled)
                {
                    return;
                }

                if (!Settings.Instance.IsEnabled || !SafetyChecks.IsMissionValid())
                {
                    return;
                }

                // 更新缓存
                _currentMission = Mission.Current;
                if (_currentMission == null) return;

                // 检查战斗是否结束
                if (SafetyChecks.IsBattleEnded())
                {
                    // 只在第一次检测到时记录日志
                    if (!_missionEnding)
                    {
                        #if DEBUG
                        Logger.LogDebug("MissionTick", "Battle ended detected");
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

                // 每0.5秒才检查一次场景状态
                if (_currentMission.CurrentTime % 0.5f < dt)
                {
                    bool previousSceneState = _isSiegeScene;
                    _isSiegeScene = SafetyChecks.IsSiegeSceneValid();
                    
                    #if DEBUG
                    // 每10秒记录一次当前状态
                    if (_currentMission.CurrentTime % 10 < dt)
                    {
                        Logger.LogDebug("MissionTick", $"Scene state - IsSiege: {_isSiegeScene}, IsEnabled: {Settings.Instance.IsEnabled}, " +
                            $"EnableFixedRetreat: {Settings.Instance.EnableFixedRetreat}, RetreatThreshold: {Settings.Instance.RetreatThreshold}");
                        
                        if (_attackerTeam != null)
                        {
                            int currentAttackerCount = SafetyChecks.GetAttackerCount(_attackerTeam);
                            Logger.LogDebug("MissionTick", $"Current attacker count: {currentAttackerCount}");
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

                // 更新Team缓存
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
               //if (_disableTimer >= DISABLE_DELAY)
                if (_disableTimer >= Settings.Instance.DisableDelay)
                {
                    // 获取当前攻击方士兵数量
                    int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    
                    // 如果当前士兵数量大于初始数量，取消禁用计时
                    if (currentAttackerCount > _initialAttackerCount)
                    {
                        _pendingDisable = false;
                        _disableTimer = 0f;
                        
                        // 只在战斗开始一定时间后才显示援兵消息
                        if (Mission.Current.CurrentTime - _battleStartTime > BATTLE_START_GRACE_PERIOD)
                        {
                            // 援兵到达的消息显示
                            var message = new TextObject("{=ibs_reinforcement_message}IronBlood Siege: Reinforcements have arrived, iron will attack resumed!");
                            InformationManager.DisplayMessage(new InformationMessage(
                                message.ToString(),
                                InfoColor));
                        }
                        return;
                    }

                    // 再次检查条件，确认是否真的需要禁用
                    if (ShouldAllowRetreat(Mission.Current.AttackerTeam, currentAttackerCount))
                    {
                        DisableMod("Disable timer expired");
                    }
                    
                    // 无论如何都重置计时器状态
                    _pendingDisable = false;
                    _disableTimer = 0f;
                }
                else
                {
                    // 在计时过程中也检查士兵数量变化
                    int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    if (currentAttackerCount > _initialAttackerCount)
                    {
                        _pendingDisable = false;
                        _disableTimer = 0f;
                        
                        // 只在战斗开始一定时间后才显示援兵消息
                        if (Mission.Current.CurrentTime - _battleStartTime > BATTLE_START_GRACE_PERIOD)
                        {
                            // 添加援兵到达的消息显示
                            var message = new TextObject("{=ibs_reinforcement_message}IronBlood Siege: Reinforcements have arrived, iron will attack resumed!");
                            InformationManager.DisplayMessage(new InformationMessage(
                                message.ToString(),
                                InfoColor));
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
                    Logger.LogDebug("OnClearScene", "Scene clearing started");
                    #endif
                    _missionEnding = true;
                    DisableMod("Scene clearing");
                }
                base.OnClearScene();
            }
            catch
            {
                #if DEBUG
                Logger.LogDebug("OnClearScene", "Error during scene clearing");
                #endif
            }
        }

        private void HandleError(string context, Exception ex)
        {
            try
            {
                #if DEBUG
                // 记录到日志文件
                Logger.LogError(context, ex);
                #endif
                
                // 显示在游戏中
                ErrorMessage
                    .SetTextVariable("CONTEXT", context)
                    .SetTextVariable("MESSAGE", ex.Message);
                    
                InformationManager.DisplayMessage(new InformationMessage(
                    ErrorMessage.ToString(),
                    ErrorColor));
            }
            catch
            {
                #if DEBUG
                // 确保错误处理本身不会崩溃
                try
                {
                    Logger.LogError("error handler", new Exception("Failed to handle error: " + context));
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
                Logger.LogDebug("OnEndMission", "Starting mission end cleanup");
                #endif

                // 立即标记状态，防止其他方法继续执行
                _missionEnding = true;
                _isDisabled = true;

                // 安全地清理Formation
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
                                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
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
                        _chargedFormations = null;  // 立即释放引用
                    }
                }

                // 重置所有状态
                _pendingDisable = false;
                _disableTimer = 0f;
                _isSiegeScene = false;
                _initialAttackerCount = 0;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _lastRetreatMessageTime = 0f;

                // 清理Team引用
                _attackerTeam = null;
                _defenderTeam = null;
                _currentMission = null;
                _currentSceneName = null;

                #if DEBUG
                Logger.LogDebug("OnEndMission", "Cleanup completed, calling base OnEndMission");
                #endif

                base.OnEndMission();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("OnEndMission", ex);
                #endif
                
                // 确保基类方法被调用
                base.OnEndMission();
            }
            finally
            {
                // 确保状态被正确设置
                _isCleanedUp = true;
                _isBeingRemoved = false;
                
                #if DEBUG
                Logger.LogDebug("OnEndMission", "Mission end cleanup finished");
                #endif
            }
        }

        public override void OnEndMissionInternal()
        {
            try
            {
                #if DEBUG
                Logger.LogDebug("OnEndMissionInternal", "Starting internal mission end cleanup");
                #endif

                // 确保资源被完全清理
                if (!_isCleanedUp)
                {
                    lock (_cleanupLock)
                    {
                        if (!_isCleanedUp)
                        {
                            // 清理所有状态和引用
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
                Logger.LogDebug("OnEndMissionInternal", "Calling base OnEndMissionInternal");
                #endif

                base.OnEndMissionInternal();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("OnEndMissionInternal", ex);
                #endif
                
                base.OnEndMissionInternal();
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_isBeingRemoved)
            {
                #if DEBUG
                Logger.LogDebug("OnRemoveBehavior", "Behavior already being removed, skipping");
                #endif
                return;
            }

            try
            {
                #if DEBUG
                Logger.LogDebug("OnRemoveBehavior", "Starting behavior removal");
                #endif

                _isBeingRemoved = true;
                
                // 确保所有资源都被清理
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
                Logger.LogError("OnRemoveBehavior", ex);
                #endif
                
                base.OnRemoveBehavior();
            }
            finally
            {
                _isBeingRemoved = false;
                
                #if DEBUG
                Logger.LogDebug("OnRemoveBehavior", "Behavior removal completed");
                #endif
            }
        }
    }
} 