using TaleWorlds.MountAndBlade;
using System;
using System.Linq;
using TaleWorlds.Library;

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
                    // 重置Formation的状态
                    formation.AI.ResetBehaviorWeights();
                    formation.SetControlledByAI(true, false);
                    formation.IsAITickedAfterSplit = false;
                    
                    attackerAI.OnUnitAddedToFormationForTheFirstTime(formation);
                    
                    formation.AI.SetBehaviorWeight<BehaviorAssaultWalls>(1f);
                    formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(1f);
                    formation.AI.SetBehaviorWeight<BehaviorShootFromSiegeTower>(1f);

                    formation.AI.SetBehaviorWeight<BehaviorRetreat>(0f);
                    formation.AI.SetBehaviorWeight<BehaviorRetreatToKeep>(0f);
                    formation.AI.SetBehaviorWeight<BehaviorPullBack>(0f);

                    if (teamAI.OuterGate != null || teamAI.InnerGate != null)
                    {
                        formation.AI.SetBehaviorWeight<BehaviorUseSiegeMachines>(2f);
                        formation.AI.SetBehaviorWeight<BehaviorDestroySiegeWeapons>(2f);
                    }

                    // 强制Formation的AI立即更新
                    formation.AI.Tick();
                }
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