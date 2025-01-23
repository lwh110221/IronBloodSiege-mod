// 铁血攻城
// 作者：Ahao
// 版本：1.0.0
// email ：2285813721@qq.com
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
                InformationManager.DisplayMessage(new InformationMessage("铁血攻城 -加载成功！", Color.FromUint(0x0000FFFF)));
                InformationManager.DisplayMessage(new InformationMessage("作者 ：Ahao", Color.FromUint(0x0000FFFF)));
                InformationManager.DisplayMessage(new InformationMessage("email ：2285813721@qq.com", Color.FromUint(0x0000FFFF)));
            }
            catch (Exception ex)
            {
                // 忽略显示错误
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
        private HashSet<Formation> _chargedFormations = new HashSet<Formation>();
        private const float MORALE_UPDATE_INTERVAL = 0.5f;
        private float _lastMoraleUpdateTime = 0f;
        private float _lastMessageTime = 0f;
        private const float MESSAGE_COOLDOWN = 10f; // 消息冷却时间10秒

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
                // 重置状态
                _isSiegeScene = false;
                _chargedFormations = new HashSet<Formation>();
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                
                // 检查场景
                _isSiegeScene = CheckIfSiegeScene();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("铁血攻城初始化错误: {0}", ex.Message),
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

        private void AdjustTeamMorale(Team team, float dt)
        {
            if (team == null || team != Mission.Current.AttackerTeam)
                return;

            // 限制更新频率
            if (Mission.Current.CurrentTime - _lastMoraleUpdateTime < MORALE_UPDATE_INTERVAL)
                return;

            _lastMoraleUpdateTime = Mission.Current.CurrentTime;
            
            // 计算攻守双方的有效战斗人数
            int attackerCount = team.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive());
            int defenderCount = Mission.Current.DefenderTeam?.ActiveAgents.Count(a => a?.IsHuman == true && a.IsActive()) ?? 0;
            
            // 计算战场优势
            float strengthRatio = defenderCount > 0 ? (float)attackerCount / defenderCount : 2.0f;
            
            // 根据战场优势调整士气阈值
            float moraleThreshold = strengthRatio >= 1.5f ? 30f : 50f; // 降低阈值
            
            // 只检查士气特别低的或正在逃跑的士兵
            var agentsToUpdate = team.ActiveAgents.Where(agent => 
                agent?.IsHuman == true && 
                !agent.IsPlayerControlled && 
                agent.IsActive() && 
                (agent.GetMorale() < moraleThreshold || agent.IsRetreating()));

            bool hasMoraleBoosted = false;
            int boostedCount = 0;
            foreach (Agent agent in agentsToUpdate)
            {
                float oldMorale = agent.GetMorale();
                
                // 根据战场优势设置不同的士气值
                float targetMorale = strengthRatio >= 1.5f ? 70f : 90f; // 降低目标士气
                agent.SetMorale(targetMorale);

                // 只在士气显著提升时才计数
                if (oldMorale < moraleThreshold * 0.7f) // 更严格的条件
                {
                    boostedCount++;
                    hasMoraleBoosted = true;
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

            // 如果有士兵被鼓舞且消息冷却时间已过，显示消息
            if (hasMoraleBoosted && boostedCount >= 5 && // 至少5个士兵被鼓舞
                Mission.Current.CurrentTime - _lastMessageTime >= MESSAGE_COOLDOWN)
            {
                _lastMessageTime = Mission.Current.CurrentTime;
                InformationManager.DisplayMessage(new InformationMessage(
                    string.Format("铁血攻城：已鼓舞{0}名士兵", boostedCount),
                    Color.FromUint(0xFFFF00FF)));
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(85f); // 提高初始士气
            }
        }

        public override void OnAgentFleeing(Agent agent)
        {
            base.OnAgentFleeing(agent);
            if (_isSiegeScene && agent?.IsHuman == true && !agent.IsPlayerControlled && 
                agent.Team == Mission.Current.AttackerTeam && agent.IsActive())
            {
                agent.SetMorale(90f);
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
                        "铁血攻城：已阻止士兵撤退！",
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
                    // 如果Mission变为null主动清理
                    OnRemoveBehavior();
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
                    string.Format("铁血攻城错误: {0}", ex.Message),
                    Color.FromUint(0xFF0000FF)));
            }
        }

        public override void OnRemoveBehavior()
        {
            try
            {
                // 确保所有Formation恢复原状
                if (_chargedFormations != null)
                {
                    foreach (Formation formation in _chargedFormations.ToList())
                    {
                        try
                        {
                            if (formation != null && 
                                formation.Team != null && 
                                formation.Team.ActiveAgents.Count > 0)
                            {
                                formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                            }
                        }
                        catch
                        {
                            // 忽略单个Formation的清理错误
                        }
                    }
                    _chargedFormations.Clear();
                }

                // 重置所有状态
                _isSiegeScene = false;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;

                base.OnRemoveBehavior();
            }
            catch (Exception)
            {
                // 忽略清理过程中的错误
            }
        }
    }
} 