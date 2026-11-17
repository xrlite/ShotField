#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 编译自检：只在命令行 batchmode 下用。
    /// 如果本类能被加载并执行，本身就证明 ShotField 的全部脚本编译通过。
    /// </summary>
    public static class CompileCheck
    {
        public static void Run()
        {
            int ok = 0;

            Check("Core.ShotData", typeof(ShotField.Core.ShotData), ref ok);
            Check("Core.ShotCameraState", typeof(ShotField.Core.ShotCameraState), ref ok);
            Check("Core.SceneSnapshot", typeof(ShotField.Core.SceneSnapshot), ref ok);
            Check("Core.ConstraintResult", typeof(ShotField.Core.ConstraintResult), ref ok);
            Check("Core.SemanticMarker", typeof(ShotField.Core.SemanticMarker), ref ok);
            Check("Core.ShotLibrary", typeof(ShotField.Core.ShotLibrary), ref ok);

            Check("Constraints.ShotConstraint", typeof(ShotField.Constraints.ShotConstraint), ref ok);
            Check("Constraints.VisibilityConstraint", typeof(ShotField.Constraints.VisibilityConstraint), ref ok);
            Check("Constraints.AxisConstraint", typeof(ShotField.Constraints.AxisConstraint), ref ok);
            Check("Constraints.FramingConstraint", typeof(ShotField.Constraints.FramingConstraint), ref ok);

            Debug.Log("[COMPILE-CHECK] 通过类型数 = " + ok + " / 10");
            Debug.Log("[COMPILE-CHECK] RESULT=" + (ok == 10 ? "PASS" : "FAIL"));

            EditorApplication.Exit(ok == 10 ? 0 : 1);
        }

        private static void Check(string name, System.Type t, ref int ok)
        {
            if (t != null)
            {
                ok++;
                Debug.Log("[COMPILE-CHECK] OK  " + name);
            }
            else
            {
                Debug.LogError("[COMPILE-CHECK] MISSING  " + name);
            }
        }
    }
}
#endif
