using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace SimpleRTSCam
{
    public class RTSCam : MissionView
    {
        bool _battleStarted = false;
        bool _inRtsCam = false;
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            var deploymentView = Mission.GetMissionBehavior<DeploymentMissionView>();
            if (deploymentView != null)
            {
                deploymentView.OnDeploymentFinish = (OnPlayerDeploymentFinishDelegate)Delegate.Combine(deploymentView.OnDeploymentFinish,
                    new OnPlayerDeploymentFinishDelegate(OnDeploymentFinish));
            }
        }
        void OnDeploymentFinish()
        {
            _battleStarted = true;
        }
        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (_inRtsCam)
            {
                if (base.MissionScreen.SceneLayer.Input.IsKeyPressed(InputKey.RightMouseButton))
                {
                    ScreenManager.FirstHitLayer.InputRestrictions.SetMouseVisibility(false);
                }
                if (base.MissionScreen.SceneLayer.Input.IsKeyReleased(InputKey.RightMouseButton))
                {
                    ScreenManager.FirstHitLayer.InputRestrictions.SetMouseVisibility(true);
                }
            }
            if (_battleStarted && Input.IsKeyPressed(InputKey.F10))
            {
                if (Mission.Mode == MissionMode.Battle)
                {
                    Mission.SetMissionMode(MissionMode.Deployment, false);
                    Mission.ClearDeploymentPlanForSide(Mission.PlayerTeam.Side);
                    ScreenManager.FirstHitLayer.InputRestrictions.SetMouseVisibility(true);
                    _inRtsCam = true;
                }
                else if (Mission.Mode == MissionMode.Deployment)
                {
                    Mission.SetMissionMode(MissionMode.Battle, false);
                    ScreenManager.FirstHitLayer.InputRestrictions.SetMouseVisibility(false);
                    _inRtsCam = false;
                }
            }
        }
    }

    public class SubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new RTSCam());
        }
    }
}