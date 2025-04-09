using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Setting;
using IronBloodSiege.Util;
using IronBloodSiege.Message;

namespace IronBloodSiege.Patche
{
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

            Team attackerTeam = Mission.Current.AttackerTeam;
            if (attackerTeam == null) return;

            if (!_gateDestroyedStates.ContainsKey(attackerTeam))
            {
                _gateDestroyedStates[attackerTeam] = false;
            }


            if (_gateDestroyedStates[attackerTeam] && __instance.LaneSide == FormationAI.BehaviorSide.Middle)
            {
                __result = false;
                return;
            }

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

            Team attackerTeam = Mission.Current.AttackerTeam;
            if (attackerTeam == null) return;

            if (!_gateDestroyedStates.ContainsKey(attackerTeam))
            {
                _gateDestroyedStates[attackerTeam] = false;
            }

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