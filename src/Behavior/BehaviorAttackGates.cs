/*
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Setting;
using IronBloodSiege.Util;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace IronBloodSiege.Behavior
{
    /// <summary>
    /// 攻击城门行为 - 当攻城冲车被摧毁时，提供攻击城门的备选方案
    /// </summary>
    public class BehaviorAttackGates : BehaviorComponent
    {
        private enum BehaviorState
        {
            Deciding,
            AttackGate,
            Charging
        }

        private BehaviorState _behaviorState;
        private List<CastleGate> _targetGates;
        private CastleGate _currentTargetGate;
        private MovementOrder _attackOrder;
        private MovementOrder _chargeOrder;
        private MovementOrder _stopOrder;

        public override float NavmeshlessTargetPositionPenalty => 1f;

        public BehaviorAttackGates(Formation formation) : base(formation)
        {
            _behaviorState = BehaviorState.Deciding;
            _chargeOrder = MovementOrder.MovementOrderCharge;
            _stopOrder = MovementOrder.MovementOrderStop;
            CurrentOrder = _stopOrder;
            UpdateTargetGates();
        }

        private void UpdateTargetGates()
        {
            _targetGates = IbsBattleCheck.GetIntactGates(Mission.Current);
        }

        private ArrangementOrder GetFormationArrangementOrder()
        {
            switch (IbsSettings.Instance.GateAttackFormationType.SelectedValue)
            {
                case GateAttackFormation.Line:
                    return ArrangementOrder.ArrangementOrderLine;
                case GateAttackFormation.Square:
                    return ArrangementOrder.ArrangementOrderSquare;
                case GateAttackFormation.Loose:
                    return ArrangementOrder.ArrangementOrderLoose;
                case GateAttackFormation.ShieldWall:
                    return ArrangementOrder.ArrangementOrderShieldWall;
                default:
                    return ArrangementOrder.ArrangementOrderShieldWall;
            }
        }

        public override TextObject GetBehaviorString()
        {
            TextObject behaviorString = base.GetBehaviorString();
            TextObject variable = GameTexts.FindText("str_formation_ai_side_strings", base.Formation.AI.Side.ToString());
            behaviorString.SetTextVariable("SIDE_STRING", variable);
            behaviorString.SetTextVariable("IS_GENERAL_SIDE", "0");
            return behaviorString;
        }

        private BehaviorState CheckAndChangeState()
        {
            UpdateTargetGates();

            switch (_behaviorState)
            {
                case BehaviorState.Deciding:
                    if (_targetGates.Count == 0)
                    {
                        return BehaviorState.Charging;
                    }
                    return BehaviorState.AttackGate;

                case BehaviorState.AttackGate:
                    if (_targetGates.Count == 0)
                    {
                        return BehaviorState.Charging;
                    }

                    if (_currentTargetGate == null || _currentTargetGate.IsDestroyed || _currentTargetGate.State == CastleGate.GateState.Open)
                    {
                        return BehaviorState.Deciding;
                    }

                    return BehaviorState.AttackGate;

                case BehaviorState.Charging:
                    if (_targetGates.Count > 0)
                    {
                        return BehaviorState.Deciding;
                    }
                    return BehaviorState.Charging;

                default:
                    return BehaviorState.Deciding;
            }
        }

        protected override void CalculateCurrentOrder()
        {
            switch (_behaviorState)
            {
                case BehaviorState.Deciding:
                    CurrentOrder = _stopOrder;
                    break;

                case BehaviorState.AttackGate:
                    if (_targetGates.Count > 0)
                    {
                        _currentTargetGate = IbsBattleCheck.GetNearestGate(Formation.QuerySystem.AveragePosition, Mission.Current);

                        if (_currentTargetGate != null)
                        {
                            _attackOrder = MovementOrder.MovementOrderAttackEntity(_currentTargetGate.GameEntity, false);
                            CurrentOrder = _attackOrder;
                        }
                        else
                        {
                            CurrentOrder = _chargeOrder;
                        }
                    }
                    else
                    {
                        CurrentOrder = _chargeOrder;
                    }
                    break;

                case BehaviorState.Charging:
                    CurrentOrder = _chargeOrder;
                    break;
            }
        }

        public override void TickOccasionally()
        {
            BehaviorState newState = CheckAndChangeState();
            if (newState != _behaviorState)
            {
                _behaviorState = newState;
                CalculateCurrentOrder();
            }

            Formation.SetMovementOrder(CurrentOrder);

            if (_behaviorState == BehaviorState.AttackGate && _currentTargetGate != null)
            {
                Formation.ArrangementOrder = GetFormationArrangementOrder();
                Formation.FormOrder = FormOrder.FormOrderDeep;
                Formation.FacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            }
        }

        protected override void OnBehaviorActivatedAux()
        {
            Formation.ArrangementOrder = GetFormationArrangementOrder();
            Formation.FormOrder = FormOrder.FormOrderDeep;
            
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.FacingOrder = FacingOrder.FacingOrderLookAtEnemy;
            Formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;
        }

        protected override float GetAiWeight()
        {
            if (!IbsSettings.Instance.IsEnabled) return 0f;
            if (_targetGates == null || _targetGates.Count == 0) return 0f;

            bool isRamDestroyed = IbsBattleCheck.IsCurrentBatteringRamDestroyed();
            return isRamDestroyed ? 1.0f : 0f;
        }
    }
}
*/