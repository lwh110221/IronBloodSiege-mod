using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.Localization;
using System.Linq;

namespace IronBloodSiege.Setting
{
    public enum GateAttackFormation
    {
        Line,
        ShieldWall,
        Square,
        Loose
    }

    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "IronBloodSiege_v1";
        public override string DisplayName => new TextObject("{=ibs_mod_name}IronBlood Siege").ToString();
        public override string FolderName => "IronBloodSiege";
        public override string FormatType => "json";
        
        public new static Settings Instance => AttributeGlobalSettings<Settings>.Instance;

        [SettingPropertyBool("{=ibs_enable_mod}Enable Mod", RequireRestart = false, 
            HintText = "{=ibs_enable_mod_hint}Whether to enable IronBlood Siege - Note: Do not enable or disable during battle, adjust before entering the scene", Order = 0)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 0)]
        public bool IsEnabled { get; set; } = true;

        [SettingPropertyBool("{=ibs_enable_when_player_attacker}Enable When Player is Attacker", RequireRestart = false,
            HintText = "{=ibs_enable_when_player_attacker_hint}Whether to enable iron will effect when player is on the attacking side", Order = 1)]
        [SettingPropertyGroup("{=ibs_settings_basic}Basic Settings", GroupOrder = 0)]
        public bool EnableWhenPlayerAttacker { get; set; } = true;

        [SettingPropertyBool("{=ibs_aggressive_reinforcement}Enable Aggressive Reinforcement", RequireRestart = false, 
            HintText = "{=ibs_aggressive_reinforcement_hint}Faster reinforcement arrival - Compatible with RBM AI's Spawning modifications (RBM tested only)", Order = 1)]
        [SettingPropertyGroup("{=ibs_reinforcement_settings}Reinforcement Settings", GroupOrder = 1)]
        public bool EnableAggressiveReinforcement { get; set; } = false;

        [SettingPropertyBool("{=ibs_fixed_retreat}Fixed Number Iron Will Disable", RequireRestart = false, 
            HintText = "{=ibs_fixed_retreat_hint}Disable iron will when attacker troops fall below specified number", Order = 0)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 2)]
        public bool EnableFixedRetreat { get; set; } = true;

        [SettingPropertyInteger("{=ibs_retreat_threshold}Fixed Number Threshold", 10, 500, "0", RequireRestart = false, 
            HintText = "{=ibs_retreat_threshold_hint}Disable iron will when attacker troops fall below this number", Order = 1)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public int RetreatThreshold { get; set; } = 100;

        [SettingPropertyBool("{=ibs_ratio_retreat}Auto Ratio Iron Will Disable", RequireRestart = false, 
            HintText = "{=ibs_ratio_retreat_hint}Disable iron will when attacker troops fall below specified ratio of defenders", Order = 2)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public bool EnableRatioRetreat { get; set; } = false;

        [SettingPropertyFloatingInteger("{=ibs_ratio_threshold}Ratio Threshold", 0.3f, 0.9f, "0.00", RequireRestart = false,
            HintText = "{=ibs_ratio_threshold_hint}Disable iron will when attacker/defender ratio falls below this value. Default: 0.7", Order = 3)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public float RatioThreshold { get; set; } = 0.7f;

        [SettingPropertyFloatingInteger("{=ibs_disable_delay}Disable Delay", 10f, 120f, "0", RequireRestart = false, 
            HintText = "{=ibs_disable_delay_hint}Time to wait before disabling iron will (seconds). Default: 60", Order = 4)]
        [SettingPropertyGroup("{=ibs_retreat_conditions}Iron Will Disable Conditions (Note: Choose only one of the two methods!)", GroupOrder = 1)]
        public float DisableDelay { get; set; } = 60f;

        [SettingPropertyFloatingInteger("{=ibs_morale_threshold}Siege Troop Morale Threshold", 20f, 80f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_threshold_hint}Morale will be boosted when troops fall below this value. Default: 70", Order = 0)]
        [SettingPropertyGroup("{=ibs_combat_settings}Combat Settings", GroupOrder = 2)]
        public float MoraleThreshold { get; set; } = 70f;

        [SettingPropertyFloatingInteger("{=ibs_morale_boost_rate}Morale Boost Rate", 5f, 30f, "0", RequireRestart = false, 
            HintText = "{=ibs_morale_boost_rate_hint}Amount of morale boost per update. Default: 15", Order = 1)]
        [SettingPropertyGroup("{=ibs_combat_settings}Combat Settings", GroupOrder = 2)]
        public float MoraleBoostRate { get; set; } = 15f;

        [SettingPropertyGroup("{=ibs_formation_settings}Formation Settings", GroupOrder = 3)]
        [SettingPropertyDropdown("{=ibs_gate_attack_formation}Gate Attack Formation", Order = 0, RequireRestart = false,
            HintText = "{=ibs_gate_attack_formation_hint}Choose the formation type when troops attack gates Default ： Line")]
        public Dropdown<GateAttackFormation> GateAttackFormationType { get; set; } = new Dropdown<GateAttackFormation>(
            System.Enum.GetValues(typeof(GateAttackFormation)).Cast<GateAttackFormation>().ToArray(),
            selectedIndex: 0);  // Default to Line

        public Settings()
        {
            IsEnabled = true;
            EnableFixedRetreat = true;
            RetreatThreshold = 100;
            EnableRatioRetreat = false;
            RatioThreshold = 0.7f;
            DisableDelay = 60f;
            MoraleThreshold = 70f;
            MoraleBoostRate = 15f;
            EnableAggressiveReinforcement = false;
            GateAttackFormationType = new Dropdown<GateAttackFormation>(
                System.Enum.GetValues(typeof(GateAttackFormation)).Cast<GateAttackFormation>().ToArray(),
                selectedIndex: 0);  // Default to Line
        }
    }
} 