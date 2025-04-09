/*
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Setting;
using IronBloodSiege.Behavior;
using IronBloodSiege.Util;
using IronBloodSiege.Message;

namespace IronBloodSiege.Patche
{
    [HarmonyPatch(typeof(TacticBreachWalls))]
    public static class IbsAttackGatesPatch
    {
        private static bool _lastRamState = false;
        private static bool _lastGatesState = false;
        private static readonly Dictionary<Formation, BehaviorAttackGates> _behaviorCache = new Dictionary<Formation, BehaviorAttackGates>();

        [HarmonyPatch("AssignMeleeFormationsToLanes")]
        [HarmonyPostfix]
        public static void PostfixAssignMeleeFormationsToLanes(List<Formation> meleeFormationsSource, List<SiegeLane> currentLanes)
        {
            if (!IbsSettings.Instance.IsEnabled) return;

            bool currentRamState = IbsBattleCheck.IsCurrentBatteringRamDestroyed();
            bool currentGatesState = IbsBattleCheck.AreCurrentGatesDestroyed();

            if (currentRamState == _lastRamState && currentGatesState == _lastGatesState)
            {
                return;
            }

            foreach (var formation in meleeFormationsSource)
            {
                if (formation == null || formation.CountOfUnits <= 0) continue;

                BehaviorAttackGates attackGatesBehavior;
                if (!_behaviorCache.TryGetValue(formation, out attackGatesBehavior))
                {
                    attackGatesBehavior = formation.AI.GetBehavior<BehaviorAttackGates>();
                    if (attackGatesBehavior == null)
                    {
                        attackGatesBehavior = new BehaviorAttackGates(formation);
                        formation.AI.AddAiBehavior(attackGatesBehavior);
                    }
                    _behaviorCache[formation] = attackGatesBehavior;
                }

                if (currentGatesState && !_lastGatesState)
                {
                    attackGatesBehavior = formation.AI.GetBehavior<BehaviorAttackGates>();
                    if (attackGatesBehavior == null)
                    {
                        attackGatesBehavior = new BehaviorAttackGates(formation);
                        formation.AI.AddAiBehavior(attackGatesBehavior);
                    }
                    formation.AI.SetBehaviorWeight<BehaviorAttackGates>(0.1f);
                    formation.AI.SetBehaviorWeight<BehaviorCharge>(1.5f);
                    formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(1.0f);
                    formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(1.0f);
                    IbsMessage.ShowGateDestroyed();
                }
                else if (currentRamState && !_lastRamState)
                {
                    attackGatesBehavior = formation.AI.GetBehavior<BehaviorAttackGates>();
                    if (attackGatesBehavior == null)
                    {
                        attackGatesBehavior = new BehaviorAttackGates(formation);
                        formation.AI.AddAiBehavior(attackGatesBehavior);
                    }
                    formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.5f);
                    formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(0.5f);
                    formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.3f);
                    formation.AI.SetBehaviorWeight<BehaviorAttackGates>(1.0f);
                    IbsMessage.ShowAttackGate();
                }
            }

            _lastRamState = currentRamState;
            _lastGatesState = currentGatesState;

            List<Formation> formationsToRemove = new List<Formation>();
            foreach (var kvp in _behaviorCache)
            {
                if (!meleeFormationsSource.Contains(kvp.Key))
                {
                    formationsToRemove.Add(kvp.Key);
                }
            }
            foreach (var formation in formationsToRemove)
            {
                _behaviorCache.Remove(formation);
            }
        }

        [HarmonyPatch("TickOccasionally")]
        [HarmonyPostfix]
        public static void PostfixTickOccasionally(TacticBreachWalls __instance)
        {
            if (!IbsSettings.Instance.IsEnabled) return;

            if (IbsBattleCheck.IsCurrentBatteringRamDestroyed() && !IbsBattleCheck.AreCurrentGatesDestroyed())
            {
                foreach (var formation in _behaviorCache.Keys)
                {
                    if (formation == null || formation.CountOfUnits <= 0) continue;

                    var attackGatesBehavior = formation.AI.GetBehavior<BehaviorAttackGates>();
                    if (attackGatesBehavior != null)
                    {
                        formation.AI.SetBehaviorWeight<BehaviorAttackGates>(1.0f);
                    }
                }
            }
            else if(IbsBattleCheck.AreCurrentGatesDestroyed())
            {
                  foreach (var formation in _behaviorCache.Keys)
                {
                    if (formation == null || formation.CountOfUnits <= 0) continue;

                    var attackGatesBehavior = formation.AI.GetBehavior<BehaviorAttackGates>();
                    if (attackGatesBehavior != null)
                    {
                        formation.AI.SetBehaviorWeight<BehaviorAttackGates>(0.1f);
                        formation.AI.SetBehaviorWeight<BehaviorCharge>(1.5f);
                    }
                }
            }
        }
    }
}
*/