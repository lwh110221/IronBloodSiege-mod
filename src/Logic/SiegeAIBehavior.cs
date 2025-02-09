using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System;

namespace IronBloodSiege.Logic
{
    /// <summary>
    /// 攻城战AI战术
    /// </summary>
    public static class SiegeAIBehavior
    {
        public static void ApplyAttackBehavior(Formation formation)
        {
            if (formation?.AI == null || formation.Team == null) return;

            try
            {   
                formation.AI.ResetBehaviorWeights();

                formation.AI.SetBehaviorWeight<BehaviorRetreat>(0f);
                formation.AI.SetBehaviorWeight<BehaviorRetreatToKeep>(0f);
                
                formation.SetControlledByAI(true, false);
                
                switch (formation.RepresentativeClass)
                {
                    case FormationClass.Infantry:
                    case FormationClass.HeavyInfantry:
                    case FormationClass.Cavalry:
                    case FormationClass.LightCavalry:
                    case FormationClass.HeavyCavalry:
                        formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(3.0f);
                        formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(3.0f);
                        formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(3.0f);
                        formation.AI.SetBehaviorWeight<BehaviorDestroySiegeWeapons>(2.5f);
                        formation.AI.SetBehaviorWeight<BehaviorAdvance>(2.0f);
                        formation.AI.SetBehaviorWeight<BehaviorRegroup>(1.0f);
                        formation.AI.SetBehaviorWeight<BehaviorStop>(0.1f);
                        break;
                        
                    case FormationClass.Ranged:
                    case FormationClass.HorseArcher:
                        formation.AI.SetBehaviorWeight<BehaviorSkirmish>(2.0f);
                        break;
                }
                                
                formation.AI.Tick();
                
                #if DEBUG
                Util.Logger.LogDebug("AI战术", 
                    $"已设置战术 - Formation: {formation.FormationIndex}, " +
                    $"兵种: {formation.RepresentativeClass}, " +
                    $"当前行为: {formation.AI.ActiveBehavior?.GetType().Name ?? "无"}, " +
                    $"单位数量: {formation.CountOfUnits}");
                #endif
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("AI战术", ex);
                #endif
            }
        }
    }
}