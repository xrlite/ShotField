using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>头显内联机 modal；从掌心菜单呼出，不占三块主工作区卡片。</summary>
    public sealed class VrNetworkPanel : MonoBehaviour
    {
        public NetworkSessionController controller;
        public Transform headAnchor;
        public bool Visible { get; private set; }
        public Transform PanelRoot => _root != null ? _root.transform : null;

        private GameObject _root;
        private Canvas _canvas;
        private TMP_InputField _addressInput;
        private TMP_Text _status;
        private bool _bound;

        private void Awake() => Build();

        public void Bind(NetworkSessionController value)
        {
            if (_bound && controller != null) controller.Changed -= Refresh;
            controller = value; _bound = controller != null;
            if (_bound) controller.Changed += Refresh;
            Refresh();
        }

        public void Toggle() => SetVisible(!Visible);

        public void SetVisible(bool visible)
        {
            if (_root == null) Build();
            ResolveDependencies();
            Visible = visible;
            if (_root != null) _root.SetActive(visible);
            if (visible) { Place(); Refresh(); }
        }

        private void Build()
        {
            _root = new GameObject("VrNetworkPanel", typeof(RectTransform), typeof(Canvas),
                typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
            _root.transform.SetParent(null, false);
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = Camera.main;
            _root.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.15f, 0.97f);
            var rt = (RectTransform)_root.transform;
            rt.sizeDelta = new Vector2(460f, 340f); rt.localScale = Vector3.one * 0.001f;
            RuntimeUiFactory.CreateTmpLabel(rt, "Title", "[测试] 联机", 26, new Vector2(0f, 140f),
                new Vector2(420f, 40f), Color.white);
            _status = RuntimeUiFactory.CreateTmpLabel(rt, "Status", "未连接", 14, new Vector2(0f, 104f),
                new Vector2(420f, 44f));
            RuntimeUiFactory.CreateTmpLabel(rt, "Hint", "主机 IPv4（同一局域网）", 13,
                new Vector2(0f, 72f), new Vector2(420f, 24f));
            _addressInput = RuntimeUiFactory.CreateTmpInputField(rt, "Address", "127.0.0.1",
                new Vector2(420f, 40f));
            _addressInput.transform.SetParent(rt, false);
            ((RectTransform)_addressInput.transform).anchoredPosition = new Vector2(0f, 38f);
            RuntimeUiFactory.CreateTmpButton(rt, "Host", "开局", new Vector2(-145f, -18f),
                new Vector2(130f, 42f), onClick: () => controller?.StartHost());
            RuntimeUiFactory.CreateTmpButton(rt, "Join", "加入", new Vector2(0f, -18f),
                new Vector2(130f, 42f), onClick: () => controller?.Join(_addressInput.text));
            RuntimeUiFactory.CreateTmpButton(rt, "Disconnect", "断开", new Vector2(145f, -18f),
                new Vector2(130f, 42f), onClick: () => controller?.Disconnect());
            RuntimeUiFactory.CreateTmpButton(rt, "ScanLan", "局域网发现", new Vector2(-105f, -76f),
                new Vector2(160f, 38f), onClick: ScanAndFillRoom);
            RuntimeUiFactory.CreateTmpButton(rt, "QuickJoin", "一键加入", new Vector2(65f, -76f),
                new Vector2(120f, 38f), onClick: ScanAndJoinRoom);
            RuntimeUiFactory.CreateTmpButton(rt, "Close", "关闭", new Vector2(175f, -76f),
                new Vector2(80f, 38f), onClick: () => SetVisible(false));
            RuntimeUiFactory.CreateTmpLabel(rt, "Boundary", "真机双端同步仍需设备取证", 12,
                new Vector2(0f, -128f), new Vector2(420f, 26f), new Color(0.72f, 0.75f, 0.80f));
            _root.SetActive(false);
        }

        private void ScanAndFillRoom()
        {
            var rooms = controller?.ScanLanRooms();
            if (rooms == null || rooms.Count == 0) { if (_status != null) _status.text = "未发现局域网房间"; return; }
            _addressInput.text = rooms[0].hostIp;
            if (_status != null) _status.text = $"发现房间 {rooms[0].roomCode}: {rooms[0].hostIp}";
        }

        private void ScanAndJoinRoom()
        {
            ScanAndFillRoom();
            if (_addressInput != null && !string.IsNullOrEmpty(_addressInput.text))
                controller?.Join(_addressInput.text);
        }

        private void ResolveDependencies()
        {
            if (controller == null) controller = FindAnyObjectByType<NetworkSessionController>();
            if (!_bound && controller != null) Bind(controller);
            if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
        }

        private void Place()
        {
            if (headAnchor == null) return;
            _root.transform.position = headAnchor.position + headAnchor.forward * 0.8f - headAnchor.up * 0.05f;
            Vector3 away = _root.transform.position - headAnchor.position;
            _root.transform.rotation = Quaternion.LookRotation(away.normalized, headAnchor.up);
        }

        private void Refresh()
        {
            if (_status != null) _status.text = controller != null ? controller.Status : "未绑定联机会话";
        }

        private void LateUpdate()
        {
            if (!Visible) return;
            ResolveDependencies();
            Place();
        }

        private void OnDestroy()
        {
            if (_bound && controller != null) controller.Changed -= Refresh;
            if (_root != null)
            {
                if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root);
            }
        }
    }
}
