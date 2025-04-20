using TaleWorlds.MountAndBlade;
using System.Linq;
using TaleWorlds.Core;

namespace IronBloodSiege.Util
{
    /// <summary>
    /// 攻城战部队数量检查工具类
    /// </summary>
    public static class IbsAgentCountCheck
    {
        // 缓存的数量
        private static int _cachedAttackerCount;
        private static int _cachedDefenderCount;
        
        // 缓存的初始总兵力
        private static int _initialAttackerTotalCount;
        private static int _initialDefenderTotalCount;
        private static bool _hasInitialCounts;
        
        // 上次更新时间
        private static float _lastUpdateTime;
        
        // 更新间隔（游戏秒）
        private const float UPDATE_INTERVAL = 1f;
        
        // 当前缓存的任务引用
        private static Mission _lastMission;

        /// <summary>
        /// 重置检查状态
        /// </summary>
        public static void ResetCheckStatus()
        {
            _cachedAttackerCount = 0;
            _cachedDefenderCount = 0;
            _lastUpdateTime = 0f;
            _hasInitialCounts = false;
            _initialAttackerTotalCount = 0;
            _initialDefenderTotalCount = 0;
        }
        
        /// <summary>
        /// 初始化总兵力数量
        /// </summary>
        private static void InitializeTotalCounts(Mission mission)
        {
            bool isInitialized = _hasInitialCounts || mission == null;
            if (isInitialized) return;

            var spawnLogic = mission.GetMissionBehavior<MissionAgentSpawnLogic>();
            bool hasNoSpawnLogic = spawnLogic == null;
            if (hasNoSpawnLogic) return;

            // 检查是否在部署阶段
            bool isDeploymentPhase = !spawnLogic.IsInitialSpawnOver;
            if (isDeploymentPhase)
            {
                return;
            }

            int activeAttackers = spawnLogic.NumberOfActiveAttackerTroops;
            int remainingAttackers = spawnLogic.NumberOfRemainingAttackerTroops;
            int activeDefenders = mission.DefenderTeam.ActiveAgents.Where(agent => !agent.IsMount).Count();
            int remainingDefenders = spawnLogic.NumberOfRemainingDefenderTroops;

            bool hasEnoughTroops = activeAttackers > 0 && activeDefenders > 0;
            if (hasEnoughTroops)
            {
                _initialAttackerTotalCount = activeAttackers + remainingAttackers;
                _initialDefenderTotalCount = activeDefenders + remainingDefenders;
                _hasInitialCounts = true;
            }
        }
        
        /// <summary>
        /// 更新缓存的数量
        /// </summary>
        private static void UpdateCachedCounts(Mission mission)
        {
            bool isMissionNull = mission == null;
            if (isMissionNull) 
            {
                return;
            }
            
            bool isNewMission = _lastMission != mission;
            if (isNewMission)
            {
                _lastMission = mission;
                ResetCheckStatus();
            }
            
            float currentTime = mission.CurrentTime;
            bool isUpdateNeeded = currentTime - _lastUpdateTime >= UPDATE_INTERVAL;
            if (!isUpdateNeeded) return;
            
            _lastUpdateTime = currentTime;
            
            // 初始化总兵力（如果还未初始化）
            InitializeTotalCounts(mission);
            
            // 更新攻城方和守城方数量
            bool hasValidTeams = mission.AttackerTeam != null && mission.DefenderTeam != null;
            if (hasValidTeams)
            {
                _cachedAttackerCount = mission.AttackerTeam.ActiveAgents.Count(a => a.IsHuman && !a.IsMount);
                _cachedDefenderCount = mission.DefenderTeam.ActiveAgents.Count(a => a.IsHuman && !a.IsMount);
                
                // 显示攻城方剩余兵力信息
                // ShowAttackerForceMessage(mission);
            }
            else
            {
                _cachedAttackerCount = 0;
                _cachedDefenderCount = 0;
            }
        }
        
        /// <summary>
        /// 获取战场双方的部队数量
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回攻城方和守城方的数量</returns>
        public static (int attackerCount, int defenderCount) GetAgentCount(Mission mission)
        {
            bool hasInvalidTeams = mission?.AttackerTeam == null || mission.DefenderTeam == null;
            if (hasInvalidTeams)
            {
                return (0, 0);
            }
            
            UpdateCachedCounts(mission);
            return (_cachedAttackerCount, _cachedDefenderCount);
        }
        
        /// <summary>
        /// 获取当前战场双方的部队数量
        /// </summary>
        /// <returns>返回当前战场双方的部队数量</returns>
        public static (int attackerCount, int defenderCount) GetCurrentAgentCount()
        {
            return GetAgentCount(Mission.Current);
        }
        
        /// <summary>
        /// 获取队伍的总兵力数量
        /// </summary>
        /// <param name="team">要检查的队伍</param>
        /// <returns>队伍的总兵力数量</returns>
        public static int GetTotalTroopCount(Team team)
        {
            bool isTeamNull = team == null;
            if (isTeamNull) return 0;
            
            var mission = Mission.Current;
            bool isMissionNull = mission == null;
            if (isMissionNull) return 0;

            // 初始化总兵力（如果还未初始化）
            InitializeTotalCounts(mission);

            var battleSide = team.Side;

            // 返回缓存的初始总兵力
            if (battleSide == BattleSideEnum.Attacker)
            {
                return _initialAttackerTotalCount;
            }
            else if (battleSide == BattleSideEnum.Defender)
            {
                return _initialDefenderTotalCount;
            }

            return 0;
        }

        /// <summary>
        /// 获取队伍的伤亡数量
        /// </summary>
        /// <param name="team">要检查的队伍</param>
        /// <returns>队伍的伤亡数量</returns>
        public static int GetCasualtiesCount(Team team)
        {
            bool isTeamNull = team == null || Mission.Current == null;
            if (isTeamNull) return 0;
            
            int casualties = 0;
            
            var casualtyHandler = Mission.Current.GetMissionBehavior<CasualtyHandler>();
            bool hasCasualtyHandler = casualtyHandler != null;
            if (hasCasualtyHandler)
            {
                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    bool isValidFormation = formation != null;
                    if (isValidFormation)
                    {
                        casualties += casualtyHandler.GetCasualtyCountOfFormation(formation);
                    }
                }
            }
            else
            {
                casualties = team.QuerySystem.DeathCount;
            }
            
            return casualties;
        }
        
        /// <summary>
        /// 计算队伍的伤亡百分比
        /// </summary>
        /// <param name="team">要检查的队伍</param>
        /// <returns>伤亡百分比（0-100）</returns>
        public static float GetCasualtyPercentage(Team team)
        {
            int totalTroops = GetTotalTroopCount(team);
            bool hasNoTroops = totalTroops == 0;
            if (hasNoTroops) return 0f;
            
            int casualties = GetCasualtiesCount(team);
            float percentage = (float)casualties / totalTroops * 100f;
            
            return percentage;
        }
        
        /// <summary>
        /// 检查玩家是否为进攻方
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果玩家是进攻方返回true，否则返回false</returns>
        public static bool IsPlayerAttacker(Mission mission)
        {
            bool hasInvalidTeams = mission?.PlayerTeam == null || mission.AttackerTeam == null;
            if (hasInvalidTeams)
            {
                return false;
            }

            return mission.PlayerTeam == mission.AttackerTeam;
        }
    }
}