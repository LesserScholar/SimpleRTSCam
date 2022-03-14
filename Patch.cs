using HarmonyLib;
using SandBox.ViewModelCollection;
using System.Collections.Generic;
using System.Reflection.Emit;

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
}
