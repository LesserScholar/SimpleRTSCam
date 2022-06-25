using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace SimpleRTSCam
{
    public class RTSFormationsVM : ViewModel
    {
        public RTSCam rtsCam;

        [DataSourceProperty]
        public bool IsEnabled => rtsCam.InRtsCam;

        [DataSourceProperty]
        public MBBindingList<RTSFormationItemVM> Formations { get; set; }

        public RTSFormationsVM(RTSCam rts)
        {
            rtsCam = rts;
            Formations = new MBBindingList<RTSFormationItemVM>();
        }
        public void Initialize()
        {
            foreach (var formation in rtsCam.Mission.PlayerTeam.FormationsIncludingEmpty)
            {
                Formations.Add(new RTSFormationItemVM(formation, rtsCam));
            }
        }

        public void Tick()
        {
            foreach (var f in Formations)
            {
                f.Tick();
            }
        }
    }
    public class RTSFormationItemVM : ViewModel
    {
        public Mission mission;
        public Formation formation;
        public Camera camera;
        public OrderController playerOrderController;
        public MissionGauntletSingleplayerOrderUIHandler orderUIHandler;

        [DataSourceProperty]
        public int OrderOfBattleFormationClassInt { get; set; }
        [DataSourceProperty]
        public string TitleText { get; set; }
        [DataSourceProperty]
        public int TroopCount => formation.CountOfUnits;
        [DataSourceProperty]
        public bool IsMarkerShown => TroopCount > 0;
        [DataSourceProperty]
        public bool IsSelected => mission.IsOrderMenuOpen && playerOrderController.SelectedFormations.Contains(formation);
        [DataSourceProperty]
        public bool IsControlledByPlayer => mission.PlayerTeam.IsPlayerGeneral;
        [DataSourceProperty]
        public Vec2 ScreenPosition { get; set; }
        [DataSourceProperty]
        public int WSign { get; set; }

        public RTSFormationItemVM(Formation formation, RTSCam rts)
        {
            this.formation = formation;
            TitleText = Common.ToRoman(formation.Index + 1);
            ScreenPosition = new Vec2();
            this.mission = rts.Mission;
            this.camera = rts.MissionScreen.CombatCamera;
            playerOrderController = mission.PlayerTeam.PlayerOrderController;

            orderUIHandler = rts._singleplayerOrderHandler;
        }

        public void Tick()
        {
            OnPropertyChanged("IsMarkerShown");
            if (!IsMarkerShown) return;

            UpdatePosition();
            UpdateClass();

            OnPropertyChanged("TroopCount");
            OnPropertyChanged("WSign");
            OnPropertyChanged("ScreenPosition");
            OnPropertyChanged("OrderOfBattleFormationClassInt");
            OnPropertyChanged("IsSelected");
        }
        void UpdatePosition()
        {
            var worldPoint = formation.GetAveragePositionOfUnits(true, true);
            if (worldPoint == Vec2.Invalid) return;
            var worldPos = worldPoint.ToVec3();
            mission.Scene.GetHeightAtPoint(worldPoint, TaleWorlds.Engine.BodyFlags.CommonCollisionExcludeFlagsForCombat, ref worldPos.z);
            worldPos.z += 1.3f;
            float w = 0f;
            Vec2 screenPos = new Vec2();
            MBWindowManager.WorldToScreen(camera, worldPos, ref screenPos.x, ref screenPos.y, ref w);
            ScreenPosition = screenPos;

            WSign = w < 0f ? -1 : 1;
        }
        void UpdateClass()
        {
            // If formation class is set to one of the double classes, leave it alone.
            if (OrderOfBattleFormationClassInt >= (int)DeploymentFormationClass.InfantryAndRanged) return;

            float bestRatio = 0.0f;
            DeploymentFormationClass bestClass = DeploymentFormationClass.Unset;

            float r = formation.QuerySystem.InfantryUnitRatio;
            if (r > bestRatio)
            {
                bestRatio = r;
                bestClass = DeploymentFormationClass.Infantry;
            }
            r = formation.QuerySystem.RangedUnitRatio;
            if (r > bestRatio)
            {
                bestRatio = r;
                bestClass = DeploymentFormationClass.Ranged;
            }
            r = formation.QuerySystem.CavalryUnitRatio;
            if (r > bestRatio)
            {
                bestRatio = r;
                bestClass = DeploymentFormationClass.Cavalry;
            }
            r = formation.QuerySystem.RangedCavalryUnitRatio;
            if (r > bestRatio)
            {
                bestRatio = r;
                bestClass = DeploymentFormationClass.HorseArcher;
            }
            OrderOfBattleFormationClassInt = (int)bestClass;
        }

        private void ExecuteSelection()
        {
            if (IsSelected)
            {
                orderUIHandler.DeselectFormationAtIndex(formation.Index);
            }
            else
            {
                orderUIHandler.SelectFormationAtIndex(formation.Index);
            }
        }
    }
}