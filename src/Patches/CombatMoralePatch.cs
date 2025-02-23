using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Util;
using TaleWorlds.Core;


namespace CombatMoralePatch
{
    [HarmonyPatch(typeof(CommonAIComponent))]
    public static class CommonAIPatch
    {
        [HarmonyPatch(nameof(CommonAIComponent.Morale), MethodType.Getter)]
        [HarmonyPostfix]
        public static void PostfixMoraleGetter(CommonAIComponent __instance, ref float __result)
        {
            float oldValue = __result;
            __result = 100f;
            Logger.LogDebug("士气系统", $"获取士气值: {oldValue} -> {__result}");
        }

        [HarmonyPatch(nameof(CommonAIComponent.Morale), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool PrefixMoraleSetter(CommonAIComponent __instance, ref float value)
        {
            float oldValue = value;
            value = TaleWorlds.Library.MBMath.ClampFloat(100f, 0f, 100f);
            Logger.LogDebug("士气系统", $"设置士气值: {oldValue} -> {value}");
            return true;
        }

        [HarmonyPatch("Panic")]
        [HarmonyPrefix]
        public static bool PrefixPanic(CommonAIComponent __instance)
        {
            Logger.LogDebug("士气系统", "尝试触发Panic被阻止");
            return false;
        }

        // [HarmonyPatch("OnTickAsAI")]
        // [HarmonyPrefix]
        // public static bool PrefixOnTickAsAI(CommonAIComponent __instance)
        // {
        //     if (__instance.IsRetreating)
        //     {
        //         Logger.LogDebug("士气系统", "检测到单位正在撤退");
        //     }
            
        //     // 保持士气始终为100
        //     __instance.Morale = 100f;
            
        //     return true; // 允许执行其他非士气相关的AI逻辑
        // }

        [HarmonyPatch("CanPanic")]
        [HarmonyPrefix]
        public static bool PrefixCanPanic(CommonAIComponent __instance, ref bool __result)
        {
            Logger.LogDebug("士气系统", "检查是否可以Panic");
            __result = false;
            return false;
        }

        [HarmonyPatch("InitializeMorale")]
        [HarmonyPrefix]
        public static bool PrefixInitializeMorale(CommonAIComponent __instance)
        {
            Logger.LogDebug("士气系统", "初始化士气值");
            __instance.Morale = 100f;
            return false;
        }

        [HarmonyPatch("Retreat")]
        [HarmonyPrefix]
        public static bool PrefixRetreat(CommonAIComponent __instance, bool useCachingSystem)
        {
            Logger.LogDebug("士气系统", "尝试触发撤退被阻止");
            return false;
        }
    }

    [HarmonyPatch(typeof(SandboxBattleMoraleModel))]
    public static class CombatMoralePatch
    {
    //     // <summary>
    //     // 计算恐慌士气
    //     // </summary>
    [HarmonyPatch("CanPanicDueToMorale")]
    [HarmonyPostfix]
        public static void PostfixCanPanic(Agent agent, ref bool __result)
        {
            try
            {
                if (agent?.Team != null && agent.Team.IsEnemyOf(agent.Mission.PlayerTeam))
                {
                    // 阻止敌方士兵因士气低落而恐慌
                    __result = false;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("士气系统", e);
            }
        }

        // <summary>
        // 计算士气变化
        // </summary>
        [HarmonyPatch("CalculateMoraleChangeToCharacter")]
        [HarmonyPostfix]
        public static void PostfixCalculateMorale(Agent agent, float maxMoraleChange, ref float __result)
        {
            try
            {
                if (agent?.Team != null && agent.Team.IsEnemyOf(agent.Mission.PlayerTeam))
                {
                    __result = 0f;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("士气系统", e);
            }
        }

        // <summary>
        // 计算伤亡因素
        // </summary>
        [HarmonyPatch("CalculateCasualtiesFactor")]
        [HarmonyPostfix]
        public static void PostfixCalculateCasualtiesFactor(BattleSideEnum battleSide, ref float __result)
        {
            try
            {
                bool flag = Mission.Current.PlayerTeam.Side != battleSide;
                if (flag)
                {
                    __result = 0f;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("士气系统", ex);
            }
        }

        // // <summary>
        // // 计算最大士气变化
        // // </summary>
        [HarmonyPatch("CalculateMaxMoraleChangeDueToAgentIncapacitated")]
        [HarmonyPrefix]
        public static bool Prefix(Agent affectedAgent, AgentState affectedAgentState, Agent affectorAgent, in KillingBlow killingBlow, ref ValueTuple<float, float> __result)
        {
            bool result;
            try
            {
                bool flag = Mission.Current.PlayerTeam.Side != affectedAgent.Team.Side;
                if (flag)
                {
                    __result = new ValueTuple<float, float>(0f, 0f);
                    result = false;
                }
                else
                {
                    result = true;
                }
            }
            catch (Exception e)
            {
                Logger.LogError("士气系统", e);
                result = true;
            }
            return result;
        }

        [HarmonyPatch("GetEffectiveInitialMorale")]
        [HarmonyPrefix]
        public static bool PrefixGetMorale(Agent agent, float baseMorale, ref float __result)
        {
            if (agent?.Team != null && agent.Team.IsEnemyOf(agent.Mission.PlayerTeam))
            {
                Logger.LogDebug("士气系统", $"获取初始士气: {agent.Name}, 基础值: {baseMorale}");
                __result = 100f;
                return false;
            }
            return true;
        }

        [HarmonyPatch("CalculateMaxMoraleChangeDueToAgentPanicked")]
        [HarmonyPrefix]
        public static bool PrefixCalculatePanicMorale(Agent agent, ref ValueTuple<float, float> __result)
        {
            if (agent?.Team != null && agent.Team.IsEnemyOf(agent.Mission.PlayerTeam))
            {
                Logger.LogDebug("士气系统", $"计算恐慌士气变化: {agent.Name}");
                __result = new ValueTuple<float, float>(0f, 0f);
                return false;
            }
            return true;
        }

        [HarmonyPatch("GetAverageMorale")]
        [HarmonyPrefix]
        public static bool PrefixGetAverageMorale(Formation formation, ref float __result)
        {   
            if (formation?.Team != null)
            {
                Logger.LogDebug("士气系统", "获取编队平均士气");
                __result = 100f;
                return false;
            }
            return true;
        }
    }
}
