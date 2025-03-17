using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace IronBloodSiege.Util
{
    /// <summary>
    /// 攻城战状态检查工具类
    /// </summary>
    public static class IbsBattleCheck
    {
        // 缓存检查结果
        private static bool? _cachedIsSiegeSceneResult = null;
        private static string _lastCheckedSceneName = null;
        private static Mission _lastCheckedMission = null;  
        private static float _lastCheckTime = 0f;
        private const float CACHE_TIMEOUT = 5f;
        
        /// <summary>
        /// 清除缓存，强制重新检查
        /// </summary>
        public static void ClearCache()
        {
            _cachedIsSiegeSceneResult = null;
            _lastCheckedSceneName = null;
            _lastCheckTime = 0f;
            _lastCheckedMission = null;
        }
        
        /// <summary>
        /// 判断当前是否为攻城战场景
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果是攻城战返回true，否则返回false</returns>
        public static bool IsSiegeScene(Mission mission)
        {
            bool isMissionNull = mission == null;
            if (isMissionNull) return false;
            
            float currentTime = mission.CurrentTime;
            bool isCacheValid = _lastCheckedMission == mission && 
                              _cachedIsSiegeSceneResult.HasValue && 
                              currentTime - _lastCheckTime < CACHE_TIMEOUT;
                              
            if (isCacheValid)
            {
                return _cachedIsSiegeSceneResult.Value;
            }
            
            _lastCheckTime = currentTime;
            _lastCheckedMission = mission;
            _lastCheckedSceneName = mission.SceneName;
            
            bool isSiegeSceneBasic = mission.IsSiegeBattle && !mission.IsSallyOutBattle && !mission.IsFieldBattle;
            
            bool hasValidTeams = mission.AttackerTeam != null && mission.DefenderTeam != null;
            bool hasActiveAgents = hasValidTeams && 
                                 mission.AttackerTeam.ActiveAgents.Count > 0 && 
                                 mission.DefenderTeam.ActiveAgents.Count > 0;
            bool isValid = isSiegeSceneBasic && hasValidTeams && hasActiveAgents;
            
            _cachedIsSiegeSceneResult = isValid;
            return isValid;
        }
        
        /// <summary>
        /// 判断当前任务是否为攻城战场景
        /// </summary>
        /// <returns>如果是攻城战返回true，否则返回false</returns>
        public static bool IsCurrentSiegeScene()
        {
            return IsSiegeScene(Mission.Current);
        }
        
        /// <summary>
        /// 检查玩家是否为攻城方
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>玩家是否为攻城方</returns>
        public static bool IsPlayerAttacker(Mission mission)
        {
            bool hasValidTeams = mission?.PlayerTeam != null && mission.AttackerTeam != null;
            if (!hasValidTeams) return false;
            
            return mission.PlayerTeam == mission.AttackerTeam;
        }
        
        /// <summary>
        /// 检查当前玩家是否为攻城方
        /// </summary>
        /// <returns>当前玩家是否为攻城方</returns>
        public static bool IsCurrentPlayerAttacker()
        {
            return IsPlayerAttacker(Mission.Current);
        }
        
        /// <summary>
        /// 获取攻城战的TeamAI组件
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>攻城战TeamAI组件，如果不存在则返回null</returns>
        public static TeamAISiegeComponent GetSiegeTeamAI(Mission mission)
        {
            bool isMissionNull = mission == null || mission.AttackerTeam == null;
            if (isMissionNull) return null;
            
            return mission.AttackerTeam.TeamAI as TeamAISiegeComponent;
        }
        
        /// <summary>
        /// 获取攻城战的查询系统
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>攻城战查询系统，如果不存在则返回null</returns>
        public static SiegeQuerySystem GetSiegeQuerySystem(Mission mission)
        {
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return null;
            
            // 通过反射获取查询系统，因为QuerySystem是静态属性
            var querySystem = typeof(TeamAISiegeComponent).GetProperty("QuerySystem")?.GetValue(null) as SiegeQuerySystem;
            return querySystem;
        }
        
        /// <summary>
        /// 获取场景内城门信息
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回城门列表，如果没有找到则返回空列表</returns>
        public static List<CastleGate> GetSiegeGates(Mission mission)
        {
            List<CastleGate> gates = new List<CastleGate>();
            
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return gates;
            
            bool hasOuterGate = teamAI.OuterGate != null;
            if (hasOuterGate)
            {
                gates.Add(teamAI.OuterGate);
            }
            
            bool hasInnerGate = teamAI.InnerGate != null;
            if (hasInnerGate) 
            {
                gates.Add(teamAI.InnerGate);
            }
            
            return gates;
        }
        
        /// <summary>
        /// 获取当前场景内城门信息
        /// </summary>
        /// <returns>返回城门列表，如果没有找到则返回空列表</returns>
        public static List<CastleGate> GetCurrentSiegeGates()
        {
            return GetSiegeGates(Mission.Current);
        }
        
        /// <summary>
        /// 获取未摧毁的城门列表
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回未摧毁的城门列表</returns>
        public static List<CastleGate> GetIntactGates(Mission mission)
        {
            var gates = GetSiegeGates(mission);
            return gates.Where(gate => !gate.IsDestroyed && gate.State == CastleGate.GateState.Closed).ToList();
        }
        
        /// <summary>
        /// 检查城门是否已全部摧毁或打开
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果所有城门都已被摧毁或打开返回true，否则返回false</returns>
        public static bool AreGatesDestroyed(Mission mission)
        {
            var gates = GetSiegeGates(mission);
            bool hasNoGates = gates.Count == 0;
            if (hasNoGates) return false;
            
            return gates.All(gate => gate.IsDestroyed || gate.State == CastleGate.GateState.Open);
        }
        
        /// <summary>
        /// 检查当前场景城门是否已全部摧毁或打开
        /// </summary>
        /// <returns>如果所有城门都已被摧毁或打开返回true，否则返回false</returns>
        public static bool AreCurrentGatesDestroyed()
        {
            return AreGatesDestroyed(Mission.Current);
        }
        
        /// <summary>
        /// 获取最近的城门
        /// </summary>
        /// <param name="position">参考位置</param>
        /// <param name="mission">当前任务实例</param>
        /// <returns>最近的城门，如果没有城门则返回null</returns>
        public static CastleGate GetNearestGate(Vec2 position, Mission mission)
        {
            var gates = GetIntactGates(mission);
            bool hasNoGates = gates.Count == 0;
            if (hasNoGates) return null;
            
            return gates.OrderBy(g => position.DistanceSquared(g.GameEntity.GlobalPosition.AsVec2)).FirstOrDefault();
        }
        
        /// <summary>
        /// 获取所有攻城器械
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>攻城器械列表，如果没有则返回空列表</returns>
        public static List<SiegeWeapon> GetSiegeWeapons(Mission mission)
        {
            var result = new List<SiegeWeapon>();
            
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return result;
            
            bool hasPrimarySiegeWeapons = teamAI.PrimarySiegeWeapons != null;
            if (hasPrimarySiegeWeapons)
            {
                result.AddRange(teamAI.PrimarySiegeWeapons.Select(w => w as SiegeWeapon));
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取当前场景所有攻城器械
        /// </summary>
        /// <returns>攻城器械列表，如果没有则返回空列表</returns>
        public static List<SiegeWeapon> GetCurrentSiegeWeapons()
        {
            return GetSiegeWeapons(Mission.Current);
        }
        
        /// <summary>
        /// 获取特定类型的攻城器械
        /// </summary>
        /// <typeparam name="T">攻城器械类型</typeparam>
        /// <param name="mission">当前任务实例</param>
        /// <returns>指定类型的攻城器械列表</returns>
        public static List<T> GetSiegeWeaponsByType<T>(Mission mission) where T : SiegeWeapon
        {
            var weapons = GetSiegeWeapons(mission);
            return weapons.OfType<T>().ToList();
        }
        
        /// <summary>
        /// 获取当前场景特定类型的攻城器械
        /// </summary>
        /// <typeparam name="T">攻城器械类型</typeparam>
        /// <returns>指定类型的攻城器械列表</returns>
        public static List<T> GetCurrentSiegeWeaponsByType<T>() where T : SiegeWeapon
        {
            return GetSiegeWeaponsByType<T>(Mission.Current);
        }
        
        /// <summary>
        /// 获取冲车
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>冲车，如果不存在则返回null</returns>
        public static BatteringRam GetBatteringRam(Mission mission)
        {
            var rams = GetSiegeWeaponsByType<BatteringRam>(mission);
            bool hasNoRams = rams.Count == 0;
            if (hasNoRams) return null;
            
            return rams.FirstOrDefault();
        }
        
        /// <summary>
        /// 获取当前场景冲车
        /// </summary>
        /// <returns>冲车，如果不存在则返回null</returns>
        public static BatteringRam GetCurrentBatteringRam()
        {
            return GetBatteringRam(Mission.Current);
        }
        
        /// <summary>
        /// 检查冲车是否已损毁或无法使用
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果冲车已损毁或无法使用返回true，否则返回false</returns>
        public static bool IsBatteringRamDestroyed(Mission mission)
        {
            var ram = GetBatteringRam(mission);
            bool isRamNull = ram == null;
            if (isRamNull) return true;
            
            bool isDestroyed = ram.IsDestroyed;
            if (isDestroyed) return true;
            
            bool hasNoDestructionComponent = ram.DestructionComponent == null;
            if (hasNoDestructionComponent) return true;
            
            bool isNoHitPoint = ram.DestructionComponent.HitPoint <= 0;
            if (isNoHitPoint) return true;
            
            bool isDeactivated = ram.IsDeactivated || ram.IsDisabled;
            
            return isDeactivated;
        }
        
        /// <summary>
        /// 检查当前场景冲车是否已损毁或无法使用
        /// </summary>
        /// <returns>如果冲车已损毁或无法使用返回true，否则返回false</returns>
        public static bool IsCurrentBatteringRamDestroyed()
        {
            return IsBatteringRamDestroyed(Mission.Current);
        }
        
        /// <summary>
        /// 获取攻城塔列表
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>攻城塔列表，如果不存在则返回空列表</returns>
        public static List<SiegeTower> GetSiegeTowers(Mission mission)
        {
            return GetSiegeWeaponsByType<SiegeTower>(mission);
        }
        
        /// <summary>
        /// 获取当前场景攻城塔列表
        /// </summary>
        /// <returns>攻城塔列表，如果不存在则返回空列表</returns>
        public static List<SiegeTower> GetCurrentSiegeTowers()
        {
            return GetSiegeTowers(Mission.Current);
        }
        
        /// <summary>
        /// 检查是否所有攻城塔都已损毁
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果所有攻城塔都已损毁返回true，否则返回false</returns>
        public static bool AreSiegeTowersDestroyed(Mission mission)
        {
            var towers = GetSiegeTowers(mission);
            bool hasNoTowers = towers.Count == 0;
            if (hasNoTowers) return true;
            
            return towers.All(tower => tower.IsDestroyed || tower.IsDeactivated || tower.IsDisabled);
        }
        
        /// <summary>
        /// 检查当前场景是否所有攻城塔都已损毁
        /// </summary>
        /// <returns>如果所有攻城塔都已损毁返回true，否则返回false</returns>
        public static bool AreCurrentSiegeTowersDestroyed()
        {
            return AreSiegeTowersDestroyed(Mission.Current);
        }
        
        /// <summary>
        /// 获取攻城梯列表
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>攻城梯列表，如果不存在则返回空列表</returns>
        public static List<SiegeLadder> GetSiegeLadders(Mission mission)
        {
            return GetSiegeWeaponsByType<SiegeLadder>(mission);
        }
        
        /// <summary>
        /// 获取当前场景攻城梯列表
        /// </summary>
        /// <returns>攻城梯列表，如果不存在则返回空列表</returns>
        public static List<SiegeLadder> GetCurrentSiegeLadders()
        {
            return GetSiegeLadders(Mission.Current);
        }
        
        /// <summary>
        /// 检查所有攻城器械状态
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果所有攻城器械都已损毁返回true，否则返回false</returns>
        public static bool AreSiegeWeaponsDestroyed(Mission mission)
        {
            bool isRamDestroyed = IsBatteringRamDestroyed(mission);
            bool areTowersDestroyed = AreSiegeTowersDestroyed(mission);
            
            return isRamDestroyed && areTowersDestroyed;
        }
        
        /// <summary>
        /// 检查当前场景所有攻城器械状态
        /// </summary>
        /// <returns>如果所有攻城器械都已损毁返回true，否则返回false</returns>
        public static bool AreCurrentSiegeWeaponsDestroyed()
        {
            return AreSiegeWeaponsDestroyed(Mission.Current);
        }
        
        /// <summary>
        /// 获取攻城战区域内的攻击方数量信息
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回一个元组，包含左翼、中央、右翼和城内的攻击方数量</returns>
        public static (int leftCount, int middleCount, int rightCount, int insideCount) GetAttackerRegionCounts(Mission mission)
        {
            var querySystem = GetSiegeQuerySystem(mission);
            bool isQuerySystemNull = querySystem == null;
            if (isQuerySystemNull) return (0, 0, 0, 0);
            
            return (querySystem.LeftRegionMemberCount,
                    querySystem.MiddleRegionMemberCount,
                    querySystem.RightRegionMemberCount,
                    querySystem.InsideAttackerCount);
        }
        
        /// <summary>
        /// 获取当前攻城战区域内的攻击方数量信息
        /// </summary>
        /// <returns>返回一个元组，包含左翼、中央、右翼和城内的攻击方数量</returns>
        public static (int leftCount, int middleCount, int rightCount, int insideCount) GetCurrentAttackerRegionCounts()
        {
            return GetAttackerRegionCounts(Mission.Current);
        }
        
        /// <summary>
        /// 获取城墙附近的攻击方数量信息
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回一个元组，包含左翼、中央、右翼墙附近的攻击方数量</returns>
        public static (int leftCloseCount, int middleCloseCount, int rightCloseCount) GetAttackerCloseToWallCounts(Mission mission)
        {
            var querySystem = GetSiegeQuerySystem(mission);
            bool isQuerySystemNull = querySystem == null;
            if (isQuerySystemNull) return (0, 0, 0);
            
            return (querySystem.LeftCloseAttackerCount,
                    querySystem.MiddleCloseAttackerCount,
                    querySystem.RightCloseAttackerCount);
        }
        
        /// <summary>
        /// 获取当前城墙附近的攻击方数量信息
        /// </summary>
        /// <returns>返回一个元组，包含左翼、中央、右翼墙附近的攻击方数量</returns>
        public static (int leftCloseCount, int middleCloseCount, int rightCloseCount) GetCurrentAttackerCloseToWallCounts()
        {
            return GetAttackerCloseToWallCounts(Mission.Current);
        }
        
        /// <summary>
        /// 获取防守方各区域的数量信息
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>返回一个元组，包含左翼、中央、右翼的防守方数量</returns>
        public static (int leftCount, int middleCount, int rightCount) GetDefenderRegionCounts(Mission mission)
        {
            var querySystem = GetSiegeQuerySystem(mission);
            bool isQuerySystemNull = querySystem == null;
            if (isQuerySystemNull) return (0, 0, 0);
            
            return (querySystem.LeftDefenderCount,
                    querySystem.MiddleDefenderCount,
                    querySystem.RightDefenderCount);
        }
        
        /// <summary>
        /// 获取当前防守方各区域的数量信息
        /// </summary>
        /// <returns>返回一个元组，包含左翼、中央、右翼的防守方数量</returns>
        public static (int leftCount, int middleCount, int rightCount) GetCurrentDefenderRegionCounts()
        {
            return GetDefenderRegionCounts(Mission.Current);
        }
        
        /// <summary>
        /// 检查攻城通道是否已被突破
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果攻城通道已被突破返回true，否则返回false</returns>
        public static bool IsCastleBreached(Mission mission)
        {
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return false;
            
            return teamAI.CalculateIsAnyLaneOpenToGetInside();
        }
        
        /// <summary>
        /// 检查当前攻城通道是否已被突破
        /// </summary>
        /// <returns>如果攻城通道已被突破返回true，否则返回false</returns>
        public static bool IsCurrentCastleBreached()
        {
            return IsCastleBreached(Mission.Current);
        }
        
        /// <summary>
        /// 检查部队是否在城堡内部
        /// </summary>
        /// <param name="formation">要检查的部队</param>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果部队在城堡内部返回true，否则返回false</returns>
        public static bool IsFormationInsideCastle(Formation formation, Mission mission)
        {
            bool isFormationNull = formation == null;
            if (isFormationNull) return false;
            
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return false;
            
            // 使用游戏内置方法判断部队是否在城内
            return TeamAISiegeComponent.IsFormationInsideCastle(formation, includeOnlyPositionedUnits: true);
        }
        
        /// <summary>
        /// 检查当前部队是否在城堡内部
        /// </summary>
        /// <param name="formation">要检查的部队</param>
        /// <returns>如果部队在城堡内部返回true，否则返回false</returns>
        public static bool IsCurrentFormationInsideCastle(Formation formation)
        {
            return IsFormationInsideCastle(formation, Mission.Current);
        }
        
        /// <summary>
        /// 检查城堡是否已被大规模突破（多数攻击方已进入城内）
        /// </summary>
        /// <param name="mission">当前任务实例</param>
        /// <returns>如果城堡已被大规模突破返回true，否则返回false</returns>
        public static bool IsCastleMassivelyBreached(Mission mission)
        {
            var teamAI = GetSiegeTeamAI(mission);
            bool isTeamAINull = teamAI == null;
            if (isTeamAINull) return false;
            
            // 使用游戏内置方法判断城堡是否已被大规模突破
            return teamAI.IsCastleBreached();
        }
        
        /// <summary>
        /// 检查当前城堡是否已被大规模突破
        /// </summary>
        /// <returns>如果城堡已被大规模突破返回true，否则返回false</returns>
        public static bool IsCurrentCastleMassivelyBreached()
        {
            return IsCastleMassivelyBreached(Mission.Current);
        }
        
        /// <summary>
        /// 获取攻城武器的当前命令状态
        /// </summary>
        /// <param name="weapon">要检查的攻城武器</param>
        /// <returns>当前的命令状态</returns>
        public static SiegeWeaponOrderType GetSiegeWeaponOrder(SiegeWeapon weapon)
        {
            bool isWeaponNull = weapon == null;
            if (isWeaponNull) return SiegeWeaponOrderType.Stop;
            
            return SiegeWeaponController.GetActiveOrderOf(weapon);
        }
        
        /// <summary>
        /// 检查攻城武器是否在强制使用状态
        /// </summary>
        /// <param name="weapon">要检查的攻城武器</param>
        /// <returns>如果攻城武器在强制使用状态返回true，否则返回false</returns>
        public static bool IsSiegeWeaponForcedUse(SiegeWeapon weapon)
        {
            bool isWeaponNull = weapon == null;
            if (isWeaponNull) return false;
            
            return weapon.ForcedUse;
        }
        
        /// <summary>
        /// 检查攻城武器是否可被选择操作
        /// </summary>
        /// <param name="weapon">要检查的攻城武器</param>
        /// <returns>如果攻城武器可被选择操作返回true，否则返回false</returns>
        public static bool IsSiegeWeaponSelectable(SiegeWeapon weapon)
        {
            bool isWeaponNull = weapon == null;
            if (isWeaponNull) return false;
            
            return SiegeWeaponController.IsWeaponSelectable(weapon);
        }
        
        /// <summary>
        /// 获取攻城武器所在的区域侧翼
        /// </summary>
        /// <param name="weapon">要检查的攻城武器</param>
        /// <returns>攻城武器所在的区域侧翼</returns>
        public static FormationAI.BehaviorSide GetSiegeWeaponSide(SiegeWeapon weapon)
        {
            bool isWeaponNull = weapon == null;
            if (isWeaponNull) return FormationAI.BehaviorSide.BehaviorSideNotSet;
            
            if (weapon is IPrimarySiegeWeapon primaryWeapon)
            {
                return primaryWeapon.WeaponSide;
            }
            
            // 远程武器通常在中央
            if (weapon is RangedSiegeWeapon)
            {
                return FormationAI.BehaviorSide.Middle;
            }
            
            return FormationAI.BehaviorSide.BehaviorSideNotSet;
        }
        
        /// <summary>
        /// 判断一个区域与指定侧翼是否相关
        /// </summary>
        /// <param name="side">要检查的侧翼</param>
        /// <param name="connectedSides">关联的侧翼</param>
        /// <returns>如果区域与指定侧翼相关返回true，否则返回false</returns>
        public static bool AreSidesRelated(FormationAI.BehaviorSide side, int connectedSides)
        {
            return SiegeQuerySystem.AreSidesRelated(side, connectedSides);
        }
    }
}