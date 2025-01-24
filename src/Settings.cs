using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace IronBloodSiege
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "IronBloodSiege_v1";
        public override string DisplayName => new TextObject("{=ibs_mod_name}IronBlood Siege").ToString();
        public override string FolderName => "IronBloodSiege";
        public override string FormatType => "json";
        
        public new static Settings Instance => AttributeGlobalSettings<Settings>.Instance;

        [SettingPropertyBool("{=ibs_enable_mod}Enable Mod", RequireRestart = false, 
            HintText = "{=ibs_enable_mod_hint}Whether to enable IronBlood Siege - Note: Author is just a student, please be kind", Order = 0)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 0)]
        public bool IsEnabled { get; set; } = true;

        [SettingPropertyBool("{=ibs_fixed_retreat}Fixed Number Iron Will Disable", RequireRestart = false, 
            HintText = "{=ibs_fixed_retreat_hint}Disable iron will when attacker troops fall below specified number", Order = 0)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public bool EnableFixedRetreat { get; set; } = false;

        [SettingPropertyInteger("{=ibs_retreat_threshold}Fixed Number Threshold", 10, 500, "0", RequireRestart = false, 
            HintText = "{=ibs_retreat_threshold_hint}Disable iron will when attacker troops fall below this number", Order = 1)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public int RetreatThreshold { get; set; } = 100;

        [SettingPropertyBool("{=ibs_ratio_retreat}Auto Ratio Iron Will Disable", RequireRestart = false, 
            HintText = "{=ibs_ratio_retreat_hint}Disable iron will when attacker troops fall below 70% of defenders", Order = 2)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public bool EnableRatioRetreat { get; set; } = false;

        [SettingPropertyFloatingInteger("{=ibs_morale_threshold}Siege Troop Morale Threshold", 20f, 80f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_threshold_hint}Morale will be boosted when troops fall below this value. Default: 70", Order = 0)]
        [SettingPropertyGroup("{=ibs_combat_settings}Combat Settings", GroupOrder = 2)]
        public float MoraleThreshold { get; set; } = 70f;

        [SettingPropertyFloatingInteger("{=ibs_morale_boost_rate}Morale Boost Rate", 5f, 30f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_boost_rate_hint}Amount of morale boost per update. Default: 15", Order = 1)]
        [SettingPropertyGroup("{=ibs_combat_settings}Combat Settings", GroupOrder = 2)]
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