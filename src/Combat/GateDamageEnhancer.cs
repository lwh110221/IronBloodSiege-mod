using HarmonyLib;
using TaleWorlds.MountAndBlade;
using IronBloodSiege.Setting;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using IronBloodSiege.Message;

namespace IronBloodSiege.Combat
{
    [HarmonyPatch(typeof(Mission))]
    [HarmonyPatch("OnEntityHit")]
    public static class GateDamageEnhancer
    {
        private static float GateDamageMultiplier => IbsSettings.Instance.GateDamageMultiplier;

        [HarmonyPrefix]
        public static void Prefix(GameEntity entity, Agent attackerAgent, ref int inflictedDamage, DamageTypes damageType, Vec3 impactPosition, Vec3 impactDirection, in MissionWeapon weapon)
        {
            if (!IbsSettings.Instance.IsEnabled ||
                !IbsSettings.Instance.EnableOuterGateDamageEnhance ||
                attackerAgent == null ||
                inflictedDamage <= 0 ||
                entity == null)
                return;

            if (attackerAgent.Team?.Side != BattleSideEnum.Attacker) return;

            bool isOuterGate = false;

            CastleGate gate = GetCastleGateFromEntity(entity);
            if (gate != null && gate.GameEntity.HasTag("outer_gate"))
            {
                isOuterGate = true;
            }
            else
            {
                return;
            }

            if (isOuterGate)
            {
                int originalDamage = inflictedDamage;
                inflictedDamage = (int)(inflictedDamage * GateDamageMultiplier);
#if DEBUG                
                IbsMessage.ShowMessage(
                    $"{originalDamage} → {inflictedDamage}"
                , Colors.Green);
#endif
            }
        }

        private static CastleGate GetCastleGateFromEntity(GameEntity entity)
        {
            if (entity?.HasScriptOfType<CastleGate>() == true)
            {
                return entity.GetFirstScriptOfType<CastleGate>();
            }

            GameEntity current = entity;
            while (current?.Parent != null)
            {
                current = current.Parent;
                if (current.HasScriptOfType<CastleGate>())
                {
                    return current.GetFirstScriptOfType<CastleGate>();
                }
            }

            if (entity != null)
            {
                for (int i = 0; i < entity.ChildCount; i++)
                {
                    GameEntity child = entity.GetChild(i);
                    if (child.HasScriptOfType<CastleGate>())
                    {
                        return child.GetFirstScriptOfType<CastleGate>();
                    }
                }
            }

            return null;
        }
    }
}