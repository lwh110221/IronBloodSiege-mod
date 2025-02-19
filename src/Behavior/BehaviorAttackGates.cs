using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;

namespace IronBloodSiege.Behavior
{
    public class BehaviorAttackGates : BehaviorComponent
    {
        private readonly List<CastleGate> _targetGates;
        private CastleGate _currentTargetGate;
        private MovementOrder _attackOrder;
        private MovementOrder _chargeOrder;

        // public override float NavmeshlessTargetPositionPenalty => 0.5f;

        public BehaviorAttackGates(Formation formation) : base(formation)
        {
            // 设置较低的凝聚度，让士兵更分散
            base.BehaviorCoherence = 0.5f;

            // 获取所有敌方城门
            var teamAI = formation.Team.TeamAI as TeamAISiegeComponent;
            if (teamAI != null)
            {
                _targetGates = new List<CastleGate>();
                if (teamAI.OuterGate != null && !teamAI.OuterGate.IsDestroyed && teamAI.OuterGate.State == CastleGate.GateState.Closed)
                {
                    _targetGates.Add(teamAI.OuterGate);
                }
                if (teamAI.InnerGate != null && !teamAI.InnerGate.IsDestroyed && teamAI.InnerGate.State == CastleGate.GateState.Closed)
                {
                    _targetGates.Add(teamAI.InnerGate);
                }
            }

            _chargeOrder = MovementOrder.MovementOrderCharge;
            base.CurrentOrder = _chargeOrder;
        }

        public override TextObject GetBehaviorString()
        {
            TextObject behaviorString = base.GetBehaviorString();
            TextObject variable = GameTexts.FindText("str_formation_ai_side_strings", base.Formation.AI.Side.ToString());
            behaviorString.SetTextVariable("SIDE_STRING", variable);
            behaviorString.SetTextVariable("IS_GENERAL_SIDE", "0");
            return behaviorString;
        }

        public override void OnValidBehaviorSideChanged()
        {
            base.OnValidBehaviorSideChanged();
            
            // 重新检查目标城门
            var teamAI = base.Formation.Team.TeamAI as TeamAISiegeComponent;
            if (teamAI != null)
            {
                _targetGates.Clear();
                if (teamAI.OuterGate != null && !teamAI.OuterGate.IsDestroyed && teamAI.OuterGate.State == CastleGate.GateState.Closed)
                {
                    _targetGates.Add(teamAI.OuterGate);
                }
                if (teamAI.InnerGate != null && !teamAI.InnerGate.IsDestroyed && teamAI.InnerGate.State == CastleGate.GateState.Closed)
                {
                    _targetGates.Add(teamAI.InnerGate);
                }
            }
        }

        public override void TickOccasionally()
        {
            base.TickOccasionally();

            // 移除已被摧毁或打开的城门
            _targetGates?.RemoveAll(g => g == null || g.IsDestroyed || g.State == CastleGate.GateState.Open);

            if (base.Formation.AI.ActiveBehavior != this)
            {
                return;
            }

            if (_targetGates == null || _targetGates.Count == 0)
            {
                if (base.CurrentOrder != _chargeOrder)
                {
                    base.CurrentOrder = _chargeOrder;
                }
            }
            else
            {
                // 选择最近的城门作为目标
                CastleGate nearestGate = _targetGates.MinBy(g => 
                    base.Formation.QuerySystem.AveragePosition.DistanceSquared(g.GameEntity.GlobalPosition.AsVec2));

                if (_currentTargetGate != nearestGate)
                {
                    _currentTargetGate = nearestGate;
                    // 设置为直接攻击实体，不需要包围
                    _attackOrder = MovementOrder.MovementOrderAttackEntity(_currentTargetGate.GameEntity, false);
                    base.CurrentOrder = _attackOrder;
                }

                // 如果部队距离城门很近但还没有攻击命令，强制设置攻击命令
                float distanceToGate = base.Formation.QuerySystem.AveragePosition.Distance(_currentTargetGate.GameEntity.GlobalPosition.AsVec2);
                if (distanceToGate < 15f && base.CurrentOrder.OrderEnum != MovementOrder.MovementOrderEnum.AttackEntity)
                {
                    base.CurrentOrder = _attackOrder;
                }
            }

            base.Formation.SetMovementOrder(base.CurrentOrder);
            
            // 如果在攻击城门，确保Formation保持松散
            if (_currentTargetGate != null)
            {   
                // base.Formation.ArrangementOrder = ArrangementOrder.ArrangementOrderSquare;
                base.Formation.FormOrder = FormOrder.FormOrderDeep;
                
                // 设置面向敌人的方向
                Vec2 directionToGate = (_currentTargetGate.GameEntity.GlobalPosition.AsVec2 - base.Formation.QuerySystem.AveragePosition).Normalized();
                base.Formation.FacingOrder = FacingOrder.FacingOrderLookAtDirection(directionToGate);
            }
        }

        protected override void OnBehaviorActivatedAux()
        {
            // base.Formation.ArrangementOrder = ArrangementOrder.ArrangementOrderSquare;
            base.Formation.FacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            base.Formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;
            base.Formation.FormOrder = FormOrder.FormOrderDeep;
        }

        protected override float GetAiWeight()
        {
            if (_targetGates != null && _targetGates.Count > 0)
            {
                return 1.0f;
            }
            return 0f;
        }
    }
} 