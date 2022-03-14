using TaleWorlds.MountAndBlade;

namespace SimpleRTSCam
{
    public class SubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new RTSCam());
        }
    }
}