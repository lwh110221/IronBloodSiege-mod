// using HarmonyLib;
// using TaleWorlds.MountAndBlade;
// using IronBloodSiege.Behavior;
// using IronBloodSiege.Setting;
// using IronBloodSiege.Message;
// using IronBloodSiege.Util;
// using TaleWorlds.Library;
// using System.Collections.Generic;
// using System.Linq;

// namespace IronBloodSiege.Patche
// {
//     [HarmonyPatch(typeof(TacticBreachWalls))]
//     public static class BehaviorRegistrationPatch
//     {
//         private static readonly Dictionary<Formation, BehaviorIronAssaultWalls> _behaviorCache = new Dictionary<Formation, BehaviorIronAssaultWalls>();
        
//         [HarmonyPatch("AssignMeleeFormationsToLanes")]
//         [HarmonyPostfix]
//         public static void AssignMeleeFormationsToLanesPostfix(List<Formation> meleeFormationsSource)
//         {
//             if (!IbsSettings.Instance.IsEnabled) return;
            
            
//             foreach (var formation in meleeFormationsSource)
//             {
//                 if (formation == null || formation.CountOfUnits <= 0) continue;
                
//                 var originalBehavior = formation.AI.GetBehavior<BehaviorAssaultWalls>();
                
//                 var customBehavior = formation.AI.GetBehavior<BehaviorIronAssaultWalls>();
//                 if (customBehavior == null)
//                 {
//                     customBehavior = new BehaviorIronAssaultWalls(formation);
//                     formation.AI.AddAiBehavior(customBehavior);
//                     _behaviorCache[formation] = customBehavior;
//                 }
                
//                 if (originalBehavior != null)
//                 {
//                     formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.01f);
//                 }
                
//                 if (IbsBattleCheck.IsCurrentBatteringRamDestroyed())
//                 {
//                     formation.AI.SetBehaviorWeight<BehaviorIronAssaultWalls>(0.80f);
//                 }
//                 else
//                 {
//                     formation.AI.SetBehaviorWeight<BehaviorIronAssaultWalls>(0.75f);
//                 }
//             }
            
//             List<Formation> formationsToRemove = new List<Formation>();
//             foreach (var kvp in _behaviorCache)
//             {
//                 if (!meleeFormationsSource.Contains(kvp.Key))
//                 {
//                     formationsToRemove.Add(kvp.Key);
//                 }
//             }
//             foreach (var formation in formationsToRemove)
//             {
//                 _behaviorCache.Remove(formation);
//             }
//         }
        
//         [HarmonyPatch("TickOccasionally")]
//         [HarmonyPostfix]
//         public static void TickOccasionallyPostfix()
//         {
//             if (!IbsSettings.Instance.IsEnabled) return;
            
//             foreach (var formation in _behaviorCache.Keys)
//             {
//                 if (formation == null || formation.CountOfUnits <= 0) continue;
                
//                 if (IbsBattleCheck.IsCurrentBatteringRamDestroyed())
//                 {
//                     formation.AI.SetBehaviorWeight<BehaviorIronAssaultWalls>(0.80f);
//                     formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.01f);
//                 }
//                 else
//                 {
//                     formation.AI.SetBehaviorWeight<BehaviorIronAssaultWalls>(0.75f);
//                     formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.01f);
//                 }
//             }
//         }
//     }
// } 