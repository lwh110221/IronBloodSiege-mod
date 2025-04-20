using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.Localization;
using System.Linq;

namespace IronBloodSiege.Setting
{
    public class IbsSettings : AttributeGlobalSettings<IbsSettings>
    {
        public override string Id => "IronBloodSiege_v1";
        public override string DisplayName => new TextObject("{=ibs_mod_name}IronBloodSiege").ToString();
        public override string FolderName => "IronBloodSiege";
        public override string FormatType => "json";
        
        public new static IbsSettings Instance => AttributeGlobalSettings<IbsSettings>.Instance;

        [SettingPropertyBool("{=ibs_enable_mod}Enable Mod", RequireRestart = false, 
            HintText = "{=ibs_enable_mod_hint}Whether to enable Mod", Order = 0)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 1)]
        public bool IsEnabled { get; set; } = true;

        [SettingPropertyBool("{=ibs_enable_when_player_attacker}Enable When Player is Attacker", RequireRestart = false,
            HintText = "{=ibs_enable_when_player_attacker_hint}Whether to enable iron will effect when player is on the attacking side", Order = 1)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 1)]
        public bool EnableWhenPlayerAttacker { get; set; } = true;

        [SettingPropertyBool("{=ibs_enable_attack_gates}Enable Attack Gates", RequireRestart = false,
            HintText = "{=ibs_enable_attack_gates_hint}When there is no battering ram or the battering ram is destroyed, middle lane formations will attack the gates", Order = 2)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 1)]
        public bool EnableAttackGates { get; set; } = true;

        [SettingPropertyBool("{=ibs_enable_outer_gate_damage_enhance}Enable Outer Gate Damage Enhance", RequireRestart = false,
            HintText = "{=ibs_enable_outer_gate_damage_enhance_hint}Damage multiplier for soldiers attacking outer gates with weapons", Order = 3)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 1)]
        public bool EnableOuterGateDamageEnhance { get; set; } = true;

        [SettingPropertyFloatingInteger("{=ibs_retreat_dead}Casualties as a percentage of total troops", 10f, 99f, "0.0", RequireRestart = false,
            HintText = "{=ibs_retreat_dead_hint}When the number of casualties reaches this ratio of the total number of troops, the Buff effect disappears.. Default: 70", Order = 1)]
        [SettingPropertyGroup("{=ibs_retreat_settings}Buff Settings", GroupOrder = 2)]
        public float CanRetreatRatios { get; set; } = 70f;

        [SettingPropertyBool("{=ibs_show_casualty_message}Show Casualty Message", RequireRestart = false,
            HintText = "{=ibs_show_casualty_message_hint}Whether to display messages showing attacker force remaining and casualty rate", Order = 2)]
        [SettingPropertyGroup("{=ibs_retreat_settings}Buff Settings", GroupOrder = 2)]
        public bool ShowCasualtyMessage { get; set; } = true;

        [SettingPropertyBool("{=ibs_enable_spawn_balance}Enable strength adjustment", RequireRestart = false,
            HintText = "{=ibs_enable_spawn_balance_hint}Whether to enable Adjusts the in-battlefield force ratio between the attacking and defending sides in a siege", Order = 1)]
        [SettingPropertyGroup("{=ibs_spawn_basic}Strength adjustment", GroupOrder = 3)]
        public bool EnableSpawnBalance { get; set; } = true;

        [SettingPropertyInteger("{=ibs_attacker_ratio}Attacker Troops Ratio", 50, 80, RequireRestart = true,
            HintText = "{=ibs_attacker_ratio_hint}Percentage of the attacking side's initial force to the total force in a siege battle. Default value: 65", Order = 2)]
        [SettingPropertyGroup("{=ibs_spawn_basic}Strength adjustment", GroupOrder = 3)]
        public int AttackerTroopsRatio { get; set; } = 65;

        [SettingPropertyFloatingInteger("{=ibs_gate_damage_multiplier}Outer Gate Damage Multiplier", 1.0f, 10.0f, "0.0", RequireRestart = false,
            HintText = "{=ibs_gate_damage_multiplier_hint}Multiplier for damage dealt to outer gates. Default: 2.0", Order = 4)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 1)]
        public float GateDamageMultiplier { get; set; } = 2.0f;

        public IbsSettings()
        {
            IsEnabled = true;
            EnableWhenPlayerAttacker = true;
            EnableAttackGates = true;
            EnableOuterGateDamageEnhance = true;
            CanRetreatRatios = 70f;
            ShowCasualtyMessage = true;
            EnableSpawnBalance = true;
            AttackerTroopsRatio = 65;
            GateDamageMultiplier = 2.0f;
        }
    }
} 