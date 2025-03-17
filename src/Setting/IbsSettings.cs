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

        [SettingPropertyFloatingInteger("{=ibs_retreat_dead}Casualties as a percentage of total troops", 10f, 99f, "0.0", RequireRestart = false,
            HintText = "{=ibs_retreat_dead_hint}When the number of casualties reaches this ratio of the total number of troops, the Buff effect disappears.. Default: 70", Order = 1)]
        [SettingPropertyGroup("{=ibs_retreat_settings}Buff Settings", GroupOrder = 2)]
        public float CanRetreatRatios { get; set; } = 70f;

        [SettingPropertyBool("{=ibs_enable_spawn_balance}Enable strength adjustment", RequireRestart = false,
            HintText = "{=ibs_enable_spawn_balance_hint}Whether to enable Adjusts the in-battlefield force ratio between the attacking and defending sides in a siege", Order = 1)]
        [SettingPropertyGroup("{=ibs_spawn_basic}Strength adjustment", GroupOrder = 3)]
        public bool EnableSpawnBalance { get; set; } = true;

        [SettingPropertyInteger("{=ibs_attacker_ratio}Attacker Troops Ratio", 50, 80, RequireRestart = true,
            HintText = "{=ibs_attacker_ratio_hint}Percentage of the attacking side's initial force to the total force in a siege battle. Default value: 65", Order = 2)]
        [SettingPropertyGroup("{=ibs_spawn_basic}Strength adjustment", GroupOrder = 3)]
        public int AttackerTroopsRatio { get; set; } = 65;

        [SettingPropertyGroup("{=ibs_formation_settings}Formation Settings", GroupOrder = 4)]
        [SettingPropertyDropdown("{=ibs_gate_attack_formation}Gate Attack Formation", Order = 0, RequireRestart = false,
            HintText = "{=ibs_gate_attack_formation_hint}Choose the formation type when troops attack gates Default ： Line")]
        public Dropdown<GateAttackFormation> GateAttackFormationType { get; set; } = new Dropdown<GateAttackFormation>(
            System.Enum.GetValues(typeof(GateAttackFormation)).Cast<GateAttackFormation>().ToArray(),
            selectedIndex: 0);

        public IbsSettings()
        {
            IsEnabled = true;
            EnableWhenPlayerAttacker = true;
            CanRetreatRatios = 70f;
            EnableSpawnBalance = true;
            AttackerTroopsRatio = 65;

            GateAttackFormationType = new Dropdown<GateAttackFormation>(
                System.Enum.GetValues(typeof(GateAttackFormation)).Cast<GateAttackFormation>().ToArray(),
                selectedIndex: 0);
        }
    }
} 