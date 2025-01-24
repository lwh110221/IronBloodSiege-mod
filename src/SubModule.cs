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
        private bool _isSiegeScene = false;
        private bool _isDisabled = false;
        private bool _pendingDisable = false;    // 用于标记是否处于等待禁用状态
        private float _disableTimer = 0f;        // 禁用计时器
        private const float DISABLE_DELAY = 10f; // 禁用延迟时间（10秒）
        private int _initialAttackerCount = 0;   // 开始计时时的攻击方士兵数量
        private HashSet<Formation> _chargedFormations = new HashSet<Formation>();
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;
        private float _lastMessageTime = 0f;
        private const float MESSAGE_COOLDOWN = 10f;
        private const float RETREAT_MESSAGE_COOLDOWN = 25f;  // 不再铁血消息的冷却时间
        private float _lastRetreatMessageTime = 0f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
                // 重置状态
                _isSiegeScene = false;
                _isDisabled = false;
                _pendingDisable = false;
                _disableTimer = 0f;
                _chargedFormations = new HashSet<Formation>();
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _lastRetreatMessageTime = 0f;
                
                // 检查场景
                _isSiegeScene = CheckIfSiegeScene();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_error_init}IronBlood Siege initialization error: {MESSAGE}")
                        .SetTextVariable("MESSAGE", ex.Message)
                        .ToString(),
                    Color.FromUint(0xFF0000FF)));
            }
        }

        private bool CheckIfSiegeScene()
        {
            if (Mission.Current == null) 
                return false;
            
            return Mission.Current.Mode == MissionMode.Battle && 
                   Mission.Current.SceneName != null &&
                   (Mission.Current.SceneName.ToLower().Contains("siege") || 
                    Mission.Current.SceneName.ToLower().Contains("castle")) &&
                   Mission.Current.DefenderTeam != null &&
                   Mission.Current.AttackerTeam != null;
        }

        private void PreventRetreat(Formation formation)
        {
            if (formation != null && !_chargedFormations.Contains(formation))
            {
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                _chargedFormations.Add(formation);
            }
        }

        private bool ShouldAllowRetreat(Team attackerTeam, int attackerCount)
        {
            if (_isDisabled || attackerTeam == null || Mission.Current?.DefenderTeam == null)
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
                int defenderCount = Mission.Current.DefenderTeam.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive());
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

        private void StartDisableTimer(string reason)
        {
            if (!_pendingDisable)
            {
                _pendingDisable = true;
                _disableTimer = 0f;
                // 记录开始计时时的攻击方士兵数量
                _initialAttackerCount = Mission.Current?.AttackerTeam?.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive()) ?? 0;
                ShowRetreatMessage(reason);
            }
        }

        private void ShowRetreatMessage(string reason)
        {
            if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= RETREAT_MESSAGE_COOLDOWN)
            {
                _lastRetreatMessageTime = Mission.Current.CurrentTime;
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled").ToString(),
                    Color.FromUint(0xFFFF00FF)));
            }
        }

        private void DisableMod(string reason)
        {
            if (!_isDisabled)
            {
                _isDisabled = true;
                _pendingDisable = false;
                _disableTimer = 0f;
                RestoreAllFormations();
                ShowRetreatMessage(reason);
            }
        }

        private void RestoreAllFormations()
        {
            if (_chargedFormations != null)
            {
                foreach (Formation formation in _chargedFormations.ToList())
                {
                    try
                    {
                        if (formation?.Team == Mission.Current?.AttackerTeam)
                        {
                            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                _chargedFormations.Clear();
            }
        }

        private void AdjustTeamMorale(Team team, float dt)
        {
            if (team == null || team != Mission.Current.AttackerTeam)
                return;

            // 限制更新频率
            if (Mission.Current.CurrentTime - _lastMoraleUpdateTime < MORALE_UPDATE_INTERVAL)
                return;

            _lastMoraleUpdateTime = Mission.Current.CurrentTime;
            
            // 计算当前攻城方人数
            int attackerCount = team.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive());
            
            // 检查是否应该禁用mod功能
            if (ShouldAllowRetreat(team, attackerCount))
            {
                // 如果应该禁用，显示消息并临时禁用mod
                if (Mission.Current.CurrentTime - _lastRetreatMessageTime >= RETREAT_MESSAGE_COOLDOWN)
                {
                    _lastRetreatMessageTime = Mission.Current.CurrentTime;
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled").ToString(),
                        Color.FromUint(0xFFFF00FF)));
                }
                
                // 恢复所有Formation的状态
                if (_chargedFormations != null)
                {
                    foreach (Formation formation in _chargedFormations.ToList())
                    {
                        if (formation?.Team == Mission.Current?.AttackerTeam)
                        {
                            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                        }
                    }
                    _chargedFormations.Clear();
                }
                
                return;
            }
            
            int defenderCount = Mission.Current.DefenderTeam?.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive()) ?? 0;
            
            // 计算战场优势
            float strengthRatio = defenderCount > 0 ? (float)attackerCount / defenderCount : 2.0f;
            
            // 根据战场优势调整士气阈值
            float moraleThreshold = Settings.Instance.MoraleThreshold;
            if (strengthRatio >= 1.5f)
            {
                moraleThreshold *= 0.6f;
            }
            
            // 只检查士气特别低的或正在逃跑的士兵
            var agentsToUpdate = team.ActiveAgents.Where(agent => 
                agent?.IsHuman == true && 
                !agent.IsPlayerControlled && 
                agent.IsActive() && 
                (agent.GetMorale() < moraleThreshold || agent.IsRetreating()));

            int boostedCount = 0;
            foreach (Agent agent in agentsToUpdate)
            {
                float oldMorale = agent.GetMorale();
                
                // 直接使用设置的阈值，不再额外计算
                if (oldMorale < moraleThreshold || agent.IsRetreating())
                {
                    // 从用户设置中获取士气提升速率
                    float targetMorale = oldMorale + Settings.Instance.MoraleBoostRate;
                    
                    // 正在逃跑的士兵给予额外的士气提升
                    if (agent.IsRetreating())
                    {
                        targetMorale += Settings.Instance.MoraleBoostRate * 0.5f;
                    }
                    
                    // 设置上限为100
                    if (targetMorale > 100f) 
                    {
                        targetMorale = 100f;
                    }
                    
                    agent.SetMorale(targetMorale);

                    // 只在实际提升士气时计数
                    if (targetMorale > oldMorale)
                    {
                        boostedCount++;
                    }
                }

                if (agent.IsRetreating())
                {
                    agent.StopRetreating();
                    if (agent.Formation != null && !_chargedFormations.Contains(agent.Formation))
                    {
                        PreventRetreat(agent.Formation);
                    }
                }
            }

            // 只在确实有士兵被鼓舞且数量达到阈值时才显示消息
            if (boostedCount >= 10 && Mission.Current.CurrentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
            {
                _lastMessageTime = Mission.Current.CurrentTime;
                var textObject = new TextObject("{=ibs_morale_boost}IronBlood Siege: {COUNT} troops were inspired");
                textObject.SetTextVariable("COUNT", boostedCount);
                InformationManager.DisplayMessage(new InformationMessage(
                    textObject.ToString(),
                    Color.FromUint(0xFFFF00FF)));
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(Settings.Instance.MoraleThreshold); 
            }
        }

        public override void OnAgentFleeing(Agent agent)
        {
            base.OnAgentFleeing(agent);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                // 检查是否应该禁用mod功能
                int attackerCount = agent.Team.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive());
                if (ShouldAllowRetreat(agent.Team, attackerCount))
                {
                    // 如果应该禁用，不干预士兵的逃跑行为
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
                
                // 只在消息冷却时间过后显示消息
                if (Mission.Current.CurrentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
                {
                    _lastMessageTime = Mission.Current.CurrentTime;
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ibs_prevent_retreat}IronBlood Siege: Prevented troops from retreating!").ToString(),
                        Color.FromUint(0xFFFF00FF)));
                }
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                if (!Settings.Instance.IsEnabled || Mission.Current == null)
                {
                    return;
                }

                // 检查Mission是否已结束
                if (Mission.Current.MissionEnded || Mission.Current.CheckIfBattleInRetreat())
                {
                    DisableMod("Mission ended");
                    OnRemoveBehavior();
                    return;
                }

                // 处理延迟禁用
                if (_pendingDisable)
                {
                    _disableTimer += dt;
                    if (_disableTimer >= DISABLE_DELAY)
                    {
                        // 获取当前攻击方士兵数量
                        int currentAttackerCount = Mission.Current.AttackerTeam?.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive()) ?? 0;
                        
                        // 如果当前士兵数量大于初始数量，取消禁用计时
                        if (currentAttackerCount > _initialAttackerCount)
                        {
                            _pendingDisable = false;
                            _disableTimer = 0f;
                            return;
                        }
                        
                        // 再次检查条件，确认是否真的需要禁用
                        if (ShouldAllowRetreat(Mission.Current.AttackerTeam, currentAttackerCount))
                        {
                            DisableMod("Delay threshold reached");
                        }
                        
                        // 无论如何都重置计时器状态
                        _pendingDisable = false;
                        _disableTimer = 0f;
                    }
                    else
                    {
                        // 在计时过程中也检查士兵数量变化
                        int currentAttackerCount = Mission.Current.AttackerTeam?.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive()) ?? 0;
                        if (currentAttackerCount > _initialAttackerCount)
                        {
                            _pendingDisable = false;
                            _disableTimer = 0f;
                            return;
                        }
                    }
                }

                if (_isDisabled)
                {
                    return;
                }

                _isSiegeScene = CheckIfSiegeScene();
                if (!_isSiegeScene)
                    return;

                Team attackerTeam = Mission.Current.AttackerTeam;
                if (attackerTeam == null)
                    return;

                AdjustTeamMorale(attackerTeam, dt);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_error_general}IronBlood Siege error: {MESSAGE}")
                        .SetTextVariable("MESSAGE", ex.Message)
                        .ToString(),
                    Color.FromUint(0xFF0000FF)));
                DisableMod("Error occurred");
            }
        }

        public override void OnRemoveBehavior()
        {
            try
            {
                _isDisabled = true;
                _pendingDisable = false;
                RestoreAllFormations();

                // 确保所有Formation都被正确处理
                if (Mission.Current?.AttackerTeam != null)
                {
                    foreach (Formation formation in Mission.Current.AttackerTeam.Formations)
                    {
                        try
                        {
                            if (formation != null)
                            {
                                formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ibs_error_cleanup}IronBlood Siege cleanup error: {MESSAGE}")
                        .SetTextVariable("MESSAGE", ex.Message)
                        .ToString(),
                    Color.FromUint(0xFF0000FF)));
            }
            finally
            {
                // 确保在任何情况下都清理资源
                _isSiegeScene = false;
                _isDisabled = true;
                _pendingDisable = false;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _lastRetreatMessageTime = 0f;
                _disableTimer = 0f;
                
                if (_chargedFormations != null)
                {
                    _chargedFormations.Clear();
                    _chargedFormations = null;
                }
                
                base.OnRemoveBehavior();
            }
        }
    }
} 