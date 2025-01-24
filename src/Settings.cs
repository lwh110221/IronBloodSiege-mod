using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace IronBloodSiege
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "IronBloodSiege_v1";
        public override string DisplayName => new TextObject("{=ibs_mod_name}铁血攻城").ToString();
        public override string FolderName => "IronBloodSiege";
        public override string FormatType => "json";
        
        public new static Settings Instance => AttributeGlobalSettings<Settings>.Instance;

        [SettingPropertyBool("{=ibs_enable_mod}启用Mod", RequireRestart = false, 
            HintText = "{=ibs_enable_mod_hint}是否启用铁血攻城 -- tip：作者只是个学生，不喜勿喷", Order = 0)]
        [SettingPropertyGroup("{=ibs_settings_basic}基础设置", GroupOrder = 0)]
        public bool IsEnabled { get; set; } = true;

        [SettingPropertyBool("{=ibs_fixed_retreat}固定数量不再铁血进攻", RequireRestart = false, 
            HintText = "{=ibs_fixed_retreat_hint}当进攻方士兵数量低于指定值时，不再铁血进攻", Order = 0)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}不再铁血触发条件（注意：两种方式只能任选其一！）", GroupOrder = 1)]
        public bool EnableFixedRetreat { get; set; } = false;

        [SettingPropertyInteger("{=ibs_retreat_threshold}固定数量触发阈值", 10, 500, "0", RequireRestart = false, 
            HintText = "{=ibs_retreat_threshold_hint}当进攻方士兵数量低于此值时，不再铁血进攻", Order = 1)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}不再铁血触发条件（注意：两种方式只能任选其一！）", GroupOrder = 1)]
        public int RetreatThreshold { get; set; } = 100;

        [SettingPropertyBool("{=ibs_ratio_retreat}自动比例不再铁血进攻", RequireRestart = false, 
            HintText = "{=ibs_ratio_retreat_hint}当进攻方士兵数量低于守军的70%时，不再铁血进攻", Order = 2)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}不再铁血触发条件（注意：两种方式只能任选其一！）", GroupOrder = 1)]
        public bool EnableRatioRetreat { get; set; } = false;

        [SettingPropertyFloatingInteger("{=ibs_morale_threshold}攻城士兵士气阈值", 20f, 80f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_threshold_hint}当攻城士兵士气低于此值时会被提升 默认 70", Order = 0)]
        [SettingPropertyGroup("{=ibs_combat_settings}战斗设置", GroupOrder = 2)]
        public float MoraleThreshold { get; set; } = 70f;

        [SettingPropertyFloatingInteger("{=ibs_morale_boost_rate}士气提升速率", 5f, 30f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_boost_rate_hint}攻城士兵每次提升士气的幅度 默认15", Order = 1)]
        [SettingPropertyGroup("{=ibs_combat_settings}战斗设置", GroupOrder = 2)]
        public float MoraleBoostRate { get; set; } = 15f;

        public Settings()
        {
            IsEnabled = true;
            EnableFixedRetreat = false;
            RetreatThreshold = 100;
            EnableRatioRetreat = false;
            MoraleThreshold = 70f;
            MoraleBoostRate = 15f;
        }
    }
} 