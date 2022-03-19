using HarmonyLib;
using SandBox.ViewModelCollection;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;
using TaleWorlds.MountAndBlade.View.Screen;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace SimpleRTSCam
{

    // Patch to hide spectator controls and crosshairs when in rts cam
    [HarmonyPatch]
    internal class SpectatorControlPatch : HarmonyPatch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MissionSpectatorControl), "OnMissionTick");
            yield return AccessTools.Method(typeof(MissionGauntletCrosshair), "OnMissionScreenTick");
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Match `this.Mission.Mode` and replace it with a static call to RTSCam
            var toReplace = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(MissionBehavior), "get_Mission")),
                new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.Method(typeof(Mission), "get_Mode")),
            };

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                int j;
                for (j = 0; j < toReplace.Count; j++)
                {
                    if (codes[i + j].opcode != toReplace[j].opcode || codes[i + j].operand != toReplace[j].operand
                        || codes[i + j].labels.Count > 0 || codes[i + j].blocks.Count > 0)
                        break;
                }
                if (j == toReplace.Count)
                {
                    codes.RemoveRange(i, j);
                    var instruction = new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(RTSCam), nameof(RTSCam.RtsMissionMode)));
                    codes.Insert(i, instruction);
                }
            }
            return codes;
        }
    }

    // Patch to unlock camera from development boundaries.
    [HarmonyPatch(typeof(MissionScreen), "UpdateCamera")]
    internal class UnlockCameraFromBoundariesPatch : HarmonyPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int opsToRemove = 4;
            for (int i = opsToRemove - 1; i < codes.Count; i++)
            {

                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand == (object)AccessTools.Method(typeof(Mission), nameof(Mission.HasDeploymentBoundaries)))
                {
                    codes.RemoveRange(i - 3, opsToRemove);
                    codes.Insert(i - 3, new CodeInstruction(OpCodes.Ldc_I4_0));
                    break;
                }
            }
            return codes;
        }
    }
    
    // Do not unload order of battle sprites when deployment finishes.
    // MissionOrderOfBattleGauntletUIHandler should try to unload them at end of mission as well.
    // It might be better to manage the sprites in this module instead of using Harmony, but it's ok either way.
    [HarmonyPatch(typeof(MissionOrderOfBattleGauntletUIHandler), "OnDeploymentFinish")]
    internal class MissionOrderOfBattleGauntletUIHandlerPatch : HarmonyPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var toRemove = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(typeof(MissionOrderOfBattleGauntletUIHandler), "_orderOfBattleCategory")),
                new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.Method(typeof(TaleWorlds.TwoDimension.SpriteCategory), "Unload")),
            };
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                int j;
                for (j = 0; j < toRemove.Count; j++)
                {
                    if (codes[i + j].opcode != toRemove[j].opcode || codes[i + j].operand != toRemove[j].operand
                        || codes[i + j].labels.Count > 0 || codes[i + j].blocks.Count > 0)
                        break;
                }
                if (j == toRemove.Count)
                {
                    codes.RemoveRange(i, j);
                    break;
                }
            }
            return codes;
        }
    }
}
