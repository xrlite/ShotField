using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>AI 命令的 Gate 3 视觉预览与显式确认/取消；不执行任何场景写入。</summary>
    public sealed class SceneCommandPreviewPanel : MonoBehaviour
    {
        public Transform headAnchor;
        public bool Visible { get; private set; }
        public event Action OnConfirm;
        public event Action OnCancel;

        private GameObject _root;
        private TMP_Text _title, _detail;
        public Transform PanelRoot => _root != null ? _root.transform : null;

        private void Awake() => Build();

        public void Show(string title, string detail)
        {
            if (_root == null) Build();
            if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
            _title.text = title; _detail.text = detail;
            Visible = true; _root.SetActive(true); Place();
        }

        public void Hide()
        {
            Visible = false;
            if (_root != null) _root.SetActive(false);
        }

        private void Build()
        {
            _root = new GameObject("SceneCommandPreviewPanel", typeof(RectTransform), typeof(Canvas),
                typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
            _root.transform.SetParent(null, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            _root.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.15f, 0.98f);
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(520f, 300f); rt.localScale = Vector3.one * 0.001f;
            _title = RuntimeUiFactory.CreateTmpLabel(rt, "Title", "AI 场景命令", 25,
                new Vector2(0f, 116f), new Vector2(480f, 38f), Color.white);
            _detail = RuntimeUiFactory.CreateTmpLabel(rt, "Detail", "", 14,
                new Vector2(0f, 52f), new Vector2(480f, 96f));
            _detail.alignment = TextAlignmentOptions.Left;
            RuntimeUiFactory.CreateTmpButton(rt, "Confirm", "确认执行", new Vector2(-90f, -62f),
                new Vector2(160f, 44f), onClick: () => OnConfirm?.Invoke());
            RuntimeUiFactory.CreateTmpButton(rt, "Cancel", "取消", new Vector2(90f, -62f),
                new Vector2(160f, 44f), onClick: () => OnCancel?.Invoke());
            RuntimeUiFactory.CreateTmpLabel(rt, "GateNote", "Gate 3 / 4：确认前不写场景，执行后立即回读", 12,
                new Vector2(0f, -116f), new Vector2(480f, 28f), new Color(0.72f, 0.75f, 0.80f));
            _root.SetActive(false);
        }

        private void Place()
        {
            if (headAnchor == null || _root == null) return;
            _root.transform.position = headAnchor.position + headAnchor.forward * 0.85f - headAnchor.up * 0.08f;
            Vector3 away = _root.transform.position - headAnchor.position;
            _root.transform.rotation = Quaternion.LookRotation(away.normalized, headAnchor.up);
        }

        private void LateUpdate()
        {
            if (!Visible) return;
            if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
            Place();
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root);
            }
        }
    }
}
