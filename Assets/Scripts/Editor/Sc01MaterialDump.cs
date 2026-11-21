#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 只读诊断：把影棚壳 STUDIO_Studio 下每个渲染器的名称、材质名、世界高度与包围盒尺寸导出发到日志。
    /// 用途：定位"抬头一片死黑"到底是哪块面、挂了哪个材质。
    /// </summary>
    internal static class Sc01MaterialDump
    {
        [MenuItem("ShotField/SC01/Dump 影棚壳材质对应")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (!Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Finish("DUMP");
                return;
            }

            var shell = Sc01PatchCore.FindDeep("STUDIO_Studio");
            if (shell == null)
            {
                Debug.LogError("[SC01-DUMP] 找不到 STUDIO_Studio");
                Sc01PatchCore.Finish("DUMP");
                return;
            }

            var report = new StringBuilder("[SC01-DUMP] STUDIO_Studio 渲染器清单\n");
            foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bounds = renderer.bounds;
                var material = renderer.sharedMaterial;
                report.AppendLine(string.Format(
                    "[SC01-DUMP] {0,-28} mat={1,-26} y={2,6:F2} size=({3:F2},{4:F2},{5:F2}) active={6}",
                    renderer.name, material == null ? "(null)" : material.name,
                    bounds.center.y, bounds.size.x, bounds.size.y, bounds.size.z,
                    renderer.gameObject.activeInHierarchy));
            }
            Debug.Log(report.ToString());

            var panels = 0;
            foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.bounds.center.y > 3.5f && renderer.bounds.size.y < 0.5f) panels++;
            Sc01PatchCore.Check(panels > 0, "DUMP 顶部平板数", "count=" + panels);
            Sc01PatchCore.Finish("DUMP");
        }
    }
}
#endif
