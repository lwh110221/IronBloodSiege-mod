using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Behavior
{
    public class SiegeMoraleManagerBehavior : MissionBehavior, IModBehavior
    {
        #region Fields
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        // 缓存攻防双方的Team引用
        private Team _attackerTeam;
        
        private bool _isSiegeScene = false;       // 是否是攻城场景
        private float _lastMoraleUpdateTime = 0f; // 最后一次更新士气的时间
        private float _lastMessageTime = 0f;      // 消息时间
        private bool _wasEnabledBefore = false;   // 添加字段跟踪之前的启用状态
        private bool _isDisabled = false;
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
                if (!_wasEnabledBefore) return;

                _currentMission = Mission.Current;
                _attackerTeam = _currentMission?.AttackerTeam;
                _isSiegeScene = CheckIfSiegeScene();

                // 如果不是攻城战场景，直接禁用
                if (!_isSiegeScene)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "非攻城战场景，禁用士气管理器");
                    #endif
                    OnModDisabled();
                    return;
                }
            }
            catch (Exception ex)
            {
                HandleError("初始化", ex);
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                // 检查mod是否被禁用
                if (_isDisabled)
                {
                    return;
                }

                // 检查总开关状态变化
                bool isCurrentlyEnabled = Settings.Instance.IsEnabled;
                if (_wasEnabledBefore != isCurrentlyEnabled)
                {
                    if (!isCurrentlyEnabled)
                    {
                        OnModDisabled();
                    }
                    else
                    {
                        OnModEnabled();
                    }
                    _wasEnabledBefore = isCurrentlyEnabled;
                }

                // 如果mod未启用或任务无效，直接返回
                if (!isCurrentlyEnabled || !SafetyChecks.IsMissionValid())
                {
                    return;
                }

                if (_attackerTeam != null)
                {
                    AdjustTeamMorale(_attackerTeam, dt);
                }
            }
            catch (Exception ex)
            {
                HandleError("任务更新", ex);
            }
        }

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

        public override void OnClearScene()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("场景清理", "开始清理场景");
                #endif
                OnModDisabled();
                base.OnClearScene();
            }
            catch (Exception ex)
            {
                HandleError("场景清理", ex);
            }
        }

        protected override void OnEndMission()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("任务结束", "开始任务结束清理");
                #endif

                // 恢复所有Agent的士气
                if (_attackerTeam != null)
                {
                    foreach (var agent in _attackerTeam.ActiveAgents)
                    {
                        if (agent != null && agent.IsActive())
                        {
                            BehaviorCleanupHelper.RestoreAgent(agent);
                        }
                    }
                }

                // 重置所有状态
                _isDisabled = true;
                _isSiegeScene = false;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _currentMission = null;
                _attackerTeam = null;
                _wasEnabledBefore = false;

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
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("移除行为", "开始移除行为");
                #endif

                // 恢复所有Agent的士气
                if (_attackerTeam != null)
                {
                    foreach (var agent in _attackerTeam.ActiveAgents)
                    {
                        if (agent != null && agent.IsActive())
                        {
                            BehaviorCleanupHelper.RestoreAgent(agent);
                        }
                    }
                }

                // 确保所有状态被重置
                OnModDisabled();
                _currentMission = null;
                _attackerTeam = null;
                _isSiegeScene = false;
                _lastMoraleUpdateTime = 0f;
                _lastMessageTime = 0f;
                _wasEnabledBefore = false;

                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                HandleError("移除行为", ex);
                base.OnRemoveBehavior();
            }
        }
        #endregion

        #region Core Logic
        private void AdjustTeamMorale(Team team, float dt)
        {
            try
            {
                // 如果mod未启用，直接返回
                if (!Settings.Instance.IsEnabled) return;

                // 严格检查是否为攻城方Team
                if (team == null || 
                    team != _attackerTeam || 
                    team.IsDefender) 
                    return;

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
                
                try
                {
                    // 调整士气并获取提升数量
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
                catch (Exception)
                {
                    _lastMoraleUpdateTime = currentTime - Constants.MORALE_UPDATE_INTERVAL;
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
            
            try
            {
                // 严格检查是否为攻城方Team
                if (team == null || 
                    team != _attackerTeam || 
                    team.IsDefender)
                    return boostedCount;

                // 获取需要更新士气的单位
                var agentsToUpdate = team.ActiveAgents
                    .Where(agent => 
                        SafetyChecks.IsValidAgent(agent) && 
                        !agent.IsPlayerControlled &&
                        agent.Team == _attackerTeam &&
                        !agent.Team.IsDefender &&
                        agent.Formation?.PlayerOwner == null &&  // Formation所有者检查
                        agent.Formation?.Captain?.IsPlayerControlled != true &&  // Formation队长检查
                        (agent.GetMorale() < moraleThreshold || agent.IsRetreating()))
                    .ToList();

                // 处理每个单位的士气
                foreach (Agent agent in agentsToUpdate)
                {
                    try
                    {
                        // 再次检查agent的有效性
                        if (!SafetyChecks.IsValidAgent(agent) || 
                            agent.IsPlayerControlled || 
                            agent.Formation?.PlayerOwner != null ||
                            agent.Formation?.Captain?.IsPlayerControlled == true)
                            continue;

                        float oldMorale = agent.GetMorale();
                        if (oldMorale < moraleThreshold || agent.IsRetreating())
                        {
                            // 直接使用设置的提升值
                            float targetMorale = oldMorale + moraleBoostRate;
                            targetMorale = Math.Min(targetMorale, 100f);
                            
                            try
                            {
                                agent.SetMorale(targetMorale);

                                if (targetMorale > oldMorale)
                                {
                                    boostedCount++;
                                }

                                // 停止撤退状态
                                if (agent.IsRetreating())
                                {
                                    agent.StopRetreating();
                                }
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            
                            #if DEBUG
                            if (boostedCount % 10 == 0)
                            {
                                Util.Logger.LogDebug("士气更新", 
                                    $"单位士气已更新 - 旧士气: {oldMorale:F2}, " +
                                    $"新士气: {targetMorale:F2}, " +
                                    $"已提升数量: {boostedCount}");
                            }
                            #endif
                        }
                    }
                    catch (Exception)
                    {
                        // 单个Agent处理失败不影响其他Agent
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("AI士气更新", ex);
            }
            
            return boostedCount;
        }
        #endregion

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

        public void OnModEnabled()
        {
            _isDisabled = false;
        }

        public void OnModDisabled()
        {
            if (!_isDisabled)
            {
                _isDisabled = true;
                
                // 恢复所有Agent的士气
                if (_attackerTeam != null)
                {
                    foreach (var agent in _attackerTeam.ActiveAgents)
                    {
                        if (agent != null && agent.IsActive())
                        {
                            BehaviorCleanupHelper.RestoreAgent(agent);
                        }
                    }
                }
            }
        }

        public void OnSceneChanged()
        {
            _isSiegeScene = false;
        }
    }
}