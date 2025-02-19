using TaleWorlds.MountAndBlade;
using System;
using TaleWorlds.Core;
using IronBloodSiege.Behavior;

namespace IronBloodSiege.Logic
{
    public static class SiegeAIBehavior
    {
        public static void ApplyAttackBehavior(Formation formation)
        {
            if (formation?.AI == null || formation.Team == null) return;

            try
            {
                var teamAI = formation.Team.TeamAI as TeamAISiegeComponent;
                if (teamAI == null) return;

                // 重新激活原生攻城战术
                if (teamAI is TeamAISiegeAttacker attackerAI)
                {
                    attackerAI.OnUnitAddedToFormationForTheFirstTime(formation);
                    formation.IsAITickedAfterSplit = false;

                    formation.AI.AddAiBehavior(new BehaviorAttackGates(formation));
                    formation.AI.SetBehaviorWeight<BehaviorRetreat>(0f);
                    formation.AI.SetBehaviorWeight<BehaviorRetreatToKeep>(0f);
                    formation.AI.SetBehaviorWeight<BehaviorPullBack>(0.1f);
                    
                    // 城门状态
                    bool hasDestroyedGates = CheckForDestroyedGates(teamAI);
                    
                    switch (formation.RepresentativeClass)
                    {
                        case FormationClass.Infantry:
                        case FormationClass.HeavyInfantry:
                        case FormationClass.Cavalry:
                        case FormationClass.LightCavalry:
                        case FormationClass.HeavyCavalry:
                        case FormationClass.NumberOfDefaultFormations:
                            if (hasDestroyedGates)
                            {
                                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                                // 城门已破坏时的权重分配
                                formation.AI.SetBehaviorWeight<BehaviorAttackGates>(0.5f);   
                                formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.1f); 
                                formation.AI.SetBehaviorWeight<BehaviorCharge>(1.0f);        
                                formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.5f);   
                                formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(0.4f);
                                formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(0.4f);
                                formation.AI.SetBehaviorWeight<BehaviorShootFromSiegeTower>(0f);
                                
                                // 设置辅助行为权重
                                formation.AI.SetBehaviorWeight<BehaviorRegroup>(0f); 
                                formation.AI.SetBehaviorWeight<BehaviorReserve>(0.3f);
                                formation.AI.SetBehaviorWeight<BehaviorStop>(0.1f);
                                formation.AI.SetBehaviorWeight<BehaviorSkirmish>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorSparseSkirmish>(0f);
                            }
                            else
                            {
                                // 城门未破坏时的权重分配
                                formation.AI.SetBehaviorWeight<BehaviorAttackGates>(1.0f);
                                formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.9f);
                                formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(0.85f);
                                formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(0.8f);
                                formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.5f);
                                formation.AI.SetBehaviorWeight<BehaviorShootFromSiegeTower>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorRegroup>(0.3f);
                                formation.AI.SetBehaviorWeight<BehaviorReserve>(0.4f);
                                formation.AI.SetBehaviorWeight<BehaviorStop>(0.3f);
                                formation.AI.SetBehaviorWeight<BehaviorCharge>(0.9f);
                                formation.AI.SetBehaviorWeight<BehaviorSkirmish>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorSparseSkirmish>(0f);
                            }
                            break;
                            
                        case FormationClass.Ranged:
                        case FormationClass.HorseArcher:
                        case FormationClass.General:
                            if (hasDestroyedGates)
                            {
                                // 远程单位在城门破坏后的权重
                                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                                formation.AI.SetBehaviorWeight<BehaviorCharge>(1.0f);
                                formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.3f);
                                formation.AI.SetBehaviorWeight<BehaviorSkirmish>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorSparseSkirmish>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorRegroup>(0.2f);
                                formation.AI.SetBehaviorWeight<BehaviorReserve>(0.3f);
                                formation.AI.SetBehaviorWeight<BehaviorStop>(0.2f);
                                formation.AI.SetBehaviorWeight<BehaviorAttackGates>(0f);
                                formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.2f);
                                formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(0.1f);
                                formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(0.1f);
                                formation.AI.SetBehaviorWeight<BehaviorShootFromSiegeTower>(0.1f);
                            }
                            else
                            {
                                // 远程单位在城门未破坏时的权重
                                formation.AI.SetBehaviorWeight<BehaviorSkirmish>(0.7f);
                                formation.AI.SetBehaviorWeight<BehaviorSparseSkirmish>(0.7f);
                                formation.AI.SetBehaviorWeight<BehaviorShootFromSiegeTower>(0.5f);
                                formation.AI.SetBehaviorWeight<BehaviorRegroup>(0.7f);
                                formation.AI.SetBehaviorWeight<BehaviorReserve>(0.5f);
                                formation.AI.SetBehaviorWeight<BehaviorStop>(0.4f);
                                formation.AI.SetBehaviorWeight<BehaviorCharge>(0.6f);
                                formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(0.8f);
                                formation.AI.SetBehaviorWeight<BehaviorAttackGates>(0.2f);
                                formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(0.1f);
                                formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(0.1f);
                                formation.AI.SetBehaviorWeight<BehaviorWaitForLadders>(0.1f);
                            }
                            break;
                    }                   
                    formation.AI.Tick();
                    
                    #if DEBUG
                    Util.Logger.LogDebug("AI战术", 
                        $"应用攻城战术 - 编队类型: {formation.FormationIndex}, " +
                        $"城门状态: {(hasDestroyedGates ? "已破坏" : "未破坏")}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                #if DEBUG
                Util.Logger.LogError("AI战术", ex);
                #endif
            }
        }

        private static bool CheckForDestroyedGates(TeamAISiegeComponent teamAI)
        {
            // 检查外门
            bool outerGateAvailable = teamAI.OuterGate != null && 
                                     !teamAI.OuterGate.IsDestroyed && 
                                     teamAI.OuterGate.State == CastleGate.GateState.Closed;
                                     
            // 检查内门
            bool innerGateAvailable = teamAI.InnerGate != null && 
                                     !teamAI.InnerGate.IsDestroyed && 
                                     teamAI.InnerGate.State == CastleGate.GateState.Closed;
            
            return !outerGateAvailable && !innerGateAvailable;
        }
    }
}