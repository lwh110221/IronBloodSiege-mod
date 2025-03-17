// 铁血攻城
// 作者：Ahao
// 版本：1.0.0
// email ：ahao221x@gmail.com
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using IronBloodSiege.Message;
using TaleWorlds.Library;

namespace IronBloodSiege
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            Harmony harmony = new Harmony("mod.IronBloodSiege");
            harmony.PatchAll();
            IbsMessage.ShowMessage("IronBloodSiege loaded",Colors.Green);
        }
    }
} 