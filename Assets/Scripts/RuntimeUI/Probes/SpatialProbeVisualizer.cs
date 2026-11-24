using UnityEngine;
using ShotField.Core;
using ShotField.RuntimeUI.Tabletop;

namespace ShotField.RuntimeUI.Probes
{
    /// <summary>
    /// 空间探针渲染器：在片场实时呈现摄影机视锥网格、演员视线连线与载具大灯照明锥。
    /// 零 GC 单批次 LineRenderer 渲染，辅助导演直观判断视线对齐与构图。
    /// </summary>
    [ExecuteAlways]
    public class SpatialProbeVisualizer : MonoBehaviour
    {
        [Header("探针源绑定")]
        public Camera targetCamera;
        public ActorNavigator actorA;
        public ActorNavigator actorB;
        public VehiclePathNavigator vehicleNavigator;

        [Header("探针开关")]
        public bool showFrustum = true;
        public bool showEyeLines = true;
        public bool showHeadlight = true;

        [Header("探针尺寸")]
        public float frustumDistance = 2.5f;
        public float eyeLineDistance = 2.0f;
        public float eyeHeight = 1.55f;

        private LineRenderer _frustumLine;
        private LineRenderer _eyeLineA;
        private LineRenderer _eyeLineB;
        private LineRenderer _headlightLine;
        private Material _lineMaterial;

        void Awake() => EnsureLineRenderers();
        void LateUpdate() => RefreshProbes();

        public void EnsureLineRenderers()
        {
            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                _lineMaterial = new Material(shader);
            }
            if (_frustumLine == null) _frustumLine = CreateLine("FrustumProbe", PrevisPalette.AmberTrajectory, 0.008f);
            if (_eyeLineA == null) _eyeLineA = CreateLine("EyeLineAProbe", PrevisPalette.SlateBlueGuideline, 0.006f);
            if (_eyeLineB == null) _eyeLineB = CreateLine("EyeLineBProbe", PrevisPalette.SlateBlueGuideline, 0.006f);
            if (_headlightLine == null) _headlightLine = CreateLine("HeadlightProbe", PrevisPalette.ColorOf("WindowMoonlight"), 0.007f);
        }

        private LineRenderer CreateLine(string name, Color color, float width)
        {
            var go = transform.Find(name)?.gameObject;
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(transform, false);
            }
            var lr = go.GetComponent<LineRenderer>();
            if (lr == null) lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMaterial;
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            return lr;
        }

        public void RefreshProbes()
        {
            EnsureLineRenderers();
            UpdateFrustumProbe();
            UpdateEyeLineProbe(actorA, _eyeLineA);
            UpdateEyeLineProbe(actorB, _eyeLineB);
            UpdateHeadlightProbe();
        }

        private void UpdateFrustumProbe()
        {
            if (_frustumLine == null) return;
            if (!showFrustum || targetCamera == null) { _frustumLine.positionCount = 0; return; }

            Transform t = targetCamera.transform;
            Vector3 origin = t.position;
            float fovRad = targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float h = 2f * frustumDistance * Mathf.Tan(fovRad);
            float w = h * targetCamera.aspect;

            Vector3 center = origin + t.forward * frustumDistance;
            Vector3 u = t.up * (h * 0.5f);
            Vector3 r = t.right * (w * 0.5f);

            Vector3 c0 = center + u - r;
            Vector3 c1 = center + u + r;
            Vector3 c2 = center - u + r;
            Vector3 c3 = center - u - r;

            Vector3[] pts = new[] { origin, c0, c1, origin, c2, c1, c2, c3, origin, c3, c0 };
            _frustumLine.positionCount = pts.Length;
            _frustumLine.SetPositions(pts);
        }

        private void UpdateEyeLineProbe(ActorNavigator actor, LineRenderer lr)
        {
            if (lr == null) return;
            if (!showEyeLines || actor == null) { lr.positionCount = 0; return; }

            Vector3 eyeOrigin = actor.transform.position + Vector3.up * eyeHeight;
            Vector3 eyeEnd = eyeOrigin + actor.transform.forward * eyeLineDistance;

            lr.positionCount = 2;
            lr.SetPosition(0, eyeOrigin);
            lr.SetPosition(1, eyeEnd);
        }

        private void UpdateHeadlightProbe()
        {
            if (_headlightLine == null) return;
            if (!showHeadlight || vehicleNavigator == null || vehicleNavigator.headlightMount == null)
            {
                _headlightLine.positionCount = 0;
                return;
            }

            Transform mount = vehicleNavigator.headlightMount;
            Vector3 origin = mount.position;
            float range = vehicleNavigator.headlightRange;
            float radius = range * Mathf.Tan(vehicleNavigator.headlightSpotAngle * 0.5f * Mathf.Deg2Rad);

            Vector3 center = origin + mount.forward * range;
            Vector3 u = mount.up * radius;
            Vector3 r = mount.right * radius;

            Vector3 p0 = center + u;
            Vector3 p1 = center + r;
            Vector3 p2 = center - u;
            Vector3 p3 = center - r;

            Vector3[] pts = new[] { origin, p0, p1, origin, p2, p3, origin, p0, p1, p2, p3, p0 };
            _headlightLine.positionCount = pts.Length;
            _headlightLine.SetPositions(pts);
        }

        void OnDestroy()
        {
            if (_lineMaterial != null) DestroyImmediate(_lineMaterial);
        }
    }
}
