using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using IronBloodSiege.Message;


namespace IronBloodSiege.Patche
{
    [HarmonyPatch(typeof(TacticBreachWalls))]
    public static class IbsTacticPatch
    {
        private static float DEFAULT_RETREAT_PERCENTAGE => Setting.IbsSettings.Instance?.CanRetreatRatios ?? 70.0f;
        
        private static float _lastAttackerForceMessageTime = 0f;
        
        private const float ATTACKER_FORCE_MESSAGE_INTERVAL = 45f;
        private static bool _hasShownRetreatMessage = false;

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
            
            float currentTime = mission.CurrentTime;
            if (currentTime - _lastAttackerForceMessageTime >= ATTACKER_FORCE_MESSAGE_INTERVAL)
            {
                _lastAttackerForceMessageTime = currentTime;
                
                if (IbsSettings.Instance.ShowCasualtyMessage)
                {
                    int totalAttackerCount = IbsAgentCountCheck.GetTotalTroopCount(attackingTeam);
                    int casualtiesCount = IbsAgentCountCheck.GetCasualtiesCount(attackingTeam);
                    int remainingForce = totalAttackerCount - casualtiesCount;
                    
                    TextObject textObject = new TextObject("{=ibs_msg_attacker_force}Attacker Force Remaining: {REMAINING} (Casualty Rate: {RATE}%)");
                    textObject.SetTextVariable("REMAINING", remainingForce);
                    textObject.SetTextVariable("RATE", casualtyPercentage.ToString("F1"));
                    InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), Colors.Cyan));
                }
            }

            if (casualtyPercentage >= DEFAULT_RETREAT_PERCENTAGE)
            {
                if (!_hasShownRetreatMessage)
                {
                    IbsMessage.ShowCanRettreat();
                    _hasShownRetreatMessage = true;
                }
                return true;
            }

            __result = false;
            return false;
        }
    }
}