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
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace SimpleRTSCam
{
    public class RTSCam : MissionView
    {
        static RTSCam? instance;

        bool _battleStarted = false;
        bool _inRtsCam = false;
        public bool InRtsCam { get { return _inRtsCam; } }

        private RTSFormationsVM? _dataSource;
        private GauntletLayer? _gauntletLayer;
        private IGauntletMovie? _movie;

        private float _delayVMTick = 0f;

        private MissionOrderGauntletUIHandler? _orderUIHandler;
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
                _dataSource = new RTSFormationsVM();
                _movie = _gauntletLayer.LoadMovie("RTSFormations", _dataSource);

                _orderUIHandler = Mission.GetMissionBehavior<MissionOrderGauntletUIHandler>();
                if (_orderUIHandler != null)
                    _missionOrderVM = (MissionOrderVM)AccessTools.Field(typeof(MissionOrderGauntletUIHandler), "_dataSource").GetValue(_orderUIHandler);

                MissionScreen.AddLayer(_gauntletLayer);
                Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(new Action<MissionPlayerToggledOrderViewEvent>(this.OnToggleOrder));
            }
        }
        public override void OnMissionScreenFinalize()
        {
            instance = null;
            if (_gauntletLayer != null)
            {
                _gauntletLayer.ReleaseMovie(this._movie);
                MissionScreen.RemoveLayer(this._gauntletLayer);

                Game.Current.EventManager.UnregisterEvent<MissionPlayerToggledOrderViewEvent>(this.OnToggleOrder);
            }
            base.OnMissionScreenFinalize();
        }
        private void OnToggleOrder(MissionPlayerToggledOrderViewEvent ev)
        {
            if (ev.IsOrderEnabled)
            {
                _dataSource.DeselectAllFormations();
            }
        }
        void OnDeploymentFinish()
        {
            _battleStarted = true;

            _dataSource.Initialize(base.Mission, base.MissionScreen.CombatCamera,
                new Action<int>(this.SelectFormationAtIndex), new Action<int>(this.DeselectFormationAtIndex),
                () => { }, () => { },
                new Dictionary<int, Agent>(), (Agent) => { });
            _dataSource.missionOrderVM = _missionOrderVM;
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
                    _dataSource?.RtsTick();
                }
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

    public class RTSFormationsVM : OrderOfBattleVM
    {
        public MissionOrderVM missionOrderVM;
        public void RtsTick()
        {
            // This is going to be very dumb code but w/e. All of this is just to update the icons when troops are transfered between formations.
            // The formation classes and deployment formation classes seem a bit complicated to me.
            var troopList = missionOrderVM.TroopController.TroopList;
            foreach (OrderTroopItemVM ti in troopList)
            {
                int i = ti.Formation.Index;
                if (_allFormations.Count < i) continue;
                // If formation class is set to one of the double classes, leave it alone.
                if (_allFormations[i].OrderOfBattleFormationClassInt >= (int)DeploymentFormationClass.InfantryAndRanged) continue;
                // This is not the displayed formation class. This is just something that has to be set to a valid value or else it wont let it update.
                // Has to be set with reflection because setting the property sends an order to change the formation class.
                AccessTools.Field(typeof(OrderOfBattleFormationClassVM), "_class").SetValue(_allFormations[i].Classes[0], FormationClass.Infantry);

                float bestRatio = 0.0f;
                DeploymentFormationClass bestClass = DeploymentFormationClass.Unset;

                float r = ti.Formation.QuerySystem.InfantryUnitRatio;
                if (r > bestRatio)
                {
                    bestRatio = r;
                    bestClass = DeploymentFormationClass.Infantry;
                }
                r = ti.Formation.QuerySystem.RangedUnitRatio;
                if (r > bestRatio)
                {
                    bestRatio = r;
                    bestClass = DeploymentFormationClass.Ranged;
                }
                r = ti.Formation.QuerySystem.CavalryUnitRatio;
                if (r > bestRatio)
                {
                    bestRatio = r;
                    bestClass = DeploymentFormationClass.Cavalry;
                }
                r = ti.Formation.QuerySystem.RangedCavalryUnitRatio;
                if (r > bestRatio)
                {
                    bestRatio = r;
                    bestClass = DeploymentFormationClass.HorseArcher;
                }
                _allFormations[i].OrderOfBattleFormationClassInt = (int)bestClass;
            }

            foreach (var f in _allFormations)
            {
                f.Tick();
            }
        }
    }
}