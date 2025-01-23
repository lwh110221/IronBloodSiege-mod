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
        private Dictionary<Agent, float> _agentMoraleHistory = new Dictionary<Agent, float>();
        private HashSet<Formation> _chargedFormations = new HashSet<Formation>();
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            
            // 重置状态
            _isSiegeScene = false;
            _agentMoraleHistory.Clear();
            _chargedFormations.Clear();
            
            // 检查场景
            _isSiegeScene = CheckIfSiegeScene();
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

        private void AdjustTeamMorale(Team team, float dt)
        {
            if (team == null || team != Mission.Current.AttackerTeam)
                return;

            // 限制更新频率
            if (Mission.Current.CurrentTime - _lastMoraleUpdateTime < MORALE_UPDATE_INTERVAL)
                return;

            _lastMoraleUpdateTime = Mission.Current.CurrentTime;
            
            // 只检查士气低的或正在逃跑的士兵
            var agentsToUpdate = team.ActiveAgents.Where(agent => 
                agent?.IsHuman == true && 
                !agent.IsPlayerControlled && 
                agent.IsActive() && 
                (agent.GetMorale() < 80f || agent.IsRetreating()));

            bool hasMoraleBoosted = false;
            foreach (Agent agent in agentsToUpdate)
            {
                float oldMorale = agent.GetMorale();
                agent.SetMorale(100f);

                // 如果士气显著提升，标记为已鼓舞
                if (oldMorale < 50f)
                {
                    hasMoraleBoosted = true;
                }

                if (agent.IsRetreating())
                {
                    agent.StopRetreating();
                    hasMoraleBoosted = true;
                    
                    if (agent.Formation != null && !_chargedFormations.Contains(agent.Formation))
                    {
                        PreventRetreat(agent.Formation);
                    }
                }
            }

            // 如果有士兵被鼓舞，显示消息
            if (hasMoraleBoosted)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "铁血攻城：已鼓舞士兵",
                    Color.FromUint(0xFFFF00FF)));
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(90f);
            }
        }

        public override void OnAgentFleeing(Agent agent)
        {
            base.OnAgentFleeing(agent);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(100f);
                agent.StopRetreating();
                
                if (agent.Formation != null)
                {
                    PreventRetreat(agent.Formation);
                }
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "铁血攻城：已阻止士兵撤退！",
                    Color.FromUint(0xFFFF00FF)));
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

                AdjustTeamMorale(attackerTeam, dt);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("铁血攻城错误: {0}", ex.Message),
                    Color.FromUint(0xFF0000FF)));
            }
        }
    }
} 