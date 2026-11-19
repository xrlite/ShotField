using System.IO;
using ShotField.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 道具放置（M3「选道具 → 预览 → 落地」闭环）：选条目 → 半透明 Ghost → 右手射线命中面贴地吸附
    /// → AABB 与已有语义物体相交时变红 → 确认后实体化并补齐抓取组件。
    /// 预览源：PropBrowserUI 绑定引用优先，缺失时按路径名走 Resources（不用 Addressables，队长拍板）。
    /// PC 侧 Space 确认 / Esc 取消；头显内扳机绑定属 pending-真机。
    /// </summary>
    public class PropPlacementInteractor : MonoBehaviour
    {
        private static readonly Color NormalTint = new Color(0.35f, 0.85f, 1f, 0.32f);
        private static readonly Color BlockedTint = new Color(1f, 0.33f, 0.28f, 0.44f);

        public XRRayInteractor rayInteractor;
        public PropBrowserUI browser;
        public Transform spawnRoot;
        public float maxRayDistance = 8f;
        [Range(0.2f, 0.95f)] public float overlapShrink = 0.92f;

        private PropEntry _entry;
        private GameObject _ghost;
        private Renderer[] _ghostRenderers;
        private Material _ghostMaterial;
        private bool _placing;
        private bool _blocked;

        public bool IsPlacing => _placing;
        public bool IsBlocked => _blocked;
        public string PendingPropId => _entry != null ? _entry.id : null;
        public Vector3 GhostPosition => _ghost != null ? _ghost.transform.position : Vector3.zero;

        /// <summary>进入放置模式；取不到条目或 prefab 时返回 false。</summary>
        public bool BeginPlacement(string propId)
        {
            CancelPlacement();
            var entry = PropLibrary.Find(propId);
            var prefab = ResolvePrefab(entry);
            if (entry == null || prefab == null) return false;

            _entry = entry;
            _ghost = Instantiate(prefab, spawnRoot);
            _ghost.name = "Ghost_" + entry.id;
            _ghost.transform.rotation = Quaternion.identity;
            PropGhostVisualizer.StripGhost(_ghost);
            _ghostMaterial = PropGhostVisualizer.BuildGhostMaterial();
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            PropGhostVisualizer.ApplyGhost(_ghostRenderers, _ghostMaterial);
            _placing = true;
            return true;
        }

        /// <summary>确认落地：销毁 Ghost，在当前位置生成实体并补齐抓取三件套。</summary>
        public void ConfirmPlacement()
        {
            if (!_placing) return;
            var prefab = ResolvePrefab(_entry);
            var position = GhostPosition;
            var id = _entry.id;
            CancelPlacement();
            if (prefab == null) return;

            var solid = Instantiate(prefab, position, Quaternion.identity, spawnRoot);
            solid.name = id + "_placed";
            PropBrowserUI.EnsureGrabReady(solid, id);
        }

        public void CancelPlacement()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_ghostMaterial != null) Destroy(_ghostMaterial);
            _ghost = null; _ghostRenderers = null; _ghostMaterial = null;
            _entry = null; _placing = false; _blocked = false;
        }

        private void Update()
        {
            if (!_placing) return;
            FollowRay();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame && !_blocked) ConfirmPlacement();
            else if (keyboard.escapeKey.wasPressedThisFrame) CancelPlacement();
        }

        /// <summary>射线命中后同步水平位置，再把 Ghost 底面吸到命中点高度。</summary>
        private void FollowRay()
        {
            if (_ghost == null || rayInteractor == null) return;
            if (!rayInteractor.TryGetCurrent3DRaycastHit(out var hit)) return;
            if (hit.distance > maxRayDistance) return;

            var t = _ghost.transform;
            var pos = new Vector3(hit.point.x, t.position.y, hit.point.z);
            t.position = pos;
            pos.y += hit.point.y - ComputeBounds().min.y;
            t.position = pos;

            _blocked = HasObstruction();
            PropGhostVisualizer.SetTint(_ghostMaterial, _blocked ? BlockedTint : NormalTint);
        }

        private Bounds ComputeBounds()
        {
            var bounds = new Bounds(_ghost.transform.position, Vector3.zero);
            bool first = true;
            if (_ghostRenderers == null) return bounds;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                var r = _ghostRenderers[i];
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>只认带 SemanticMarker 的物体为障碍：地面/墙体不带标记，否则贴地必然自判相交。</summary>
        private bool HasObstruction()
        {
            var bounds = ComputeBounds();
            var hits = Physics.OverlapBox(bounds.center, bounds.extents * overlapShrink,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null || col.transform.IsChildOf(_ghost.transform)) continue;
                if (col.GetComponentInParent<SemanticMarker>() != null) return true;
            }
            return false;
        }

        private GameObject ResolvePrefab(PropEntry entry)
        {
            if (entry == null) return null;
            if (browser != null)
            {
                var bound = browser.FindPrefab(entry.id);
                if (bound != null) return bound;
            }
            if (string.IsNullOrEmpty(entry.prefabPath)) return null;
            var name = Path.GetFileNameWithoutExtension(entry.prefabPath);
            return Resources.Load<GameObject>("Props/" + name) ?? Resources.Load<GameObject>(name);
        }
    }
}
