#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 导出态视觉层：景物刷两级灰、叠加层（轨迹线/琥珀胶带/落点标记）关闭。
    /// 用 MaterialPropertyBlock 覆盖颜色，不改 sharedMaterials，因此不污染资产、
    /// 不产生 "Instantiating material" 报错；Restore 清空 property block 即复原。
    /// </summary>
    public static class Sc01PrevisLook
    {
        private const string WallHex = "#D8D8D8";
        private const string FloorHex = "#9A9A9A";
        private static readonly List<Renderer> Tinted = new List<Renderer>();
        private static readonly List<LineRenderer> HiddenLines = new List<LineRenderer>();
        private static readonly List<GameObject> HiddenObjects = new List<GameObject>();
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static bool active;

        public static bool IsActive => active;

        [MenuItem("ShotField/SC01/Previs Look: Apply (gray stage, hide overlays)")]
        public static string Apply()
        {
            if (active) return "already applied";
            var wall = ParseColor(WallHex);
            var floor = ParseColor(FloorHex);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (IsActor(r)) continue;
                if (IsOverlay(r)) { Disable(r.gameObject); continue; }
                r.GetPropertyBlock(Block);
                Block.SetColor("_BaseColor", IsFloor(r) ? floor : wall);
                Block.SetFloat("_Smoothness", 0.05f);
                Block.SetFloat("_Metallic", 0f);
                r.SetPropertyBlock(Block);
                Tinted.Add(r);
            }
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude))
                if (lr.enabled && IsTrajectory(lr)) { lr.enabled = false; HiddenLines.Add(lr); }
            active = true;
            return "tinted=" + Tinted.Count + " linesOff=" + HiddenLines.Count + " objectsOff=" + HiddenObjects.Count;
        }

        [MenuItem("ShotField/SC01/Previs Look: Restore")]
        public static string Restore()
        {
            if (!active) return "not applied";
            foreach (var r in Tinted) if (r != null) r.SetPropertyBlock(null);
            foreach (var lr in HiddenLines) if (lr != null) lr.enabled = true;
            foreach (var go in HiddenObjects) if (go != null) go.SetActive(true);
            Tinted.Clear(); HiddenLines.Clear(); HiddenObjects.Clear();
            active = false;
            return "restored";
        }

        private static bool IsActor(Renderer r)
        {
            for (var t = r.transform; t != null; t = t.parent)
            {
                var n = t.name;
                if (n.StartsWith("PrevisCast_") || n.Contains("Performer") || n.StartsWith("ActorA") || n.Contains("Biped_Humanoid")) return true;
                if (n.Contains("Envelope")) return true;
            }
            return false;
        }

        private static bool IsFloor(Renderer r)
        {
            for (var t = r.transform; t != null; t = t.parent)
                if (t.name.Contains("Floor") || t.name.Contains("Ground")) return true;
            return false;
        }

        private static bool IsOverlay(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.name.Contains("AmberTape") || m.name.Contains("MAT_Previs_Ring") || m.name.Contains("Guide"))) return true;
            return r.name.Contains("DollyTrackMark") || r.name.Contains("TargetRing") || r.name.Contains("WalkMarker");
        }

        private static bool IsTrajectory(LineRenderer lr)
        {
            for (var t = lr.transform; t != null; t = t.parent)
                if (t.name == "Director" || t.name.Contains("Trajectory") || t.name.Contains("Path")) return true;
            return lr.positionCount > 60;
        }

        private static void Disable(GameObject go)
        {
            go.SetActive(false);
            HiddenObjects.Add(go);
        }

        private static Color ParseColor(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.gray;
        }

        /// <summary>导出前的一行自检：报告灰模与叠加层状态，供验收引用。</summary>
        public static string Audit()
        {
            int lines = 0, tape = 0;
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude))
                if (lr.enabled && IsTrajectory(lr)) lines++;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if (r.gameObject.activeInHierarchy && r.name.Contains("DollyTrackMark")) tape++;
            return "PREVIS-LOOK active=" + active + " visibleTrajectoryLines=" + lines + " visibleAmberTape=" + tape;
        }
    }
}
#endif
