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

        private readonly Vector3[] _frustumPts = new Vector3[11];
        private readonly Vector3[] _headlightPts = new Vector3[12];
        private readonly Vector3[] _eyePtsA = new Vector3[2];
        private readonly Vector3[] _eyePtsB = new Vector3[2];

        void Awake()
        {
            EnsureLineRenderers();
            AutoWireReferences();
        }

        void LateUpdate() => RefreshProbes();

        public void AutoWireReferences()
        {
            if (targetCamera == null)
            {
                var camRec = FindAnyObjectByType<CameraMotionRecorder>();
                targetCamera = (camRec != null && camRec.filmCamera != null) ? camRec.filmCamera : Camera.main;
            }
            if (actorA == null) actorA = GameObject.Find("SC01_Performer_A")?.GetComponent<ActorNavigator>();
            if (actorB == null) actorB = GameObject.Find("SC01_Performer_B")?.GetComponent<ActorNavigator>();
            if (vehicleNavigator == null) vehicleNavigator = FindAnyObjectByType<VehiclePathNavigator>();
        }

        public void EnsureLineRenderers()
        {
            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/Internal-Colored");
                if (shader != null) _lineMaterial = new Material(shader);
            }
            if (_frustumLine == null) _frustumLine = CreateLine("FrustumProbe", PrevisPalette.AmberTrajectory, 0.008f);
            if (_eyeLineA == null) _eyeLineA = CreateLine("EyeLineAProbe", PrevisPalette.SlateBlueGuideline, 0.006f);
            if (_eyeLineB == null) _eyeLineB = CreateLine("EyeLineBProbe", PrevisPalette.SlateBlueGuideline, 0.006f);
            if (_headlightLine == null) _headlightLine = CreateLine("HeadlightProbe", PrevisPalette.ColorOf("WindowMoonlight"), 0.007f);
        }

        private LineRenderer CreateLine(string name, Color color, float width)
        {
            var child = transform.Find(name);
            var go = child != null ? child.gameObject : new GameObject(name);
            if (child == null) go.transform.SetParent(transform, false);
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
            UpdateEyeLineProbe(actorA, _eyeLineA, _eyePtsA);
            UpdateEyeLineProbe(actorB, _eyeLineB, _eyePtsB);
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

            _frustumPts[0] = origin; _frustumPts[1] = c0; _frustumPts[2] = c1;
            _frustumPts[3] = origin; _frustumPts[4] = c2; _frustumPts[5] = c1;
            _frustumPts[6] = c2;     _frustumPts[7] = c3; _frustumPts[8] = origin;
            _frustumPts[9] = c3;     _frustumPts[10] = c0;

            if (_frustumLine.positionCount != 11) _frustumLine.positionCount = 11;
            _frustumLine.SetPositions(_frustumPts);
        }

        private void UpdateEyeLineProbe(ActorNavigator actor, LineRenderer lr, Vector3[] buffer)
        {
            if (lr == null) return;
            if (!showEyeLines || actor == null) { lr.positionCount = 0; return; }

            Vector3 eyeOrigin = actor.transform.position + Vector3.up * eyeHeight;
            Vector3 eyeEnd = eyeOrigin + actor.transform.forward * eyeLineDistance;

            buffer[0] = eyeOrigin;
            buffer[1] = eyeEnd;
            if (lr.positionCount != 2) lr.positionCount = 2;
            lr.SetPositions(buffer);
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

            Vector3 p0 = center + u; Vector3 p1 = center + r;
            Vector3 p2 = center - u; Vector3 p3 = center - r;

            _headlightPts[0] = origin; _headlightPts[1] = p0;  _headlightPts[2] = p1;
            _headlightPts[3] = origin; _headlightPts[4] = p2;  _headlightPts[5] = p3;
            _headlightPts[6] = origin; _headlightPts[7] = p0;  _headlightPts[8] = p1;
            _headlightPts[9] = p2;     _headlightPts[10] = p3; _headlightPts[11] = p0;

            if (_headlightLine.positionCount != 12) _headlightLine.positionCount = 12;
            _headlightLine.SetPositions(_headlightPts);
        }

        void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                if (Application.isPlaying) Destroy(_lineMaterial);
                else DestroyImmediate(_lineMaterial);
            }
        }
    }
}
