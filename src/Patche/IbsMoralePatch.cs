using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Patche
{
    [HarmonyPatch(typeof(SandboxBattleMoraleModel))]
    public static class IbsMoralePatch
    {
        private static float DEFAULT_RETREAT_PERCENTAGE => Setting.IbsSettings.Instance?.CanRetreatRatios ?? 70.0f;

        private static bool ShouldApplyMoraleEffect(Team agentTeam)
        {
            if (!IbsSettings.Instance.IsEnabled) return false;
            if (agentTeam == null || Mission.Current?.PlayerTeam == null) return false;

            bool isSiegeScene = IbsBattleCheck.IsCurrentSiegeScene();
            if (!isSiegeScene) return false;

            bool isAttacker = agentTeam == Mission.Current.AttackerTeam;
            if (!isAttacker) return false;

            if (agentTeam == Mission.Current.PlayerTeam)
            {
                if (!IbsSettings.Instance.EnableWhenPlayerAttacker) return false;
            }

            float casualtyPercentage = IbsAgentCountCheck.GetCasualtyPercentage(agentTeam);
            if (casualtyPercentage >= DEFAULT_RETREAT_PERCENTAGE)
            {
                return false;
            }

            return true;
        }

        [HarmonyPatch("CanPanicDueToMorale")]
        [HarmonyPostfix]
        public static void PostfixCanPanic(Agent agent, ref bool __result)
        {
            if (!IbsBattleCheck.IsCurrentSiegeScene())
            {
                return;
            }

            if (IbsSettings.Instance.IsEnabled && agent?.Team != null && ShouldApplyMoraleEffect(agent.Team))
            {
                __result = false;
            }
        }

        [HarmonyPatch("CalculateMaxMoraleChangeDueToAgentIncapacitated")]
        [HarmonyPrefix]
        public static bool PrefixMoraleChangeIncapacitated(Agent affectedAgent, AgentState affectedAgentState, Agent affectorAgent, in KillingBlow killingBlow, ref ValueTuple<float, float> __result)
        {
            if (!IbsBattleCheck.IsCurrentSiegeScene())
            {
                return true;
            }

            if (IbsSettings.Instance.IsEnabled && affectedAgent?.Team != null && ShouldApplyMoraleEffect(affectedAgent.Team))
            {
                __result = new ValueTuple<float, float>(0f, 0f);
                return false;
            }

            return true;
        }

        [HarmonyPatch("CalculateCasualtiesFactor")]
        [HarmonyPostfix]
        public static void PostfixCasualtiesFactor(BattleSideEnum battleSide, ref float __result)
        {
            if (!IbsBattleCheck.IsCurrentSiegeScene())
            {
                return;
            }

            Team team = Mission.Current?.Teams.Find(t => t.Side == battleSide);
            if (IbsSettings.Instance.IsEnabled && team != null && ShouldApplyMoraleEffect(team))
            {
                __result = 0f;
            }
        }

        [HarmonyPatch("CalculateMoraleChangeToCharacter")]
        [HarmonyPostfix]
        public static void PostfixMoraleChange(Agent agent, float maxMoraleChange, ref float __result)
        {
            if (!IbsBattleCheck.IsCurrentSiegeScene())
            {
                return;
            }

            if (IbsSettings.Instance.IsEnabled && agent?.Team != null && ShouldApplyMoraleEffect(agent.Team))
            {
                __result = 0f;
            }
        }
    }
}
