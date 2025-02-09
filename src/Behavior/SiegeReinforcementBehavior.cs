using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using IronBloodSiege.Setting;

namespace IronBloodSiege.Behavior
{
    public class SiegeReinforcementBehavior : MissionBehavior, IMissionAgentSpawnLogic, IModBehavior
    {
        #region Fields
        private bool _isDisabled = false;        // 是否禁用
        private bool _isCleanedUp = false;       // 清理标记
        
        // 缓存Mission.Current以减少访问次数
        private Mission _currentMission;
        
        // 缓存攻城方Team引用
        private Team _attackerTeam;
        
        private bool _isSiegeScene = false;      // 是否是攻城场景
        private float _nextSpawnTime = 0f;       // 下次生成援军的时间
        private float _lastCheckTime = 0f;       // 最后一次检查时间
        private bool _isSpawnerEnabled = true;   // 标记是否启用援军修改
        private float _battleStartTime = 0f;     // 战斗开始时间
        private bool _wasEnabledBefore = false;  // 添加字段跟踪之前的启用状态

        // 援军生成相关
        private const float SPAWN_INTERVAL = 10f;               // 援军生成的时间间隔
        private const float SPAWN_CHECK_INTERVAL = 5f;         // 检查是否生成援军的时间间隔
        // private const int SPAWN_BATCH_SIZE = 50;               // 每次生成援军的数量
        private const int MAX_TOTAL_ATTACKERS = 999999999;     // 攻击方最大数量限制
        
        // 援军统计
        private int _initialAttackerCount = 0;    // 初始攻击方数量
        private int _reinforcementCount = 0;      // 已生成的援军数量

        private bool? _cachedSceneValid = null;
        private float _lastSceneCheckTime = 0f;
        private const float SCENE_CHECK_INTERVAL = 5f;
        #endregion

        #region Properties
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        #endregion

        #region Lifecycle Methods
        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            try
            {
                // 记录初始启用状态
                _wasEnabledBefore = Settings.Instance.IsEnabled;
                if (!_wasEnabledBefore)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "Mod未启用，跳过初始化");
                    #endif
                    return;
                }

                _battleStartTime = Mission.Current?.CurrentTime ?? 0f;
                _currentMission = Mission.Current;
                _attackerTeam = _currentMission?.AttackerTeam;
                _nextSpawnTime = _battleStartTime;
                _isSpawnerEnabled = true;

                // 等待场景检查完全完成
                int checkCount = 0;
                const int MAX_WAIT_CHECKS = 3;
                bool isValidScene = false;

                while (checkCount < MAX_WAIT_CHECKS)
                {
                    isValidScene = SafetyChecks.IsSiegeSceneValid();
                    if (isValidScene)
                    {
                        break;
                    }
                    checkCount++;
                    System.Threading.Thread.Sleep(100); 
                }

                _isSiegeScene = isValidScene;

                // 如果最终确认不是攻城战场景，才禁用
                if (!_isSiegeScene)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", "非攻城战场景，禁用援军生成器");
                    #endif
                    OnModDisabled();
                    return;
                }

                #if DEBUG
                Util.Logger.LogDebug("初始化", 
                    $"援军生成器初始化 - 场景: {_isSiegeScene}, " +
                    $"攻击方Team: {_attackerTeam != null}, " +
                    $"激进援军设置: {Settings.Instance.EnableAggressiveReinforcement}, " +
                    $"玩家是攻方: {SafetyChecks.IsPlayerAttacker()}, " +
                    $"玩家攻方启用: {Settings.Instance.EnableWhenPlayerAttacker}");
                #endif

                // 获取并检查生成逻辑组件
                var spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
                if (spawnLogic != null)
                {
                    #if DEBUG
                    Util.Logger.LogDebug("初始化", 
                        $"生成逻辑组件状态 - " +
                        $"战场大小: {spawnLogic.BattleSize}, " +
                        $"当前攻方数量: {spawnLogic.NumberOfActiveAttackerTroops}, " +
                        $"剩余可生成: {spawnLogic.NumberOfRemainingAttackerTroops}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                HandleError("初始化", ex);
            }
        }

        public override void OnMissionTick(float dt)
        {
            try
            {
                // 检查mod是否被禁用
                if (_isDisabled)
                {
                    return;
                }

                // 检查总开关状态变化
                bool isCurrentlyEnabled = Settings.Instance.IsEnabled;
                if (_wasEnabledBefore != isCurrentlyEnabled)
                {
                    if (!isCurrentlyEnabled)
                    {
                        OnModDisabled();
                    }
                    else
                    {
                        OnModEnabled();
                    }
                    _wasEnabledBefore = isCurrentlyEnabled;
                }

                // 如果mod未启用或任务无效，直接返回
                if (!isCurrentlyEnabled || !SafetyChecks.IsMissionValid())
                {
                    return;
                }

                ProcessReinforcements(dt);
            }
            catch (Exception ex)
            {
                HandleError("任务更新", ex);
            }
        }

        private bool CheckIfSiegeScene()
        {
            try
            {
                if (_currentMission == null) return false;
                
                float currentTime = _currentMission.CurrentTime;
                if (_cachedSceneValid.HasValue && currentTime - _lastSceneCheckTime < SCENE_CHECK_INTERVAL)
                {
                    return _cachedSceneValid.Value;
                }
                
                _lastSceneCheckTime = currentTime;
                _cachedSceneValid = SafetyChecks.IsSiegeSceneValid();
                return _cachedSceneValid.Value;
            }
            catch (Exception ex)
            {
                HandleError("场景检查", ex);
                return false;
            }
        }

        public override void OnClearScene()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("场景清理", "开始清理场景");
                #endif
                OnModDisabled();
                base.OnClearScene();
            }
            catch (Exception ex)
            {
                HandleError("场景清理", ex);
            }
        }

        protected override void OnEndMission()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("任务结束", "开始任务结束清理");
                #endif

                // 重置所有状态
                _isDisabled = true;
                _isSiegeScene = false;
                _isSpawnerEnabled = false;
                _nextSpawnTime = 0f;
                _lastCheckTime = 0f;
                _battleStartTime = 0f;
                _currentMission = null;
                _attackerTeam = null;

                base.OnEndMission();
            }
            catch (Exception ex)
            {
                HandleError("任务结束", ex);
                base.OnEndMission();
            }
        }

        public override void OnRemoveBehavior()
        {
            try
            {
                #if DEBUG
                Util.Logger.LogDebug("移除行为", "开始移除行为");
                #endif

                // 确保所有状态被重置
                OnModDisabled();
                _currentMission = null;
                _attackerTeam = null;
                _isSiegeScene = false;
                _isSpawnerEnabled = false;
                _nextSpawnTime = 0f;
                _lastCheckTime = 0f;
                _battleStartTime = 0f;

                base.OnRemoveBehavior();
            }
            catch (Exception ex)
            {
                HandleError("移除行为", ex);
                base.OnRemoveBehavior();
            }
        }
        #endregion

        #region Core Logic
        private void ProcessReinforcements(float dt)
        {
            try
            {
                // 1. 缓存当前时间，避免多次访问
                float currentTime = _currentMission?.CurrentTime ?? 0f;
                
                // 2. 首先检查时间间隔，这是最快的检查
                if (currentTime - _lastCheckTime < SPAWN_CHECK_INTERVAL)
                {
                    return;
                }

                // 3. 更新最后检查时间
                _lastCheckTime = currentTime;

                // 4. 状态检查（只在间隔时间到达后才检查）
                if (_isDisabled || !Settings.Instance.IsEnabled || !Settings.Instance.EnableAggressiveReinforcement)
                {
                    return;
                }

                // 5. 检查生成间隔
                if (currentTime < _nextSpawnTime)
                {
                    return;
                }

                // 6. 获取并缓存生成逻辑组件
                var spawnLogic = _currentMission?.GetMissionBehavior<MissionAgentSpawnLogic>();
                if (spawnLogic == null) return;

                // 7. 检查是否需要禁用
                if (spawnLogic.NumberOfRemainingAttackerTroops <= 0)
                {
                    OnModDisabled();
                    return;
                }

                // 8. 只在需要时执行生成
                if (!spawnLogic.IsSideSpawnEnabled(BattleSideEnum.Attacker))
                {
                    spawnLogic.SetSpawnTroops(BattleSideEnum.Attacker, true, true);
                    _nextSpawnTime = currentTime + SPAWN_INTERVAL;
                }
            }
            catch (Exception ex)
            {
                HandleError("援军生成处理", ex);
                _isDisabled = true;
            }
        }
        #endregion

        #region IMissionAgentSpawnLogic Implementation
        public void StartSpawner(BattleSideEnum side)
        {
            if (!Settings.Instance.IsEnabled) return;
            
            if (side == BattleSideEnum.Attacker)
            {
                _isSpawnerEnabled = true;
                var spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
                if (spawnLogic != null)
                {
                    spawnLogic.SetSpawnTroops(side, true, true);
                }
            }
        }

        public void StopSpawner(BattleSideEnum side)
        {
            if (!Settings.Instance.IsEnabled) return;
            
            if (side == BattleSideEnum.Attacker)
            {
                _isSpawnerEnabled = false;
                var spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
                if (spawnLogic != null)
                {
                    spawnLogic.SetSpawnTroops(side, false);
                }
            }
        }

        public bool IsSideSpawnEnabled(BattleSideEnum side)
        {
            if (!Settings.Instance.IsEnabled) return false;
            
            var spawnLogic = Mission.Current?.GetMissionBehavior<MissionAgentSpawnLogic>();
            if (spawnLogic != null && side == BattleSideEnum.Attacker)
            {
                return spawnLogic.IsSideSpawnEnabled(side);
            }
            return false;
        }

        public bool IsSideDepleted(BattleSideEnum side)
        {
            if (!Settings.Instance.IsEnabled) return false;
            
            if (side == BattleSideEnum.Attacker)
            {
                int currentAttackerCount = SafetyChecks.GetAttackerCount(_attackerTeam);
                return currentAttackerCount >= MAX_TOTAL_ATTACKERS;
            }
            return false;
        }

        public float GetReinforcementInterval()
        {
            return SPAWN_INTERVAL;
        }
        #endregion

        public void OnModEnabled()
        {
            try
            {
                _isDisabled = false;
                _isSpawnerEnabled = true;
                
                // 重置生成时间
                _nextSpawnTime = Mission.Current?.CurrentTime ?? 0f;
                
                #if DEBUG
                Util.Logger.LogDebug("启用Mod", "援军生成器已启用");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("mod enabled", ex);
            }
        }

        public void OnModDisabled()
        {
            try
            {
                if (_isDisabled) return;
                
                _isDisabled = true;
                _isSpawnerEnabled = false;
                
                // 安全获取Mission引用
                var mission = BehaviorCleanupHelper.GetSafeMission(_currentMission);
                if (mission != null)
                {
                    // 停止生成器
                    var spawnLogic = mission.GetMissionBehavior<MissionAgentSpawnLogic>();
                    if (spawnLogic != null)
                    {
                        spawnLogic.StopSpawner(BattleSideEnum.Attacker);
                    }
                }
                
                #if DEBUG
                Util.Logger.LogDebug("禁用Mod", "援军生成器已停止");
                #endif
            }
            catch (Exception ex)
            {
                HandleError("mod disabled", ex);
            }
        }

        public void OnSceneChanged()
        {
            _isSiegeScene = false;
            _isSpawnerEnabled = false;
            _cachedSceneValid = null;
            _lastSceneCheckTime = 0f;
        }

        private void HandleError(string context, Exception ex)
        {
            try
            {
                #if DEBUG
                Util.Logger.LogError(context, ex);
                #endif
                
                Constants.ErrorMessage
                    .SetTextVariable("CONTEXT", context)
                    .SetTextVariable("MESSAGE", ex.Message);
                    
                InformationManager.DisplayMessage(new InformationMessage(
                    Constants.ErrorMessage.ToString(),
                    Constants.ErrorColor));
            }
            catch
            {
                #if DEBUG
                try
                {
                    Util.Logger.LogError("error handler", new Exception("Failed to handle error: " + context));
                }
                catch { }
                #endif
            }
        }
    }
} 