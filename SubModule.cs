using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace SimpleRTSCam
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Harmony harmony = new Harmony("SimpleRTSCam");
            harmony.PatchAll();
        }
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new RTSCam());
        }
    }
}