using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace SimpleRTSCam
{
    public class RTSCam : MissionView
    {
        bool _battleStarted = false;
        bool _inRtsCam = false;

        private OrderOfBattleVM? _dataSource;
        private GauntletLayer? _gauntletLayer;
        private IGauntletMovie? _movie;
        private MissionOrderGauntletUIHandler? _orderUIHandler;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            var deploymentView = Mission.GetMissionBehavior<DeploymentMissionView>();
            if (deploymentView != null)
            {
                deploymentView.OnDeploymentFinish = (OnPlayerDeploymentFinishDelegate)Delegate.Combine(deploymentView.OnDeploymentFinish,
                    new OnPlayerDeploymentFinishDelegate(OnDeploymentFinish));
            }

            _dataSource = new OrderOfBattleVM();
            _gauntletLayer = new GauntletLayer(this.ViewOrderPriority, "GauntletLayer", false);
            _movie = _gauntletLayer.LoadMovie("RTSFormations", _dataSource);
            _orderUIHandler = base.Mission.GetMissionBehavior<MissionOrderGauntletUIHandler>();
            MissionScreen.AddLayer(_gauntletLayer);
        }
        public override void OnMissionScreenFinalize()
        {
            if (_gauntletLayer != null)
            {
                _gauntletLayer.ReleaseMovie(this._movie);
                MissionScreen.RemoveLayer(this._gauntletLayer);
            }
            base.OnMissionScreenFinalize();
        }
        void OnDeploymentFinish()
        {
            _battleStarted = true;
            this._dataSource?.Initialize(base.Mission, base.MissionScreen.CombatCamera, 
                new Action<int>(this.SelectFormationAtIndex), new Action<int>(this.DeselectFormationAtIndex), 
                () => { }, () => { }, 
                new Dictionary<int, Agent>(), (Agent) => {});
        }
        private void SelectFormationAtIndex(int index)
        {
            _orderUIHandler?.SelectFormationAtIndex(index);
        }
        private void DeselectFormationAtIndex(int index)
        {
            _orderUIHandler?.DeselectFormationAtIndex(index);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (_inRtsCam)
            {
                if (base.MissionScreen.SceneLayer.Input.IsKeyPressed(InputKey.RightMouseButton))
                {
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(false);
                }
                if (base.MissionScreen.SceneLayer.Input.IsKeyReleased(InputKey.RightMouseButton))
                {
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(true);
                }
                _dataSource?.OnUnitDeployed();
                _dataSource?.Tick();
            }
            if (_battleStarted && Input.IsKeyPressed(InputKey.F10))
            {
                if (Mission.Mode == MissionMode.Battle)
                {
                    _inRtsCam = true;
                    Mission.ClearDeploymentPlanForSide(Mission.PlayerTeam.Side);
                    Mission.SetMissionMode(MissionMode.Deployment, false);
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(true);
                    _gauntletLayer?.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                    if (_dataSource != null)
                        _dataSource.IsEnabled = true;
                }
                else if (Mission.Mode == MissionMode.Deployment)
                {
                    _inRtsCam = false;
                    Mission.SetMissionMode(MissionMode.Battle, false);
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(false);
                    _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
                    if (_dataSource != null)
                        _dataSource.IsEnabled = false;
                }
            }
        }
    }
}