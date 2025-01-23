using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace IronBloodSiege
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "IronBloodSiege_v1";
        public override string DisplayName => "铁血攻城";
        public override string FolderName => "IronBloodSiege";
        public override string FormatType => "json";
        
        public new static Settings Instance => AttributeGlobalSettings<Settings>.Instance;

        [SettingPropertyBool("启用Mod", RequireRestart = false, HintText = "是否启用铁血攻城 -- tip：作者只是个学生，不喜勿喷", Order = 0)]
        [SettingPropertyGroup("基础设置", GroupOrder = 0)]
        public bool IsEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger("攻城士兵士气阈值", 20f, 80f, "0", RequireRestart = false, 
            HintText = "当攻城士兵士气低于此值时会被提升 默认 70", Order = 0)]
        [SettingPropertyGroup("战斗设置", GroupOrder = 1)]
        public float MoraleThreshold { get; set; } = 70f;

        [SettingPropertyFloatingInteger("士气提升速率", 5f, 30f, "0", RequireRestart = false, 
            HintText = "攻城士兵每次提升士气的幅度 默认15", Order = 1)]
        [SettingPropertyGroup("战斗设置", GroupOrder = 1)]
        public float MoraleBoostRate { get; set; } = 15f;

        public Settings()
        {
            IsEnabled = true;
            MoraleThreshold = 70f;
            MoraleBoostRate = 15f;
        }
    }
} 