using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Setting;
using IronBloodSiege.Util;
using IronBloodSiege.Message;

namespace IronBloodSiege.Patche
{
    /// <summary>
    /// 攻城路线补丁 - 控制攻城路线的可用性判断
    /// </summary>
    [HarmonyPatch(typeof(SiegeLane))]
    public static class IbsSiegeLanePatch
    {
        private static readonly Dictionary<Team, bool> _gateDestroyedStates = new Dictionary<Team, bool>();

        [HarmonyPatch("CalculateIsLaneUnusable")]
        [HarmonyPostfix]
        public static void PostfixCalculateIsLaneUnusable(SiegeLane __instance, ref bool __result)
        {
            if (!IbsSettings.Instance.IsEnabled) return;
            if (Mission.Current == null) return;

            // 获取进攻方Team
            Team attackerTeam = Mission.Current.AttackerTeam;
            if (attackerTeam == null) return;

            // 检查并初始化状态
            if (!_gateDestroyedStates.ContainsKey(attackerTeam))
            {
                _gateDestroyedStates[attackerTeam] = false;
            }

            // 检查城门状态
            bool currentGateState = IbsBattleCheck.AreCurrentGatesDestroyed();
            if (currentGateState != _gateDestroyedStates[attackerTeam])
            {
                _gateDestroyedStates[attackerTeam] = currentGateState;
                if (currentGateState)
                {
                    IbsMessage.ShowBreachMiddle();
                }
            }

            // 如果是中路且城门已被摧毁，强制将该路线标记为可用
            if (_gateDestroyedStates[attackerTeam] && __instance.LaneSide == FormationAI.BehaviorSide.Middle)
            {
                __result = false;
                return;
            }

            // 如果是中路且攻城冲车被摧毁，但城门未被摧毁，保持该路线可用
            if (!_gateDestroyedStates[attackerTeam] && __instance.LaneSide == FormationAI.BehaviorSide.Middle && IbsBattleCheck.IsCurrentBatteringRamDestroyed())
            {
                __result = false;
                return;
            }
        }

        [HarmonyPatch("IsBreach", MethodType.Getter)]
        [HarmonyPostfix]
        public static void PostfixIsBreach(SiegeLane __instance, ref bool __result)
        {
            if (!IbsSettings.Instance.IsEnabled) return;
            if (Mission.Current == null) return;

            // 获取进攻方Team
            Team attackerTeam = Mission.Current.AttackerTeam;
            if (attackerTeam == null) return;

            if (!_gateDestroyedStates.ContainsKey(attackerTeam))
            {
                _gateDestroyedStates[attackerTeam] = false;
            }

            // 如果城门被摧毁，将中路标记为已突破
            if (_gateDestroyedStates[attackerTeam] && __instance.LaneSide == FormationAI.BehaviorSide.Middle)
            {
                __result = true;
            }
        }

        public static void ClearCache()
        {
            _gateDestroyedStates.Clear();
        }
    }
} 