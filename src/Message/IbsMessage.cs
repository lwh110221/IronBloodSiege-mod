using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace IronBloodSiege.Message
{
    public static class IbsMessage
    {
        private static readonly TextObject GateDestroyedText = new TextObject("{=ibs_msg_gate_destroyed}The gates have been Destroyed.");
        private static readonly TextObject RamDestroyedText = new TextObject("{=ibs_msg_ram_destroyed}Ram Destroyed, troops will attack the gates");
        private static readonly TextObject AttackGateText = new TextObject("{=ibs_msg_attack_gate}The siege forces are assembling to attack the gates.");
        private static readonly TextObject CanRetreatText = new TextObject("{=ibs_msg_can_retreat}The attacking soldiers have suffered too many.Buff failure.");
        private static readonly TextObject BreachMiddleText = new TextObject("{=ibs_msg_breach_middle}The middle lane has been breached! Troops are moving to attack through the gates.");

        // 战斗状态消息
        public static void ShowGateDestroyed() => 
            ShowMessage(GateDestroyedText, Colors.Green);

        public static void ShowRamDestroyed() => 
            ShowMessage(RamDestroyedText, Colors.Green);

        public static void ShowAttackGate() => 
            ShowMessage(AttackGateText, Colors.Green);

        public static void ShowCanRettreat() =>
            ShowMessage(CanRetreatText, Colors.Red);

        public static void ShowBreachMiddle() => 
            ShowMessage(BreachMiddleText, Colors.Green);
        public static void ShowMessage(string message, Color color) => 
            InformationManager.DisplayMessage(new InformationMessage(message, color));

        public static void ShowMessage(TextObject textObject, Color color) => 
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), color));

        public static void ShowError(string message) => 
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Red));

        public static void ShowWarning(string message) => 
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));

        public static void ShowSuccess(string message) => 
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Green));

        public static void ShowInfo(string message) => 
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.White));
    }
} 