using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.ViewModelCollection;
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
        private float _delayVMTick = 0f;
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
            Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(new Action<MissionPlayerToggledOrderViewEvent>(this.OnToggleOrder));
        }
        public override void OnMissionScreenFinalize()
        {
            if (_gauntletLayer != null)
            {
                _gauntletLayer.ReleaseMovie(this._movie);
                MissionScreen.RemoveLayer(this._gauntletLayer);
            }
            Game.Current.EventManager.UnregisterEvent<MissionPlayerToggledOrderViewEvent>(this.OnToggleOrder);
            base.OnMissionScreenFinalize();
        }
        private void OnToggleOrder(MissionPlayerToggledOrderViewEvent ev)
        {
            if (!ev.IsOrderEnabled)
            {
                _dataSource?.DeselectAllFormations();
            }
        }
        void OnDeploymentFinish()
        {
            _battleStarted = true;
            this._dataSource?.Initialize(base.Mission, base.MissionScreen.CombatCamera,
                new Action<int>(this.SelectFormationAtIndex), new Action<int>(this.DeselectFormationAtIndex),
                () => { }, () => { },
                new Dictionary<int, Agent>(), (Agent) => { });
        }
        private void SelectFormationAtIndex(int index)
        {
            _orderUIHandler?.SelectFormationAtIndex(index);
        }
        private void DeselectFormationAtIndex(int index)
        {
            _orderUIHandler?.DeselectFormationAtIndex(index);
        }
        public void TryCloseOrderControls()
        {
            if (_orderUIHandler != null)
            {
                Traverse tr = new Traverse(_orderUIHandler);
                var orderVM = tr.Field("_dataSource").GetValue<MissionOrderVM>();
                if (orderVM != null)
                {
                    orderVM.TryCloseToggleOrder(true);
                }
            }
        }
        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (_inRtsCam)
            {
                // Order UI handler *almost* does the right mouse handling already. However, when
                // that layer is closed, things break. So we have to do this stuff again.
                if (base.MissionScreen.SceneLayer.Input.IsKeyPressed(InputKey.RightMouseButton))
                {
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(false);
                }
                if (base.MissionScreen.SceneLayer.Input.IsKeyReleased(InputKey.RightMouseButton))
                {
                    _gauntletLayer?.InputRestrictions.SetMouseVisibility(true);
                }
                // This is a bit silly hack, but something about tick right after enable seems to cause
                // the VM to go into endless recursion of updating properties.
                _delayVMTick += dt;
                if (_delayVMTick > 0.08f)
                {
                    _dataSource?.OnUnitDeployed();
                    _dataSource?.Tick();
                }
            }
            if (_battleStarted && Input.IsKeyPressed(InputKey.F10))
            {
                if (Mission.Mode == MissionMode.Battle)
                {
                    _inRtsCam = true;
                    if (!Mission.IsSiegeBattle)
                        Mission.ClearDeploymentPlanForSide(Mission.PlayerTeam.Side);
                    Mission.SetMissionMode(MissionMode.Deployment, false);
                    _gauntletLayer?.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                    if (_orderUIHandler != null)
                        new Traverse(_orderUIHandler).Property("IsBattleDeployment").SetValue(true);
                    if (_dataSource != null)
                    {
                        _dataSource.IsEnabled = true;
                        _delayVMTick = 0f;
                    }
                    {
                        new Traverse(MissionScreen).Property("CameraElevation").SetValue(-0.8f);
                        var forward = Mission.MainAgent.LookDirection;
                        var cameraPos = Mission.MainAgent.Position + (-forward) * 18f + Vec3.Up * 20f;
                        MissionScreen.CombatCamera.Position = cameraPos;
                    }
                }
                else if (Mission.Mode == MissionMode.Deployment)
                {
                    _inRtsCam = false;
                    Mission.SetMissionMode(MissionMode.Battle, false);
                    _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
                    if (_orderUIHandler != null)
                        new Traverse(_orderUIHandler).Property("IsBattleDeployment").SetValue(false);
                    if (_dataSource != null)
                        _dataSource.IsEnabled = false;
                    TryCloseOrderControls();
                }
            }
        }

        public override bool OnEscape()
        {
            if (_inRtsCam)
            {
                _dataSource?.DeselectAllFormations();
            }
            return false;
        }

    }
}