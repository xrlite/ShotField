using System;
using System.Collections.Generic;
using System.IO;
using ShotField.Core.Community;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>头显内创作资产社区面板：列出官方案例包并支持一键下载加载。</summary>
    public sealed class VrCommunityPanel : MonoBehaviour
    {
        public Transform headAnchor;
        public bool Visible { get; private set; }
        public Transform PanelRoot => _root != null ? _root.transform : null;

        private LocalCommunityAssetRepository _repo;
        private readonly List<CommunityAssetManifest> _items = new List<CommunityAssetManifest>();
        private GameObject _root;
        private TMP_Text _status;
        private readonly TMP_Text[] _slotLabels = new TMP_Text[3];

        private void Awake()
        {
            if (_repo == null) _repo = new LocalCommunityAssetRepository();
            Build();
        }

        public void Toggle() => SetVisible(!Visible);

        public void SetVisible(bool visible)
        {
            if (_root == null) Build();
            if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
            Visible = visible;
            if (_root != null) _root.SetActive(visible);
            if (visible) { Place(); RefreshCatalogAsync(); }
        }

        private void Build()
        {
            _root = new GameObject("VrCommunityPanel", typeof(RectTransform), typeof(Canvas),
                typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            SpatialGlass.Apply(_root.GetComponent<Image>(), new Color(0.96f, 0.93f, 0.86f, 0.70f), 32f);
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(480f, 360f); rt.localScale = Vector3.one * 0.001f;

            RuntimeUiFactory.CreateTmpLabel(rt, "Title", "社区案例库", 24, new Vector2(0f, 148f),
                new Vector2(456f, 36f), new Color(0.16f, 0.15f, 0.14f));
            _status = RuntimeUiFactory.CreateTmpLabel(rt, "Status", "点击下方案例一键加载", 14,
                new Vector2(0f, 112f), new Vector2(456f, 32f), new Color(0.25f, 0.24f, 0.22f));

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = RuntimeUiFactory.CreateTmpButton(rt, "Pkg_" + i, "加载中...",
                    new Vector2(0f, 52f - i * 54f), new Vector2(456f, 44f), onClick: () => LoadItemAsync(idx));
                SpatialGlass.Apply(btn.GetComponent<Image>(), new Color(0.30f, 0.29f, 0.27f, 0.86f), 20f);
                _slotLabels[i] = btn.GetComponentInChildren<TMP_Text>();
            }

            var refresh = RuntimeUiFactory.CreateTmpButton(rt, "Refresh", "刷新", new Vector2(-100f, -124f),
                new Vector2(140f, 38f), onClick: RefreshCatalogAsync);
            SpatialGlass.Apply(refresh.GetComponent<Image>(), new Color(0.30f, 0.29f, 0.27f, 0.86f), 20f);
            var close = RuntimeUiFactory.CreateTmpButton(rt, "Close", "关闭", new Vector2(100f, -124f),
                new Vector2(140f, 38f), onClick: () => SetVisible(false));
            SpatialGlass.Apply(close.GetComponent<Image>(), new Color(0.30f, 0.29f, 0.27f, 0.86f), 20f);
            _root.SetActive(false);
        }

        private async void RefreshCatalogAsync()
        {
            try
            {
                var list = await _repo.ListAsync(null);
                _items.Clear();
                if (list != null) _items.AddRange(list);
                for (int i = 0; i < _slotLabels.Length; i++)
                {
                    if (_slotLabels[i] == null) continue;
                    _slotLabels[i].text = i < _items.Count ? $"一键加载：{_items[i].title}" : "（空槽位）";
                }
                if (_status != null) _status.text = $"已扫描 {_items.Count} 套社区案例包";
            }
            catch (Exception ex) { if (_status != null) _status.text = "扫描失败: " + ex.Message; }
        }

        private async void LoadItemAsync(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            var item = _items[index];
            try
            {
                string destDir = Path.Combine(Application.persistentDataPath, "CommunityLoaded");
                string localPath = await _repo.DownloadPackageAsync(item.id, destDir);
                if (_status != null) _status.text = $"已加载 {item.title} -> {Path.GetFileName(localPath)}";
            }
            catch (Exception ex) { if (_status != null) _status.text = "加载失败: " + ex.Message; }
        }

        private void Place()
        {
            if (headAnchor == null) return;
            _root.transform.position = headAnchor.position + headAnchor.forward * 0.85f - headAnchor.up * 0.05f;
            Vector3 away = _root.transform.position - headAnchor.position;
            _root.transform.rotation = Quaternion.LookRotation(away.normalized, headAnchor.up);
        }

        private void LateUpdate() { if (Visible) Place(); }
        private void OnDestroy()
        {
            if (_root != null) { if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root); }
        }
    }
}
