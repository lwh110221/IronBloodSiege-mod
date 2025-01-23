using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Engine;
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
            InformationManager.DisplayMessage(new InformationMessage("铁血攻城已加载", Color.FromUint(0x00FF00FF)));
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            try
            {
                if (mission != null)
                {
                    mission.AddMissionBehavior(new SiegeMoraleBehavior());
                    InformationManager.DisplayMessage(new InformationMessage("铁血攻城已启用", Color.FromUint(0x00FF00FF)));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(string.Format("铁血攻城行为添加错误: {0}", ex.Message)));
            }
        }
    }

    public class SiegeMoraleBehavior : MissionBehavior
    {
        private bool _isSiegeScene = false;
        private float _lastMessageTime = 0f;
        private Dictionary<Agent, float> _agentMoraleHistory = new Dictionary<Agent, float>();
        private int _initialAttackerCount = 0;
        private float _lastCasualtyCheckTime = 0f;
        private int _lastAttackerCount = 0;
        private float _recentCasualtyRate = 0f;
        private const float CASUALTY_CHECK_INTERVAL = 3f;
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;
        private HashSet<Formation> _chargedFormations = new HashSet<Formation>();
        private float _lastPerformanceCheckTime = 0f;
        private const float PERFORMANCE_CHECK_INTERVAL = 1f;
        private int _performanceUpdateCount = 0;
        private float _lastUpdateDuration = 0f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            
            // 重置状态
            _isSiegeScene = false;
            _lastMessageTime = 0f;
            _agentMoraleHistory.Clear();
            _initialAttackerCount = 0;
            _lastCasualtyCheckTime = 0f;
            _lastAttackerCount = 0;
            _recentCasualtyRate = 0f;
            _lastMoraleUpdateTime = 0f;
            _chargedFormations.Clear();
            
            // 检查场景并初始化
            _isSiegeScene = CheckIfSiegeScene();
            if (_isSiegeScene)
            {
                InformationManager.DisplayMessage(new InformationMessage("检测到攻城战场景", Color.FromUint(0x00FF00FF)));
                if (Mission.Current?.AttackerTeam != null)
                {
                    _initialAttackerCount = Mission.Current.AttackerTeam.ActiveAgents.Count;
                    _lastAttackerCount = _initialAttackerCount;
                }
            }
        }

        private bool CheckIfSiegeScene()
        {
            if (Mission.Current == null) 
                return false;
            
            bool isSiege = Mission.Current.Mode == MissionMode.Battle && 
                          Mission.Current.SceneName != null &&
                          (Mission.Current.SceneName.ToLower().Contains("siege") || 
                           Mission.Current.SceneName.ToLower().Contains("castle")) &&
                          Mission.Current.DefenderTeam != null &&
                          Mission.Current.AttackerTeam != null;

            return isSiege;
        }

        private void UpdateCasualtyRate()
        {
            if (Mission.Current?.CurrentTime - _lastCasualtyCheckTime >= CASUALTY_CHECK_INTERVAL)
            {
                int currentCount = Mission.Current.AttackerTeam.ActiveAgents.Count;
                _recentCasualtyRate = (_lastAttackerCount - currentCount) / (float)_lastAttackerCount;
                _lastAttackerCount = currentCount;
                _lastCasualtyCheckTime = Mission.Current.CurrentTime;
            }
        }

        private void PreventRetreat(Formation formation)
        {
            if (formation != null && !_chargedFormations.Contains(formation))
            {
                // 设置进攻命令
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                _chargedFormations.Add(formation);
            }
        }

        private float CalculateTeamMorale(Team team)
        {
            if (team?.ActiveAgents == null || !team.ActiveAgents.Any())
                return 0f;

            float totalMorale = 0f;
            int agentCount = 0;
            float casualtyRatio = 0f;

            // 计算总伤亡比例
            if (_initialAttackerCount > 0)
            {
                casualtyRatio = 1f - (float)team.ActiveAgents.Count / _initialAttackerCount;
            }

            // 考虑最近的伤亡率
            float moralePenalty = casualtyRatio * 20f + _recentCasualtyRate * 40f;

            foreach (Agent agent in team.ActiveAgents)
            {
                if (agent?.IsHuman == true && !agent.IsPlayerControlled && agent.IsActive())
                {
                    float baseMorale = agent.GetMorale();
                    
                    // 根据伤亡情况调整士气
                    float adjustedMorale = baseMorale - moralePenalty;
                    
                    // 确保士气不会低于最小值
                    adjustedMorale = Math.Max(adjustedMorale, 20f);
                    
                    totalMorale += adjustedMorale;
                    agentCount++;

                    // 更新士气历史
                    _agentMoraleHistory[agent] = adjustedMorale;
                }
            }

            float averageMorale = agentCount > 0 ? totalMorale / agentCount : 0f;
            return averageMorale;
        }

        private void AdjustTeamMorale(Team team, float averageMorale, float dt)
        {
            if (team == null || team != Mission.Current.AttackerTeam)
                return;

            // 限制更新频率
            if (Mission.Current.CurrentTime - _lastMoraleUpdateTime < MORALE_UPDATE_INTERVAL)
                return;

            _lastMoraleUpdateTime = Mission.Current.CurrentTime;
            
            // 只在士气低于90或正在逃跑时更新
            var agentsToUpdate = team.ActiveAgents.Where(agent => 
                agent?.IsHuman == true && 
                !agent.IsPlayerControlled && 
                agent.IsActive() && 
                (agent.GetMorale() < 90f || agent.IsRetreating()));

            foreach (Agent agent in agentsToUpdate)
            {
                agent.SetMorale(100f);
                _agentMoraleHistory[agent] = 100f;

                if (agent.IsRetreating())
                {
                    agent.StopRetreating();
                    
                    // 只在士兵实际逃跑时设置Formation
                    if (agent.Formation != null && !_chargedFormations.Contains(agent.Formation))
                    {
                        PreventRetreat(agent.Formation);
                    }
                }
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(100f);
                _agentMoraleHistory[agent] = 100f;
            }
        }

        public override void OnAgentFleeing(Agent agent)
        {
            base.OnAgentFleeing(agent);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                // 无条件阻止逃跑，不再检查士气值
                agent.SetMorale(100f);
                _agentMoraleHistory[agent] = 100f;
                agent.StopRetreating();
                
                // 强制设置进攻状态
                if (agent.Formation != null)
                {
                    PreventRetreat(agent.Formation);
                }
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "已鼓舞攻城士兵",
                    Color.FromUint(0x00FF00FF)));
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                if (!Settings.Instance.IsEnabled || Mission.Current == null)
                    return;

                _isSiegeScene = CheckIfSiegeScene();
                if (!_isSiegeScene)
                    return;

                Team attackerTeam = Mission.Current.AttackerTeam;
                if (attackerTeam == null)
                    return;

                float startTime = Mission.Current.CurrentTime;

                // 更新伤亡率
                UpdateCasualtyRate();

                float attackerMorale = CalculateTeamMorale(attackerTeam);
                AdjustTeamMorale(attackerTeam, attackerMorale, dt);

                // 计算更新耗时
                _lastUpdateDuration = Mission.Current.CurrentTime - startTime;
                _performanceUpdateCount++;

                // 性能监控
                if (Mission.Current.CurrentTime - _lastPerformanceCheckTime >= PERFORMANCE_CHECK_INTERVAL)
                {
                    if (_lastUpdateDuration > 0.1f) // 如果单次更新耗时超过100ms
                    {
                        string perfMessage = string.Format(
                            "性能警告 - 更新耗时: {0:F3}秒, 存活士兵: {1}, 伤亡率: {2:P1}", 
                            _lastUpdateDuration,
                            attackerTeam.ActiveAgents.Count,
                            1f - ((float)attackerTeam.ActiveAgents.Count / _initialAttackerCount));
                        
                        InformationManager.DisplayMessage(new InformationMessage(
                            perfMessage,
                            Color.FromUint(0xFF0000FF)));
                    }
                    
                    _lastPerformanceCheckTime = Mission.Current.CurrentTime;
                    _performanceUpdateCount = 0;
                }

                float currentTime = Mission.Current.CurrentTime;
                if (currentTime - _lastMessageTime >= 5f)
                {
                    _lastMessageTime = currentTime;
                    string message = string.Format(
                        "攻城方: 士气 {0:F1}, 存活 {1}", 
                        attackerMorale,
                        attackerTeam.ActiveAgents.Count,
                        _initialAttackerCount);
                        
                    if (_recentCasualtyRate > 0.01f)
                    {
                        message += string.Format(", 近期伤亡率 {0:P1}", _recentCasualtyRate);
                    }
                    InformationManager.DisplayMessage(new InformationMessage(message, Color.FromUint(0x00FF00FF)));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("铁血攻城错误: {0}", ex.Message),
                    Color.FromUint(0x00FF00FF)));
            }
        }

        public override void OnDeploymentFinished()
        {
            base.OnDeploymentFinished();
            if (_isSiegeScene && Mission.Current?.AttackerTeam != null)
            {
                _initialAttackerCount = Mission.Current.AttackerTeam.ActiveAgents.Count;
                _lastAttackerCount = _initialAttackerCount;
            }
        }
    }
} 