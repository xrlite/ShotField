#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 按 B1→B5 顺序执行全部补丁，中途不退出，最后统一保存并汇总。
    /// 每个阶段的成败用"本轮新增断言是否全过"判定，避免前一阶段失败污染后一阶段。
    /// </summary>
    internal static class Sc01PatchAll
    {
        [MenuItem("ShotField/SC01/Patch 顺序执行 B1→B5")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (!Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Finish("PATCH-ALL");
                return;
            }

            Sc01PatchCore.Backup("All");
            var b1 = Stage("B1 几何", Sc01SetPatch.Apply);
            var b2 = Stage("B2 材质", Sc01ShellMaterialPatch.Apply);
            var b3a = Stage("B3a 曝光环境光", Sc01GradePatch.Apply);
            var b3b = Stage("B3b 三点光", Sc01LightPatch.Apply);
            var b3c = Stage("B3c 顶面色调", Sc01TonePatch.Apply);
            var b4 = Stage("B4 交互骨架", Sc01RigPatch.Apply);
            var b5 = Stage("B5 演员材质", Sc01ActorPatch.Apply);
            Sc01PatchCore.Commit();

            Debug.Log(string.Format("[SC01-PATCH] STAGES B1={0} B2={1} B3a={2} B3b={3} B3c={4} B4={5} B5={6}",
                b1, b2, b3a, b3b, b3c, b4, b5));
            Sc01PatchCore.Finish("PATCH-ALL");
        }

        private static bool Stage(string label, Func<bool> apply)
        {
            var passBefore = Sc01PatchCore.Pass;
            var totalBefore = Sc01PatchCore.Total;
            apply();
            var passed = Sc01PatchCore.Pass - passBefore;
            var ran = Sc01PatchCore.Total - totalBefore;
            var ok = ran > 0 && passed == ran;
            Debug.Log("[SC01-PATCH] " + label + " -> " + (ok ? "OK" : "FAIL") +
                      " (" + passed + "/" + ran + ")");
            return ok;
        }
    }
}
#endif
