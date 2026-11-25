using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 视野下方舒适区常驻胶囊栏（1.05m 距、下倾 13°）：
    /// 提供「进入 1:1 片场 ⇄ 回到 1:20 沙盘」与「重置面前」两个高频按钮，支持手柄射线/手势捏合/鼠标点击。
    /// </summary>
    public sealed class ScaleToggleCapsule : MonoBehaviour
    {
        private const float Distance = 1.05f;
        private const float DownOffset = -0.24f;
        private static readonly Color BgColor = new Color(0.10f, 0.12f, 0.15f, 0.94f);
        private static readonly Color GoldColor = new Color(0.79f, 0.66f, 0.38f, 1f);
        private static readonly Color SecondaryColor = new Color(0.22f, 0.25f, 0.30f, 1f);

        private TabletopDirector _tabletop;
        private StudioFadeTransition _fade;
        private Transform _head;
        private TMP_Text _modeLabel;
        private TabletopDirector.ScaleMode _lastMode = (TabletopDirector.ScaleMode)(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ScaleToggleCapsule>(FindObjectsInactive.Include) != null) return;
            if (Camera.main == null) return;
            var go = new GameObject("ScaleToggleCapsule",
                typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            go.AddComponent<ScaleToggleCapsule>().Build();
        }

        private void Build()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            _head = Camera.main != null ? Camera.main.transform : null;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(400f, 68f);
            rt.localScale = Vector3.one * 0.001f;

            RuntimeUiFactory.CreatePanel(rt, "CapsuleBg", BgColor, Vector2.zero, rt.sizeDelta);
            _modeLabel = CreateButton(rt, "ToggleScaleBtn", "进入 1:1 片场", GoldColor, new Color(0.10f, 0.11f, 0.14f, 1f),
                new Vector2(-62f, 0f), new Vector2(236f, 48f), OnToggleScaleClicked);
            CreateButton(rt, "RecenterBtn", "重置面前", SecondaryColor, Color.white,
                new Vector2(128f, 0f), new Vector2(116f, 48f), OnRecenterClicked);

            PlaceInComfortZone();
        }

        private TMP_Text CreateButton(RectTransform root, string name, string text, Color bg, Color fg,
            Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = RuntimeUiFactory.CreatePanel(root, name, bg, pos, size);
            var btn = go.AddComponent<Button>();
            var lbl = RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", text, 17, Vector2.zero, size, fg, FontStyle.Bold);
            btn.onClick.AddListener(onClick);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 12f);
            var xri = go.AddComponent<XRSimpleInteractable>();
            xri.selectEntered.AddListener(_ => onClick());
            go.AddComponent<UguiHoverEffect>();
            return lbl;
        }

        private void OnToggleScaleClicked()
        {
            ResolveRefs();
            if (_fade != null) { _fade.ToggleMode(); return; }
            if (_tabletop != null)
            {
                var next = _tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini
                    ? TabletopDirector.ScaleMode.Immersive1to1
                    : TabletopDirector.ScaleMode.TabletopMini;
                _tabletop.SwitchMode(next);
            }
        }

        private void OnRecenterClicked()
        {
            ResolveRefs();
            if (_tabletop == null || _head == null) return;
            Vector3 fwd = new Vector3(_head.forward.x, 0f, _head.forward.z);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            _tabletop.TabletopAnchor.position = _head.position + fwd.normalized * 0.90f;
            _tabletop.TabletopAnchor.position = new Vector3(_tabletop.TabletopAnchor.position.x,
                Mathf.Max(0.65f, _head.position.y - 0.65f), _tabletop.TabletopAnchor.position.z);
        }

        private void LateUpdate()
        {
            ResolveRefs();
            if (_tabletop != null && _tabletop.currentMode != _lastMode)
            {
                _lastMode = _tabletop.currentMode;
                if (_modeLabel != null)
                    _modeLabel.text = _lastMode == TabletopDirector.ScaleMode.TabletopMini ? "进入 1:1 片场" : "回到 1:20 沙盘";
            }
            PlaceInComfortZone();
        }

        private void PlaceInComfortZone()
        {
            if (_head == null) return;
            Vector3 fwd = new Vector3(_head.forward.x, 0f, _head.forward.z);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 target = _head.position + fwd * Distance + Vector3.up * DownOffset;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 8f);
            Vector3 look = transform.position - _head.position;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private void ResolveRefs()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_tabletop == null) _tabletop = FindAnyObjectByType<TabletopDirector>();
            if (_fade == null) _fade = FindAnyObjectByType<StudioFadeTransition>();
        }
    }
}
