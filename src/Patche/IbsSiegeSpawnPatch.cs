using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using IronBloodSiege.Setting;
using TaleWorlds.MountAndBlade;

namespace IronBloodSiege.Patche
{
    [HarmonyPatch(typeof(SandBoxSiegeMissionSpawnHandler))]
    public static class IbsSiegeSpawnPatch
    {
        [HarmonyPatch("AfterStart")]
        [HarmonyPrefix]
        [HarmonyBefore(new string[] { "com.rbmai" })]
        [HarmonyPriority(Priority.First)]
        public static bool PrefixAfterStart(ref MapEvent ____mapEvent, ref MissionAgentSpawnLogic ____missionAgentSpawnLogic)
        {
            if (!IbsSettings.Instance.IsEnabled || !IbsSettings.Instance.EnableSpawnBalance) return true;
            if (____mapEvent == null) return true;

            if (!Mission.Current.IsSiegeBattle) return true;

            int battleSize = ____missionAgentSpawnLogic.BattleSize;

            int defenderTotal = ____mapEvent.GetNumberOfInvolvedMen(BattleSideEnum.Defender);
            int attackerTotal = ____mapEvent.GetNumberOfInvolvedMen(BattleSideEnum.Attacker);

            int totalInitialSpawn = battleSize;
            int attackerInitialSpawn = (int)(totalInitialSpawn * (IbsSettings.Instance.AttackerTroopsRatio / 100f));
            int defenderInitialSpawn = totalInitialSpawn - attackerInitialSpawn;

            attackerInitialSpawn = System.Math.Min(attackerInitialSpawn, attackerTotal);
            defenderInitialSpawn = System.Math.Min(defenderInitialSpawn, defenderTotal);

            ____missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, false);
            ____missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, false);

            MissionSpawnSettings spawnSettings = MissionSpawnSettings.CreateDefaultSpawnSettings();

            float defenderAdvantage = 0.6f;
            spawnSettings.DefenderAdvantageFactor = defenderAdvantage;

            ____missionAgentSpawnLogic.InitWithSinglePhase(
                defenderTotal,
                attackerTotal,
                defenderInitialSpawn,
                attackerInitialSpawn,
                spawnDefenders: true,
                spawnAttackers: true,
                in spawnSettings
            );

            return false;
        }
    }
}