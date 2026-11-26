using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        private const float DownOffset = -0.35f;
        private const float ImmersiveDrop = -0.52f;
        private const float ImmersiveDistance = 1.15f;
        private static readonly Color BgColor = new Color(0.96f, 0.93f, 0.86f, 0.68f);
        private static readonly Color GoldColor = new Color(0.79f, 0.66f, 0.38f, 1f);
        private static readonly Color SecondaryColor = new Color(0.22f, 0.25f, 0.30f, 1f);

        private TabletopDirector _tabletop;
        private StudioFadeTransition _fade;
        private Transform _head;
        private TMP_Text _modeLabel;
        private TMP_Text _stabLabel;
        private SteadicamDriver _steadicam;
        private TabletopDirector.ScaleMode _lastMode = (TabletopDirector.ScaleMode)(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EditorCameraFallback.Ensure();
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
            rt.sizeDelta = new Vector2(780f, 68f);
            rt.localScale = Vector3.one * 0.001f;

            var capBg = RuntimeUiFactory.CreatePanel(rt, "CapsuleBg", BgColor, Vector2.zero, rt.sizeDelta);
            SpatialGlass.Apply(capBg.GetComponent<Image>(), BgColor, 32f);
            capBg.GetComponent<Image>().raycastTarget = false;
            _modeLabel = CreateButton(rt, "ToggleScaleBtn", "进入 1:1 片场", GoldColor, new Color(0.10f, 0.11f, 0.14f, 1f),
                new Vector2(-260f, 0f), new Vector2(210f, 48f), OnToggleScaleClicked);
            // 三档稳定：讲稿第四幕的能力，此前只有 Editor 验证器在调用，运行时缺入口。
            _stabLabel = CreateButton(rt, "StabilizeBtn", "稳定:斯坦尼康", SecondaryColor, Color.white,
                new Vector2(-80f, 0f), new Vector2(132f, 48f), OnStabilizeClicked);
            if (_stabLabel != null) _stabLabel.fontSize = 15f;
            CreateButton(rt, "RecenterBtn", "重置面前", SecondaryColor, Color.white,
                new Vector2(48f, 0f), new Vector2(106f, 48f), OnRecenterClicked);
            var dock = DirectorDockPanel.Create(rt);
            var settings = XrAiConnectionView.Create(rt);
            CreateButton(rt, "DockBtn", "导演工作台", GoldColor, new Color(0.10f, 0.11f, 0.14f, 1f),
                new Vector2(174f, 0f), new Vector2(126f, 48f), () => { if (XrAiConnectionView.IsAnyPanelOpen && !dock.IsOpen) settings.Toggle(); dock.Toggle(); });
            CreateButton(rt, "AiSettingsBtn", "AI 设置", SecondaryColor, Color.white,
                new Vector2(304f, 0f), new Vector2(114f, 48f), () => { if (dock.IsOpen) dock.Close(); settings.Toggle(); });

            PlaceInComfortZone();
        }

        private TMP_Text CreateButton(RectTransform root, string name, string text, Color bg, Color fg,
            Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var glassColor = new Color(bg.r, bg.g, bg.b, Mathf.Min(bg.a, 0.90f));
            var go = RuntimeUiFactory.CreatePanel(root, name, glassColor, pos, size);
            SpatialGlass.Apply(go.GetComponent<Image>(), glassColor, 20f);
            var btn = go.AddComponent<Button>();
            var lbl = RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", text, 17, Vector2.zero, size, fg, FontStyle.Bold);
            btn.onClick.AddListener(onClick);
            go.AddComponent<UguiHoverEffect>();
            return lbl;
        }

        private void OnToggleScaleClicked()
        {
            ResolveRefs();
            Debug.Log($"[UI-CLICK] ScaleToggleCapsule.ToggleScaleBtn currentMode={(_tabletop != null ? _tabletop.currentMode.ToString() : "null")}");
            if (_fade != null) { _fade.ToggleMode(); return; }
            if (_tabletop != null)
            {
                var next = _tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini
                    ? TabletopDirector.ScaleMode.Immersive1to1
                    : TabletopDirector.ScaleMode.TabletopMini;
                _tabletop.SwitchMode(next);
            }
        }

        private void OnStabilizeClicked()
        {
            ResolveRefs();
            if (_steadicam == null) _steadicam = FindAnyObjectByType<SteadicamDriver>();
            if (_steadicam == null) return;
            string label = _steadicam.CycleMode();
            if (_stabLabel != null) _stabLabel.text = "稳定:" + label;
            Debug.Log("[UI-CLICK] StabilizeBtn -> " + label);
        }

        private void OnRecenterClicked()
        {
            ResolveRefs();
            Debug.Log("[UI-CLICK] ScaleToggleCapsule.RecenterBtn clicked");
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
            // 1:1 首帧要让开茶几与信封：胶囊更靠下、稍退远，避免横在画面正中。
            bool immersive = _tabletop != null &&
                             _tabletop.currentMode == TabletopDirector.ScaleMode.Immersive1to1;
            float dist = immersive ? ImmersiveDistance : Distance;
            float drop = immersive ? ImmersiveDrop : DownOffset;
            Vector3 target = _head.position + fwd * dist + Vector3.up * drop;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 8f);
            Vector3 look = transform.position - _head.position;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private void ResolveRefs()
        {
            if ((_head == null || _head.position.y < 0.30f) && Camera.main != null)
                _head = Camera.main.transform;
            if (_tabletop == null) _tabletop = FindAnyObjectByType<TabletopDirector>();
            if (_fade == null) _fade = FindAnyObjectByType<StudioFadeTransition>();
        }
    }
}
