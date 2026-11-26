using System.Collections.Generic;
using ByteDance.PICO.OpenXR;
using UnityEngine;
using UnityEngine.XR;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// MR 首屏：开启 PICO VST，轮询 Spatial Mesh 寻找桌面；失败时回退到虚拟创作台。
    /// 只使用当前已启用的 OpenXR feature，不引入 SpatialAdapter 包。
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class MrTabletopBootstrap : MonoBehaviour
    {
        [Header("绑定")]
        public TabletopDirector tabletop;
        public Transform headAnchor;

        [Header("虚拟创作台回退")]
        public float virtualDistance = 1.05f;
        public float virtualHeight = 0.75f;

        [Header("扫描")]
        public float scanInterval = 0.5f;
        public float scanTimeout = 8f;
        public float minPlaneWidth = 0.5f;
        public float maxPlaneWidth = 3f;
        public float minPlaneHeight = 0.4f;
        public float maxPlaneHeight = 1.2f;

        private readonly List<XRMeshSubsystem> _subsystems = new List<XRMeshSubsystem>();
        private readonly List<MeshInfo> _infos = new List<MeshInfo>();
        private readonly Dictionary<MeshId, Mesh> _meshes = new Dictionary<MeshId, Mesh>();
        private XRMeshSubsystem _subsystem;
        private float _startedAt;
        private float _nextScan;
        private bool _placed;

        public bool IsPlaced => _placed;
        public string Status { get; private set; } = "scanning";

        private void Awake()
        {
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
            if (headAnchor == null)
            {
                var cam = Camera.main;
                headAnchor = cam != null ? cam.transform : transform;
            }
            if (!Application.isEditor) PassthroughFeature.EnableVideoSeeThrough = true;
        }

        private void Start()
        {
            _startedAt = Time.time;
            _nextScan = Time.time;
            if (tabletop != null)
            {
                // 放在当前正前方 0.8m，避免左偏出视野导致闪烁
                var cam = Camera.main;
                if (cam != null && (headAnchor == null || headAnchor.position.y < 0.30f))
                    headAnchor = cam.transform;
                PlaceAnchorInFrontOfHead();
                tabletop.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            }
        }

        private void PlaceAnchorInFrontOfHead()
        {
            var forward = headAnchor != null ? headAnchor.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            Vector3 basePos = headAnchor != null ? headAnchor.position : Vector3.zero;
            tabletop.TabletopAnchor.position = basePos + forward.normalized * virtualDistance;
            tabletop.TabletopAnchor.position = new Vector3(tabletop.TabletopAnchor.position.x,
                virtualHeight, tabletop.TabletopAnchor.position.z);
        }

        private void Update()
        {
            if ((headAnchor == null || headAnchor.position.y < 0.30f) && Camera.main != null)
                headAnchor = Camera.main.transform;
            if (_placed) return;
            if (tabletop == null) return;
            if (Time.time - _startedAt >= scanTimeout)
            {
                UseFallback(_subsystem == null ? "no-mesh-subsystem" : "no-horizontal-plane");
                return;
            }
            if (_subsystem == null)
            {
                ResolveSubsystem();
                if (_subsystem == null) return;
                _subsystem.SetBoundingVolume(headAnchor.position, new Vector3(2f, 1.5f, 2f));
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + scanInterval;
            _infos.Clear();
            if (!_subsystem.TryGetMeshInfos(_infos)) return;

            foreach (var info in _infos)
            {
                if (info.ChangeState == MeshChangeState.Removed)
                {
                    if (_meshes.TryGetValue(info.MeshId, out var removed)) Destroy(removed);
                    _meshes.Remove(info.MeshId);
                    continue;
                }
                if (_meshes.ContainsKey(info.MeshId)) continue;
                var mesh = new Mesh { name = "MR_" + info.MeshId };
                _meshes.Add(info.MeshId, mesh);
                _subsystem.GenerateMeshAsync(info.MeshId, mesh, null,
                    MeshVertexAttributes.Normals,
                    result => OnMeshReady(info.MeshId, result));
            }

        }

        private void ResolveSubsystem()
        {
            _subsystems.Clear();
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var candidate in _subsystems)
                if (candidate.running) { _subsystem = candidate; break; }
        }

        private void OnMeshReady(MeshId id, MeshGenerationResult result)
        {
            if (!_meshes.TryGetValue(id, out var mesh) || _placed) return;
            if (result.Status != MeshGenerationStatus.Success) return;
            if (TryFindPlane(mesh, out var center, out var width))
            {
                tabletop.TabletopAnchor.position = center + Vector3.up * 0.02f;
                tabletop.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
                _placed = true;
                Status = "spatial-mesh";
                Debug.Log($"[MR-TABLETOP] center={center} width={width:F2}");
                Debug.Log("MR-TABLETOP-RESULT=PASS source=spatial-mesh");
            }
        }

        private bool TryFindPlane(Mesh mesh, out Vector3 bestCenter, out float bestWidth)
        {
            bestCenter = Vector3.zero;
            bestWidth = 0f;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float bestArea = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.magnitude < 0.0001f || normal.y / normal.magnitude < 0.9f) continue;
                var center = (a + b + c) / 3f;
                if (center.y < minPlaneHeight || center.y > maxPlaneHeight) continue;
                float distance = Vector3.Distance(center, headAnchor.position);
                if (distance < 0.4f || distance > 2.5f) continue;
                float width = Mathf.Max(Vector3.Distance(a, b), Vector3.Distance(b, c));
                if (width < minPlaneWidth || width > maxPlaneWidth) continue;
                float area = normal.magnitude * 0.5f;
                if (area <= bestArea) continue;
                bestArea = area;
                bestCenter = center;
                bestWidth = width;
            }
            return bestArea > 0f;
        }

        private void UseFallback(string reason)
        {
            PlaceAnchorInFrontOfHead();
            tabletop.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
            _placed = true;
            Status = reason;
            Debug.Log($"[MR-TABLETOP] fallback={reason} position={tabletop.TabletopAnchor.position}");
            Debug.Log($"MR-TABLETOP-RESULT=PENDING-DEVICE reason={reason}");
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes.Values) if (mesh != null) Destroy(mesh);
            if (!Application.isEditor) PassthroughFeature.EnableVideoSeeThrough = false;
        }
    }
}
