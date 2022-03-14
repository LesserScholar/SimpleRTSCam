using HarmonyLib;
using SandBox.ViewModelCollection;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;

namespace SimpleRTSCam
{

    // Patch to show `PowerComparer` in development mode
    [HarmonyPatch(typeof(SPScoreboardVM), nameof(SPScoreboardVM.Tick))]
    internal class SPScoreboardVMPatch : HarmonyPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Match `Mission.Current.Mode != MissionMode.Deployment` and replace it with `true`
            var toReplace = new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Call, (object)AccessTools.Method("TaleWorlds.MountAndBlade.Mission:get_Current")),
                new CodeInstruction(OpCodes.Callvirt, (object)AccessTools.Method("TaleWorlds.MountAndBlade.Mission:get_Mode")),
                new CodeInstruction(OpCodes.Ldc_I4_6, null),
                new CodeInstruction(OpCodes.Ceq, null),
                new CodeInstruction(OpCodes.Ldc_I4_0, null),
                new CodeInstruction(OpCodes.Ceq, null),
            };
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                int j;
                for (j = 0; j < toReplace.Count; j++)
                {
                    if (codes[i + j].opcode != toReplace[j].opcode || codes[i + j].operand != toReplace[j].operand
                        || codes[i + j].labels.Count != 0 || codes[i + j].blocks.Count != 0)
                        break;
                }
                if (j == toReplace.Count)
                {
                    codes.RemoveRange(i, j);
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldc_I4_1));
                }
            }
            return codes;
        }
    }

    // Patch to remove deselecting formations after giving order in OoB ui.
    // This patch isn't perfect since you actually want this with orders that close order view.
    // However, the og behavior doesn't make sense either since the formations aren't really deselected, it's just a visual bug.
    // Since it's already broken, I can't be bothered to fix it for OoB, but only for RTS cam.
    [HarmonyPatch(typeof(OrderOfBattleVM), "OnOrderIssued")]
    internal class OrderOfBattleVMPatch: HarmonyPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            if (codes[1].opcode == OpCodes.Call
                && codes[1].operand == (object)AccessTools.Method("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM:DeselectAllFormations"))
            {
                codes.RemoveRange(0, 2);
            }
            return codes;
        }
    }
}
