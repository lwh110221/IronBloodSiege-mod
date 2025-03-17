using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;
using IronBloodSiege.Message;

namespace IronBloodSiege.Patche
{
    [HarmonyPatch(typeof(TacticBreachWalls))]
    public static class IbsTacticPatch
    {
        private static float DEFAULT_RETREAT_PERCENTAGE => Setting.IbsSettings.Instance?.CanRetreatRatios ?? 70.0f;

        [HarmonyPatch("ShouldRetreat")]
        [HarmonyPrefix] 
        public static bool PrefixShouldRetreat(ref bool __result, List<SiegeLane> lanes, int insideFormationCount)
        {
            if (!IbsSettings.Instance.IsEnabled) return true;

            if (!IbsBattleCheck.IsCurrentSiegeScene()) return true;

            var mission = Mission.Current;
            bool isAttackerTeam = mission.PlayerTeam == mission.AttackerTeam;

            if (isAttackerTeam && !IbsSettings.Instance.EnableWhenPlayerAttacker)
            {
                return true;
            }

            Team attackingTeam = mission.AttackerTeam;
            if (attackingTeam == null) return true;

            float casualtyPercentage = IbsAgentCountCheck.GetCasualtyPercentage(attackingTeam);

            if (casualtyPercentage >= DEFAULT_RETREAT_PERCENTAGE)
            {
                IbsMessage.ShowCanRettreat();
                return true;
            }

            __result = false;
            return false;
        }
    }
}