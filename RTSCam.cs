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
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;

namespace SimpleRTSCam
{
    public class RTSCam : MissionView
    {
        static RTSCam? instance;

        bool _battleStarted = false;
        bool _inRtsCam = false;
        public bool InRtsCam { get { return _inRtsCam; } }

        private RTSFormationsVM _dataSource;
        private GauntletLayer _gauntletLayer;
        private IGauntletMovie _movie;

        public MissionGauntletOrderOfBattleUIHandler _orderOfBattleHandler;
        public MissionGauntletSingleplayerOrderUIHandler _singleplayerOrderHandler;
        MissionOrderVM? _missionOrderVM;
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            instance = this;
            var deploymentView = Mission.GetMissionBehavior<DeploymentMissionView>();
            if (deploymentView != null)
            {
                deploymentView.OnDeploymentFinish = (OnPlayerDeploymentFinishDelegate)Delegate.Combine(deploymentView.OnDeploymentFinish,
                    new OnPlayerDeploymentFinishDelegate(OnDeploymentFinish));

                _gauntletLayer = new GauntletLayer(this.ViewOrderPriority, "GauntletLayer", false);
                _dataSource = new RTSFormationsVM(this);
                _movie = _gauntletLayer.LoadMovie("RTSFormations", _dataSource);

                _orderOfBattleHandler = Mission.GetMissionBehavior<MissionGauntletOrderOfBattleUIHandler>();
                
                _singleplayerOrderHandler = Mission.GetMissionBehavior< MissionGauntletSingleplayerOrderUIHandler>();
                _missionOrderVM = (MissionOrderVM)AccessTools.Field(typeof(MissionGauntletSingleplayerOrderUIHandler), "_dataSource").GetValue(_singleplayerOrderHandler);

                MissionScreen.AddLayer(_gauntletLayer);
            }
        }
        public override void OnMissionScreenFinalize()
        {
            instance = null;
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
            _dataSource.Initialize();
        }

        public void TryCloseOrderControls()
        {
            if (_missionOrderVM != null)
            {
                _missionOrderVM.TryCloseToggleOrder(true);
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
                    _gauntletLayer.InputRestrictions.SetMouseVisibility(false);
                }
                if (base.MissionScreen.SceneLayer.Input.IsKeyReleased(InputKey.RightMouseButton))
                {
                    _gauntletLayer.InputRestrictions.SetMouseVisibility(true);
                }

                _dataSource.Tick();
            }
            if (_battleStarted && (Input.IsKeyPressed(InputKey.F10) || Input.IsGameKeyPressed(CombatHotKeyCategory.PushToTalk)))
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
            _dataSource.OnPropertyChanged("IsEnabled");
            if (_orderOfBattleHandler != null)
                new Traverse(_orderOfBattleHandler).Property("IsBattleDeployment").SetValue(false);
            TryCloseOrderControls();
        }
        public void OpenRtsCam()
        {
            if (Mission.Mode != MissionMode.Battle || Mission.MainAgent == null) return;
            _inRtsCam = true;

            _dataSource.OnPropertyChanged("IsEnabled");
            _gauntletLayer?.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            if (_orderOfBattleHandler != null)
                new Traverse(_orderOfBattleHandler).Property("IsBattleDeployment").SetValue(true);

            new Traverse(MissionScreen).Property("CameraElevation").SetValue(-0.4f);
            if (Mission.MainAgent != null)
            {
                var lookDir = Mission.MainAgent.LookDirection;
                lookDir.z = 0;
                lookDir.Normalize();
                MissionScreen.CombatCamera.Position = Mission.MainAgent.Position + lookDir * -18f + Vec3.Up * 20f;
            }
            else
                MissionScreen.CombatCamera.Position = MissionScreen.CombatCamera.Position + Vec3.Up * 20f;
        }

        public override bool OnEscape()
        {
            if (_inRtsCam)
            {
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