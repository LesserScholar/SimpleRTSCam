using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.View.Screen;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace SimpleRTSCam
{
    public class RTSCam : MissionView
    {
        static RTSCam? instance;

        bool _battleStarted = false;
        bool _inRtsCam = false;
        public bool InRtsCam { get { return _inRtsCam; } }

        private OrderOfBattleVM? _dataSource;
        private GauntletLayer? _gauntletLayer;
        private IGauntletMovie? _movie;
        private MissionOrderGauntletUIHandler? _orderUIHandler;
        private float _delayVMTick = 0f;
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            instance = this;
            var deploymentView = Mission.GetMissionBehavior<DeploymentMissionView>();
            if (deploymentView != null)
            {
                deploymentView.OnDeploymentFinish = (OnPlayerDeploymentFinishDelegate)Delegate.Combine(deploymentView.OnDeploymentFinish,
                    new OnPlayerDeploymentFinishDelegate(OnDeploymentFinish));
            }
            _dataSource = new OrderOfBattleVM();
            _gauntletLayer = new GauntletLayer(this.ViewOrderPriority, "GauntletLayer", false);
            _movie = _gauntletLayer.LoadMovie("RTSFormations", _dataSource);
            _orderUIHandler = Mission.GetMissionBehavior<MissionOrderGauntletUIHandler>();
            MissionScreen.AddLayer(_gauntletLayer);
            Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(new Action<MissionPlayerToggledOrderViewEvent>(this.OnToggleOrder));
        }
        public override void OnMissionScreenFinalize()
        {
            instance = null;
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
            if (ev.IsOrderEnabled)
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
                if (_inRtsCam)
                    ExitRtsCam();
                else
                    OpenRtsCam();
            }
        }

        public void ExitRtsCam()
        {
            if (!_inRtsCam || Mission.MainAgent == null) return;
            _inRtsCam = false;

            _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
            if (_orderUIHandler != null)
                new Traverse(_orderUIHandler).Property("IsBattleDeployment").SetValue(false);
            if (_dataSource != null)
                _dataSource.IsEnabled = false;
            TryCloseOrderControls();
        }
        public void OpenRtsCam()
        {
            if (Mission.Mode != MissionMode.Battle || Mission.MainAgent == null) return;
            _inRtsCam = true;

            _gauntletLayer?.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            if (_orderUIHandler != null)
                new Traverse(_orderUIHandler).Property("IsBattleDeployment").SetValue(true);
            if (_dataSource != null)
            {
                _dataSource.IsEnabled = true;
                _delayVMTick = 0f;
            }
            new Traverse(MissionScreen).Property("CameraElevation").SetValue(-0.4f);
            if (Mission.MainAgent != null)
                MissionScreen.CombatCamera.Position = Mission.MainAgent.Position + Mission.MainAgent.LookDirection * -18f + Vec3.Up * 20f;
            else
                MissionScreen.CombatCamera.Position = MissionScreen.CombatCamera.Position + Vec3.Up * 20f;
        }

        public override bool OnEscape()
        {
            if (_inRtsCam)
            {
                _dataSource?.DeselectAllFormations();
                if (Mission.IsOrderMenuOpen)
                {
                    TryCloseOrderControls();
                    return true;
                }
            }
            return false;
        }

        public static MissionMode RtsMissionMode()
        {
            if (instance == null) return Mission.Current.Mode;
            if (instance._inRtsCam) return MissionMode.Deployment;
            return instance.Mission.Mode;
        }

    }

    // Patch to show make the camera think the mission is in deployment mode
    [HarmonyPatch]
    internal class MissionScreenCameraPatch : HarmonyPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MissionScreen), "UpdateCamera");
            yield return AccessTools.Method(typeof(MissionScreen), "GetSpectatingData");
            yield return AccessTools.Method(typeof(MissionScreen), "TaleWorlds.MountAndBlade.IMissionListener.OnMissionModeChange");
            yield return AccessTools.Method(typeof(MissionScreen), "HandleUserInput");
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Match `this.Mission.Mode` and replace it with a static call to RTSCam.RtsMissionMode
            var toReplace = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(MissionScreen), "get_Mission")),
                new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.Method(typeof(Mission), "get_Mode")),
            };

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                int j;
                for (j = 0; j < toReplace.Count; j++)
                {
                    if (codes[i + j].opcode != toReplace[j].opcode || codes[i + j].operand != toReplace[j].operand)
                        break;
                }
                if (j == toReplace.Count)
                {
                    for (j = 0; j < toReplace.Count; j++)
                    {
                        if (j > 0 && codes[i + j].labels.Count > 0) throw new ArgumentException("Unsupported game version. Crash from SimpleRTSCam.");
                        if (codes[i + j].blocks.Count > 0) throw new ArgumentException("Unsupported game version. Crash from SimpleRTSCam.");
                    }
                    var labels = codes[i].labels;
                    codes.RemoveRange(i, j);
                    var instruction = new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(RTSCam), nameof(RTSCam.RtsMissionMode)));
                    instruction.labels = labels;
                    codes.Insert(i, instruction);
                }
            }
            return codes;
        }
    }
}