using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 运行时真实绕行路径可视化：从 agent 已结算的 NavMesh 路径 corners 生成琥珀色虚线，
    /// 拐点做 Catmull-Rom 平滑；与编辑器 builder 烘焙的固定贝塞尔提示曲线无关。
    /// </summary>
    public partial class ActorNavigator
    {
        private LineRenderer _pathLine;

        /// <summary>按当前已结算路径刷新虚线；路径不足两点时隐藏。</summary>
        private void RefreshPathLine()
        {
            if (agent == null || _path == null || _path.corners.Length < 2)
            {
                HidePathLine();
                return;
            }
            EnsurePathLine();
            var smooth = BuildSmoothPoints(_path.corners);
            _pathLine.positionCount = smooth.Count;
            for (int i = 0; i < smooth.Count; i++) _pathLine.SetPosition(i, smooth[i]);
            _pathLine.gameObject.SetActive(true);
        }

        private void HidePathLine()
        {
            if (_pathLine != null) _pathLine.gameObject.SetActive(false);
        }

        private void EnsurePathLine()
        {
            if (_pathLine != null) return;
            var go = new GameObject("ActorRuntimePathLine");
            go.transform.SetParent(transform, false);
            _pathLine = go.AddComponent<LineRenderer>();
            _pathLine.useWorldSpace = true;
            _pathLine.startWidth = _pathLine.endWidth = 0.03f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _pathLine.material = new Material(shader) { color = PrevisPalette.AmberTrajectory };
        }

        private static List<Vector3> BuildSmoothPoints(Vector3[] corners)
        {
            var pts = new List<Vector3>();
            if (corners.Length == 2)
            {
                pts.Add(corners[0]); pts.Add(corners[1]);
                return pts;
            }
            const int subdiv = 6;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                var p0 = corners[Mathf.Max(i - 1, 0)];
                var p1 = corners[i];
                var p2 = corners[i + 1];
                var p3 = corners[Mathf.Min(i + 2, corners.Length - 1)];
                for (int s = 0; s < subdiv; s++)
                {
                    pts.Add(Catmull(p0, p1, p2, p3, s / (float)subdiv) + Vector3.up * 0.02f);
                }
            }
            pts.Add(corners[corners.Length - 1] + Vector3.up * 0.02f);
            return pts;
        }

        private static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
