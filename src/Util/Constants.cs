using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace IronBloodSiege.Util
{
    public static class Constants
    {
        // 颜色常量
        public static readonly Color WarningColor = Color.FromUint(0xFFFF00FF);
        public static readonly Color ErrorColor = Color.FromUint(0xFF0000FF);
        public static readonly Color InfoColor = Color.FromUint(0x00FF00FF);

        // 消息模板
        public static readonly TextObject MoraleBoostMessage = new TextObject("{=ibs_morale_boost}IronBlood Siege: {COUNT} Atk troops were inspired");
        public static readonly TextObject PreventRetreatMessage = new TextObject("{=ibs_prevent_retreat}IronBlood Siege: Prevented troops from retreating!");
        public static readonly TextObject RetreatMessage = new TextObject("{=ibs_retreat_message}IronBlood Siege: Insufficient attacking forces, iron will disabled");
        public static readonly TextObject ErrorMessage = new TextObject("{=ibs_error_general}IronBlood Siege {CONTEXT} error: {MESSAGE}");
        public static readonly TextObject ReinforcementMessage = new TextObject("{=ibs_reinforcement_message}IronBlood Siege: Reinforcements have arrived, iron will attack resumed!");

        // 时间常量
        public const float MORALE_UPDATE_INTERVAL = 1f; // 士气更新间隔
        public const float MESSAGE_COOLDOWN = 10f;      // 消息冷却时间
        public const float RETREAT_MESSAGE_COOLDOWN = 25f; // 撤退消息冷却时间
        public const float BATTLE_START_GRACE_PERIOD = 30f; // 战斗开始缓冲期

    }
} 