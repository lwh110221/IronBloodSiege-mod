using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using IronBloodSiege.Setting;
using IronBloodSiege.Util;
using IronBloodSiege.Message;

namespace TaleWorlds.MountAndBlade
{
    public class BehaviorAssaultWalls : BehaviorComponent
    {
        private enum BehaviorState
        {
            Deciding,
            ClimbWall,
            AttackEntity,
            TakeControl,
            MoveToGate,
            Charging,
            Stop
        }

        private BehaviorState _behaviorState;
        private List<IPrimarySiegeWeapon> _primarySiegeWeapons;
        private WallSegment _wallSegment;
        private CastleGate _innerGate;
        private TeamAISiegeComponent _teamAISiegeComponent;
        private MovementOrder _attackEntityOrderInnerGate;
        private MovementOrder _attackEntityOrderOuterGate;
        private MovementOrder _chargeOrder;
        private MovementOrder _stopOrder;
        private MovementOrder _castleGateMoveOrder;
        private MovementOrder _wallSegmentMoveOrder;
        private FacingOrder _facingOrder;
        protected ArrangementOrder CurrentArrangementOrder;
        private bool _isGateLane;
        private static bool _ramDestroyedMessageShown = false;

        public override float NavmeshlessTargetPositionPenalty => 1f;

        private void ResetOrderPositions()
        {
            _primarySiegeWeapons = _teamAISiegeComponent.PrimarySiegeWeapons.ToList();
            _primarySiegeWeapons.RemoveAll((IPrimarySiegeWeapon uM) => uM.WeaponSide != _behaviorSide);
            IEnumerable<ICastleKeyPosition> source = TeamAISiegeComponent.SiegeLanes
                .Where((SiegeLane sl) => sl.LaneSide == _behaviorSide)
                .SelectMany((SiegeLane sila) => sila.DefensePoints);
            
            _innerGate = _teamAISiegeComponent.InnerGate;
            _isGateLane = _teamAISiegeComponent.OuterGate.DefenseSide == _behaviorSide;
            
            if (_isGateLane)
            {
                _wallSegment = null;
            }
            else if (source.FirstOrDefault((ICastleKeyPosition dp) => dp is WallSegment && (dp as WallSegment).IsBreachedWall) is WallSegment wallSegment)
            {
                _wallSegment = wallSegment;
            }
            else
            {
                IPrimarySiegeWeapon primarySiegeWeapon = _primarySiegeWeapons.MaxBy((IPrimarySiegeWeapon psw) => psw.SiegeWeaponPriority);
                if (primarySiegeWeapon != null)
                {
                    _wallSegment = primarySiegeWeapon.TargetCastlePosition as WallSegment;
                }
            }

            _stopOrder = MovementOrder.MovementOrderStop;
            _chargeOrder = MovementOrder.MovementOrderCharge;
            
            bool flag = _teamAISiegeComponent.OuterGate != null && _behaviorSide == _teamAISiegeComponent.OuterGate.DefenseSide;
            
            bool outerGateCanBeAttacked = flag && _teamAISiegeComponent.OuterGate != null && !_teamAISiegeComponent.OuterGate.IsDeactivated && _teamAISiegeComponent.OuterGate.State != 0;
            _attackEntityOrderOuterGate = outerGateCanBeAttacked 
                ? MovementOrder.MovementOrderAttackEntity(_teamAISiegeComponent.OuterGate.GameEntity, surroundEntity: false) 
                : MovementOrder.MovementOrderStop;
            
            bool innerGateCanBeAttacked = flag && _teamAISiegeComponent.InnerGate != null && !_teamAISiegeComponent.InnerGate.IsDeactivated && _teamAISiegeComponent.InnerGate.State != 0;
            _attackEntityOrderInnerGate = innerGateCanBeAttacked 
                ? MovementOrder.MovementOrderAttackEntity(_teamAISiegeComponent.InnerGate.GameEntity, surroundEntity: false) 
                : MovementOrder.MovementOrderStop;
            
            WorldPosition origin = _teamAISiegeComponent.OuterGate.MiddleFrame.Origin;
            _castleGateMoveOrder = MovementOrder.MovementOrderMove(origin);
            
            if (_isGateLane)
            {
                _wallSegmentMoveOrder = _castleGateMoveOrder;
            }
            else if (_wallSegment != null)
            {
                WorldPosition origin2 = _wallSegment.MiddleFrame.Origin;
                _wallSegmentMoveOrder = MovementOrder.MovementOrderMove(origin2);
            }
            else
            {
                _wallSegmentMoveOrder = _castleGateMoveOrder;
            }

            _facingOrder = FacingOrder.FacingOrderLookAtEnemy;
        }

        public BehaviorAssaultWalls(Formation formation) : base(formation)
        {
            BehaviorCoherence = 0f;
            _behaviorSide = formation.AI.Side;
            _teamAISiegeComponent = (TeamAISiegeComponent)formation.Team.TeamAI;
            _behaviorState = BehaviorState.Deciding;
            ResetOrderPositions();
            CurrentOrder = _stopOrder;
        }

        public override TextObject GetBehaviorString()
        {
            TextObject behaviorString = base.GetBehaviorString();
            TextObject variable = GameTexts.FindText("str_formation_ai_side_strings", Formation.AI.Side.ToString());
            behaviorString.SetTextVariable("SIDE_STRING", variable);
            behaviorString.SetTextVariable("IS_GENERAL_SIDE", "0");
            return behaviorString;
        }

        private BehaviorState CheckAndChangeState()
        {
            switch (_behaviorState)
            {
                case BehaviorState.Deciding:
                    if (!_isGateLane && _wallSegment == null)
                    {
                        return BehaviorState.Charging;
                    }

                    if (_isGateLane)
                    {
                        if (IbsSettings.Instance.IsEnabled && 
                            IbsSettings.Instance.EnableAttackGates && 
                            IbsBattleCheck.IsCurrentBatteringRamDestroyed())
                        {
                            if (!_ramDestroyedMessageShown)
                            {
                                _ramDestroyedMessageShown = true;
                                IbsMessage.ShowRamDestroyed();
                            }
                            return BehaviorState.AttackEntity;
                        }
                        
                        if (_teamAISiegeComponent.OuterGate.IsGateOpen && _teamAISiegeComponent.InnerGate.IsGateOpen)
                        {
                            return BehaviorState.Charging;
                        }

                        return BehaviorState.AttackEntity;
                    }

                    return BehaviorState.ClimbWall;
                    
                case BehaviorState.ClimbWall:
                    {
                        if (_wallSegment == null)
                        {
                            return BehaviorState.Charging;
                        }

                        bool flag = false;
                        if (_behaviorSide < FormationAI.BehaviorSide.BehaviorSideNotSet)
                        {
                            SiegeLane siegeLane = TeamAISiegeComponent.SiegeLanes[(int)_behaviorSide];
                            flag = siegeLane.IsUnderAttack() && !siegeLane.IsDefended();
                        }

                        if (flag || Formation.QuerySystem.MedianPosition.GetNavMeshVec3().DistanceSquared(_wallSegment.MiddleFrame.Origin.GetNavMeshVec3()) < Formation.Depth * Formation.Depth)
                        {
                            return BehaviorState.TakeControl;
                        }

                        return BehaviorState.ClimbWall;
                    }
                    
                case BehaviorState.TakeControl:
                    if (Formation.QuerySystem.ClosestEnemyFormation != null)
                    {
                        if (!TeamAISiegeComponent.SiegeLanes.FirstOrDefault((SiegeLane sl) => sl.LaneSide == _behaviorSide).IsDefended())
                        {
                            if (!_teamAISiegeComponent.OuterGate.IsGateOpen || !_teamAISiegeComponent.InnerGate.IsGateOpen)
                            {
                                return BehaviorState.MoveToGate;
                            }

                            return BehaviorState.Charging;
                        }

                        return BehaviorState.TakeControl;
                    }

                    return BehaviorState.Deciding;
                    
                case BehaviorState.AttackEntity:
                    if (_teamAISiegeComponent.OuterGate.IsGateOpen && _teamAISiegeComponent.InnerGate.IsGateOpen)
                    {
                        return BehaviorState.Charging;
                    }

                    return BehaviorState.AttackEntity;
                    
                case BehaviorState.MoveToGate:
                    if (_teamAISiegeComponent.OuterGate.IsGateOpen && _teamAISiegeComponent.InnerGate.IsGateOpen)
                    {
                        return BehaviorState.Charging;
                    }

                    return BehaviorState.MoveToGate;
                    
                case BehaviorState.Charging:
                    if ((!_isGateLane || !_teamAISiegeComponent.OuterGate.IsGateOpen || !_teamAISiegeComponent.InnerGate.IsGateOpen) && _behaviorSide < FormationAI.BehaviorSide.BehaviorSideNotSet)
                    {
                        if (!TeamAISiegeComponent.SiegeLanes[(int)_behaviorSide].IsOpen && !TeamAISiegeComponent.IsFormationInsideCastle(Formation, includeOnlyPositionedUnits: true))
                        {
                            return BehaviorState.Deciding;
                        }

                        if (Formation.QuerySystem.ClosestEnemyFormation == null)
                        {
                            return BehaviorState.Stop;
                        }
                    }

                    return BehaviorState.Charging;
                    
                default:
                    if (Formation.QuerySystem.ClosestEnemyFormation != null)
                    {
                        return BehaviorState.Deciding;
                    }

                    return BehaviorState.Stop;
            }
        }

        protected override void CalculateCurrentOrder()
        {
            switch (_behaviorState)
            {
                case BehaviorState.Deciding:
                    CurrentOrder = _stopOrder;
                    break;
                    
                case BehaviorState.ClimbWall:
                    CurrentOrder = _wallSegmentMoveOrder;
                    CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(-_wallSegment.MiddleFrame.Rotation.f.AsVec2.Normalized());
                    CurrentArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    break;
                    
                case BehaviorState.TakeControl:
                    if (Formation.QuerySystem.ClosestEnemyFormation != null)
                    {
                        CurrentOrder = MovementOrder.MovementOrderChargeToTarget(Formation.QuerySystem.ClosestEnemyFormation.Formation);
                    }
                    else
                    {
                        CurrentOrder = MovementOrder.MovementOrderCharge;
                    }

                    CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(-_wallSegment.MiddleFrame.Rotation.f.AsVec2.Normalized());
                    CurrentArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    break;
                    
                case BehaviorState.AttackEntity:
                    if (!_teamAISiegeComponent.OuterGate.IsGateOpen)
                    {
                        CurrentOrder = _attackEntityOrderOuterGate;
                    }
                    else
                    {
                        CurrentOrder = _attackEntityOrderInnerGate;
                    }

                    CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
                    CurrentArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    break;
                    
                case BehaviorState.MoveToGate:
                    CurrentOrder = _castleGateMoveOrder;
                    CurrentFacingOrder = FacingOrder.FacingOrderLookAtDirection(-_innerGate.MiddleFrame.Rotation.f.AsVec2.Normalized());
                    CurrentArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    break;
                    
                case BehaviorState.Charging:
                    CurrentOrder = _chargeOrder;
                    CurrentFacingOrder = FacingOrder.FacingOrderLookAtEnemy;
                    CurrentArrangementOrder = ArrangementOrder.ArrangementOrderLoose;
                    break;
                    
                case BehaviorState.Stop:
                    CurrentOrder = _stopOrder;
                    break;
            }
        }

        public override void OnValidBehaviorSideChanged()
        {
            base.OnValidBehaviorSideChanged();
            ResetOrderPositions();
            _behaviorState = BehaviorState.Deciding;
        }

        public override void TickOccasionally()
        {
            BehaviorState behaviorState = CheckAndChangeState();
            _behaviorState = behaviorState;
            CalculateCurrentOrder();
            
            foreach (IPrimarySiegeWeapon primarySiegeWeapon in _primarySiegeWeapons)
            {
                UsableMachine usableMachine = primarySiegeWeapon as UsableMachine;
                if (usableMachine != null && !usableMachine.IsDeactivated && !primarySiegeWeapon.HasCompletedAction() && !usableMachine.IsUsedByFormation(Formation))
                {
                    Formation.StartUsingMachine(primarySiegeWeapon as UsableMachine);
                }
            }

            if (_behaviorState == BehaviorState.MoveToGate)
            {
                CastleGate innerGate = _teamAISiegeComponent.InnerGate;
                if (innerGate != null && !innerGate.IsGateOpen && !innerGate.IsDestroyed)
                {
                    if (!innerGate.IsUsedByFormation(Formation))
                    {
                        Formation.StartUsingMachine(innerGate);
                    }
                }
                else
                {
                    innerGate = _teamAISiegeComponent.OuterGate;
                    if (innerGate != null && !innerGate.IsGateOpen && !innerGate.IsDestroyed && !innerGate.IsUsedByFormation(Formation))
                    {
                        Formation.StartUsingMachine(innerGate);
                    }
                }
            }
            else
            {
                if (Formation.Detachments.Contains(_teamAISiegeComponent.OuterGate))
                {
                    Formation.StopUsingMachine(_teamAISiegeComponent.OuterGate);
                }

                if (Formation.Detachments.Contains(_teamAISiegeComponent.InnerGate))
                {
                    Formation.StopUsingMachine(_teamAISiegeComponent.InnerGate);
                }
            }

            Formation.SetMovementOrder(CurrentOrder);
            Formation.FacingOrder = CurrentFacingOrder;
            Formation.ArrangementOrder = CurrentArrangementOrder;
        }

        protected override void OnBehaviorActivatedAux()
        {
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            Formation.FacingOrder = CurrentFacingOrder;
            Formation.FiringOrder = FiringOrder.FiringOrderHoldYourFire;
            Formation.FormOrder = FormOrder.FormOrderDeep;
        }

        protected override float GetAiWeight()
        {
            float result = 0f;
            if (_teamAISiegeComponent != null)
            {
                if (IbsSettings.Instance.IsEnabled && 
                    IbsSettings.Instance.EnableAttackGates && 
                    _teamAISiegeComponent.OuterGate.DefenseSide == _behaviorSide && 
                    IbsBattleCheck.IsCurrentBatteringRamDestroyed())
                {
                    return 0.75f;
                }
                
                if (_primarySiegeWeapons.Any((IPrimarySiegeWeapon psw) => psw.HasCompletedAction()) || _wallSegment != null)
                {
                    result = ((!_teamAISiegeComponent.IsCastleBreached()) ? 0.25f : 0.75f);
                }
                else if (_teamAISiegeComponent.OuterGate.DefenseSide == _behaviorSide)
                {
                    result = 0.1f;
                }
            }

            return result;
        }
    }
} 