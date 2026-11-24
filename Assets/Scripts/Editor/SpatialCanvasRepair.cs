#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 把 Spatial_Workbench_Canvas 的 RectTransform 复位到 builder 的正确值。
    ///
    /// 背景（2026-11-23）：18:20 为抓图临时把画布切到 ScreenSpaceOverlay，抓完还原了
    /// renderMode / planeDistance / worldCamera，唯独漏还原 RectTransform；7f06dfb 保存
    /// 场景时把这组被覆写的像素值（scale=1、sizeDelta=1270×688.5、anchors=0,0）落盘，
    /// 画布于是变成 1270m × 688m 的世界巨墙。
    ///
    /// 用法：任何一次 Overlay 抓帧之后都必须再跑一次本工具。
    /// batchmode：
    ///   Unity.exe -batchmode -quit -projectPath &lt;projectPath&gt; \
    ///     -executeMethod ShotField.EditorTools.SpatialCanvasRepair.Run -logFile &lt;log&gt;
    /// 判据只认日志里的 CANVAS-REPAIR-RESULT 行，不认进程退出码。
    /// </summary>
    public static class SpatialCanvasRepair
    {
        private const string CanvasName = "Spatial_Workbench_Canvas";
        private const float Scale = 0.0018f;

        private static readonly Vector2 Size = new Vector2(1140f, 860f);
        private static readonly Vector3 Pos = new Vector3(2.35f, 1.35f, 1.55f);
        private static readonly Vector3 Euler = new Vector3(15f, -145f, 0f);

        [MenuItem("ShotField/Workbench/Repair Workbench Canvas Transform")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var rt = FindRoot(scene, CanvasName);
            if (rt == null)
            {
                Report(false, "找不到 " + CanvasName);
                return;
            }

            Undo.RecordObject(rt, "Repair workbench canvas");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Size;
            rt.localScale = Vector3.one * Scale;
            rt.localPosition = Pos;
            // anchors 为 (0,0)/(0,0) 时 anchoredPosition 与 localPosition 同值；
            // 显式再写一次，避免 Unity 序列化出两个不一致的字段（HEAD 就是这个症状）。
            rt.anchoredPosition = new Vector2(Pos.x, Pos.y);
            rt.localRotation = Quaternion.Euler(Euler);
            Canvas.ForceUpdateCanvases();

            var rect = rt.rect;
            var worldW = rt.lossyScale.x * rect.width;
            var worldH = rt.lossyScale.y * rect.height;
            var lp = rt.localPosition;
            var ap = rt.anchoredPosition;
            var eu = rt.localEulerAngles;

            var ok =
                Mathf.Abs(rect.width - Size.x) < 0.5f &&
                Mathf.Abs(rect.height - Size.y) < 0.5f &&
                Mathf.Abs(rt.localScale.x - Scale) < 1e-6f &&
                Vector3.Distance(lp, Pos) < 0.01f &&
                Vector2.Distance(ap, new Vector2(Pos.x, Pos.y)) < 0.01f &&
                Mathf.Abs(Mathf.DeltaAngle(eu.x, Euler.x)) < 0.5f &&
                Mathf.Abs(Mathf.DeltaAngle(eu.y, Euler.y)) < 0.5f &&
                Mathf.Abs(Mathf.DeltaAngle(eu.z, Euler.z)) < 0.5f &&
                worldW > 1.9f && worldW < 2.2f &&
                worldH > 1.4f && worldH < 1.7f;

            if (ok)
            {
                EditorUtility.SetDirty(rt);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, BuildAndroid.ScenePath);
            }

            Report(ok,
                "size=" + rect.width.ToString("F1") + "x" + rect.height.ToString("F1") +
                " scale=" + rt.localScale.x.ToString("F5") +
                " pos=(" + lp.x.ToString("F2") + "," + lp.y.ToString("F2") + "," + lp.z.ToString("F2") + ")" +
                " anch=(" + ap.x.ToString("F2") + "," + ap.y.ToString("F2") + ")" +
                " euler=(" + eu.x.ToString("F1") + "," + eu.y.ToString("F1") + "," + eu.z.ToString("F1") + ")" +
                " worldW=" + worldW.ToString("F3") + " worldH=" + worldH.ToString("F3"));
        }

        private static void Report(bool ok, string detail)
        {
            var line = "CANVAS-REPAIR-RESULT=" + (ok ? "PASS" : "FAIL") + " " + detail;
            if (ok) Debug.Log(line);
            else Debug.LogError(line);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static RectTransform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform as RectTransform;
                var hit = FindDeep(go.transform, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static RectTransform FindDeep(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c as RectTransform;
                var hit = FindDeep(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
#endif
