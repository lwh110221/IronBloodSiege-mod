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
                    mission.AddMissionBehavior(new SiegeMoraleBehavior());
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ibs_mod_enabled}IronBlood Siege is enabled").ToString(), 
                        Color.FromUint(0x00FF00FF)));
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
        private const float DISABLE_DELAY = 15f; // 禁用延迟时间（15秒）
        private int _initialAttackerCount = 0;   // 开始计时时的攻击方士兵数量
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;
        private float _lastMessageTime = 0f;
        private const float MESSAGE_COOLDOWN = 10f;
        private const float RETREAT_MESSAGE_COOLDOWN = 25f;  // 不再铁血消息的冷却时间
        private float _lastRetreatMessageTime = 0f;
        private bool _isBeingRemoved = false;  // 添加标记，防止重复执行
        private bool _missionEnding = false;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
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

                // 优先使用固定数量触发
                if (Settings.Instance.EnableFixedRetreat)
                {
                    if (attackerCount <= Settings.Instance.RetreatThreshold)
                    {
                        StartDisableTimer("Fixed threshold reached");
                        return true;
                    }
                }
                // 如果没有启用固定数量，才检查比例触发
                else if (Settings.Instance.EnableRatioRetreat)
                {
                    int defenderCount = SafetyChecks.GetAttackerCount(Mission.Current.DefenderTeam);
                    if (defenderCount > 0 && attackerCount < defenderCount * 0.7f)
                    {
                        StartDisableTimer("Ratio threshold reached");
                        return true;
                    }
                }

                // 如果条件不满足，取消待禁用状态
                _pendingDisable = false;
                _disableTimer = 0f;
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
                    _pendingDisable = true;
                    _disableTimer = 0f;
                    _initialAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    ShowRetreatMessage(reason);
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
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled").ToString(),
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
                
                if (ShouldAllowRetreat(team, attackerCount))
                {
                    if (currentTime - _lastRetreatMessageTime >= RETREAT_MESSAGE_COOLDOWN)
                    {
                        _lastRetreatMessageTime = currentTime;
                        ShowRetreatMessage("Low attacker count");
                    }
                    RestoreAllFormations();
                    return;
                }
                
                // 使用预先计算的值
                int defenderCount = SafetyChecks.GetAttackerCount(_defenderTeam);
                float strengthRatio = defenderCount > 0 ? (float)attackerCount / defenderCount : 2.0f;
                
                if (strengthRatio >= 1.5f)
                {
                    moraleThreshold *= 0.6f;
                }

                // 使用ToList()创建快照，避免在迭代时修改集合
                var agentsToUpdate = team.ActiveAgents
                    .Where(agent => SafetyChecks.IsValidAgent(agent) && 
                                   (agent.GetMorale() < moraleThreshold || agent.IsRetreating()))
                    .ToList();

                int boostedCount = ProcessAgentMorale(agentsToUpdate, moraleThreshold, moraleBoostRate);

                // 显示消息
                if (boostedCount >= 10 && currentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = currentTime;
                    MoraleBoostMessage.SetTextVariable("COUNT", boostedCount);
                    InformationManager.DisplayMessage(new InformationMessage(
                        MoraleBoostMessage.ToString(),
                        WarningColor));
                }
            }
            catch (Exception ex)
            {
                HandleError("team morale adjustment", ex);
            }
        }

        private int ProcessAgentMorale(List<Agent> agents, float moraleThreshold, float moraleBoostRate)
        {
            int boostedCount = 0;
            foreach (Agent agent in agents)
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

                // 更新场景状态
                bool previousSceneState = _isSiegeScene;
                _isSiegeScene = SafetyChecks.IsSiegeSceneValid();
                
                if (previousSceneState && !_isSiegeScene)
                {
                    DisableMod("Siege scene ended");
                    return;
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
                if (_disableTimer >= DISABLE_DELAY)  // 使用DISABLE_DELAY常量
                {
                    // 获取当前攻击方士兵数量
                    int currentAttackerCount = SafetyChecks.GetAttackerCount(Mission.Current.AttackerTeam);
                    
                    // 如果当前士兵数量大于初始数量，取消禁用计时
                    if (currentAttackerCount > _initialAttackerCount)
                    {
                        _pendingDisable = false;
                        _disableTimer = 0f;
                        
                        // 援兵到达的消息显示
                        GameTexts.SetVariable("MESSAGE", new TextObject("{=ibs_reinforcement_message}IronBlood Siege: Reinforcements have arrived, iron will attack resumed!"));
                        InformationManager.DisplayMessage(new InformationMessage(
                            GameTexts.GetVariable("MESSAGE").ToString(),
                            InfoColor));
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
                        
                        // 添加援兵到达的消息显示
                        GameTexts.SetVariable("MESSAGE", new TextObject("{=ibs_reinforcement_message}IronBlood Siege: Reinforcements have arrived, iron will attack resumed!"));
                        InformationManager.DisplayMessage(new InformationMessage(
                            GameTexts.GetVariable("MESSAGE").ToString(),
                            InfoColor));
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("process disable timer", ex);
                _pendingDisable = false;
                _disableTimer = 0f;
            }
        }

        public override void OnEndMissionInternal()
        {
            try 
            {
                if (!_missionEnding)
                {
                    #if DEBUG
                    Logger.LogDebug("OnEndMissionInternal", "Mission ending started");
                    #endif
                    _missionEnding = true;
                    DisableMod("Mission ending");
                }
                base.OnEndMissionInternal();
            }
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("OnEndMissionInternal", ex);
                #endif
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
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("OnClearScene", ex);
                #endif
            }
        }

        public override void OnRemoveBehavior()
        {
            try
            {
                if (_isBeingRemoved)
                {
                    #if DEBUG
                    Logger.LogDebug("OnRemoveBehavior", "Skipped duplicate removal");
                    #endif
                    return;
                }

                _isBeingRemoved = true;
                _missionEnding = true;
                _isDisabled = true;
                
                #if DEBUG
                Logger.LogDebug("OnRemoveBehavior", "Starting cleanup");
                #endif
                
                try
                {
                    if (_chargedFormations != null)
                    {
                        _chargedFormations.Clear();
                        _chargedFormations = null;
                    }

                    // Reset all state variables
                    _isSiegeScene = false;
                    _lastMoraleUpdateTime = 0f;
                    _lastMessageTime = 0f;
                    _lastRetreatMessageTime = 0f;
                    _initialAttackerCount = 0;
                    _pendingDisable = false;
                    _disableTimer = 0f;
                    
                    // Clear cached references
                    _currentMission = null;
                    _attackerTeam = null;
                    _defenderTeam = null;
                    _currentSceneName = null;

                    #if DEBUG
                    Logger.LogDebug("OnRemoveBehavior", "Cleanup completed");
                    #endif
                }
                catch (Exception ex)
                {
                    #if DEBUG
                    Logger.LogError("OnRemoveBehavior cleanup", ex);
                    #endif
                }
                finally
                {
                    try
                    {
                        base.OnRemoveBehavior();
                    }
                    catch (Exception ex)
                    {
                        #if DEBUG
                        Logger.LogError("base OnRemoveBehavior", ex);
                        #endif
                    }
                    _isBeingRemoved = false;
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Logger.LogError("OnRemoveBehavior outer", ex);
                #endif
                _isBeingRemoved = false;
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
    }
} 