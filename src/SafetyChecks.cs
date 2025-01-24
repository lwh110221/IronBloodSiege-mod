using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System;
using System.Linq;

namespace IronBloodSiege
{
    public static class SafetyChecks
    {
        /// <summary>
        /// 安全检查Mission.Current是否有效
        /// </summary>
        public static bool IsMissionValid()
        {
            try
            {
                return Mission.Current != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全检查攻城战场景
        /// </summary>
        public static bool IsSiegeSceneValid()
        {
            try
            {
                if (!IsMissionValid()) return false;

                return Mission.Current.Mode == MissionMode.Battle && 
                       !string.IsNullOrEmpty(Mission.Current.SceneName) &&
                       (Mission.Current.SceneName.ToLower().Contains("siege") || 
                        Mission.Current.SceneName.ToLower().Contains("castle")) &&
                       Mission.Current.DefenderTeam != null &&
                       Mission.Current.AttackerTeam != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全获取攻击方士兵数量
        /// </summary>
        public static int GetAttackerCount(Team team)
        {
            try
            {
                if (team == null || !IsMissionValid()) return 0;
                return team.ActiveAgents?.Count(a => IsValidAgent(a)) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检查Agent是否有效
        /// </summary>
        public static bool IsValidAgent(Agent agent)
        {
            try
            {
                return agent?.IsHuman == true && 
                       agent.IsActive() && 
                       !agent.IsPlayerControlled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查Formation是否有效
        /// </summary>
        public static bool IsValidFormation(Formation formation)
        {
            try
            {
                return formation != null && 
                       formation.Team == Mission.Current?.AttackerTeam;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全检查战斗是否结束
        /// </summary>
        public static bool IsBattleEnded()
        {
            try
            {
                if (!IsMissionValid()) return true;
                return Mission.Current.MissionEnded || 
                       Mission.Current.CheckIfBattleInRetreat();
            }
            catch
            {
                return true; // 出错时当作战斗已结束处理
            }
        }
    }
} 