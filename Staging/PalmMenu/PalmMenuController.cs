using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 掌心菜单 VR 呈现层：世界空间 Canvas 附着左手（手腕/手柄锚点），跟随但朝向锁定头部，
    /// 不随手腕翻滚；右手射线 + 扳机点选（手柄路径；裸手捏合选择留待真机手追接入，见 README）。
    /// 逻辑全部在 <see cref="PalmMenuState"/>，本类只做挂载、跟随、建按钮、触感触发。
    /// 冻结裁定 §2.2：无手部追踪时用手柄菜单键打开同一个界面。编译 pending-集成。
    /// </summary>
    public sealed class PalmMenuController : MonoBehaviour
    {
        [Header("挂载位姿")]
        public Transform menuAnchor;          // 左手腕/左手柄挂点
        public Transform headAnchor;          // 头部，用于菜单朝向锁定
        public Vector3 localOffset = new Vector3(0f, -0.12f, 0.18f);
        [Range(0.5f, 20f)] public float followLerp = 8f;

        [Header("UI 引用")]
        public Canvas menuCanvas;
        public Transform buttonRoot;
        public Button buttonPrefab;
        public Text statusLabel;
        public string labelChildName = "Label";

        [Header("交互与触感")]
        public XRRayInteractor selectRay;
        public UnityEngine.XR.Interaction.Toolkit.XRBaseController leftController;
        public UnityEngine.XR.Interaction.Toolkit.XRBaseController rightController;

        [Header("业务引用（留空会在 Awake 自动查找）")]
        public PropBrowserUI browser;
        public TabletopDirector tabletop;

        private PalmMenuState _state;
        private PalmMenuHaptics _haptics;
        private readonly InputAction _toggleAction = new InputAction(
            "PalmMenuToggle", InputActionType.Button);
        private readonly List<Button> _spawnedButtons = new List<Button>();

        public PalmMenuState State => _state;
        private void Awake()
        {
            _state = new PalmMenuState
            {
                Browser = browser != null ? browser : FindAnyObjectByType<PropBrowserUI>(),
                Tabletop = tabletop != null ? tabletop : FindAnyObjectByType<TabletopDirector>()
            };
            _haptics = new PalmMenuHaptics(leftController, rightController);
            // 菜单键：menuPressed 为主、primaryButtonPressed 兜底；控制名需真机核验（见 README 存疑）。
            _toggleAction.AddBinding("<XRController>{LeftHand}/menuPressed");
            _toggleAction.AddBinding("<XRController>{LeftHand}/primaryButtonPressed");
            _state.Changed += Refresh;
            if (menuCanvas != null) menuCanvas.enabled = false;
        }

        private void OnEnable() => _toggleAction.Enable();

        private void OnDisable() => _toggleAction.Disable();

        private void OnDestroy()
        {
            _toggleAction.Dispose();
            if (_state != null) _state.Changed -= Refresh;
        }

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
            {
                if (_state.IsOpen) Close();
                else Open();
            }
        }

        private void LateUpdate()
        {
            if (_state == null || !_state.IsOpen) return;
            FollowAnchor();
        }

        private void Open()
        {
            _state.SetOpen(true);
            _haptics.PulseOpen();
            Refresh();
        }

        private void Close()
        {
            _state.SetOpen(false);
            _haptics.PulseClose();
            if (menuCanvas != null) menuCanvas.enabled = false;
        }

        private void Refresh()
        {
            if (menuCanvas != null) menuCanvas.enabled = _state.IsOpen;
            if (statusLabel != null) statusLabel.text = _state.Status;
            if (buttonRoot == null) return;

            ClearButtons();
            BuildTabRow();
            foreach (var entry in _state.BuildEntries())
                BuildEntryButton(entry);
        }

        private void ClearButtons()
        {
            foreach (var btn in _spawnedButtons)
                if (btn != null) Destroy(btn.gameObject);
            _spawnedButtons.Clear();
        }

        private void BuildTabRow()
        {
            BuildTabButton("演员", PalmMenuTab.Actors);
            BuildTabButton("道具", PalmMenuTab.Props);
            BuildTabButton("场景", PalmMenuTab.Scene);
        }

        private void BuildTabButton(string label, PalmMenuTab tab)
        {
            var btn = InstantiateButton(label);
            if (btn == null) return;
            btn.onClick.AddListener(() =>
            {
                _state.SelectTab(tab);
                _haptics.PulseSelect();
            });
        }

        private void BuildEntryButton(PalmMenuEntry entry)
        {
            var btn = InstantiateButton(entry.Label);
            if (btn == null) return;
            if (entry.Active) MarkActive(btn);
            btn.onClick.AddListener(() => OnEntryClicked(entry));
        }

        private Button InstantiateButton(string label)
        {
            if (buttonPrefab == null || buttonRoot == null) return null;
            var btn = Instantiate(buttonPrefab, buttonRoot, false);
            var text = FindLabel(btn.transform);
            if (text != null) text.text = label;
            _spawnedButtons.Add(btn);
            return btn;
        }

        private Text FindLabel(Transform root)
        {
            var child = root.Find(labelChildName);
            return child != null ? child.GetComponent<Text>() : root.GetComponentInChildren<Text>();
        }

        private static void MarkActive(Button btn)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = new Color(0.30f, 0.55f, 0.95f, 0.95f);
        }

        private void OnEntryClicked(PalmMenuEntry entry)
        {
            _haptics.PulseSelect();
            bool closesAfterExecute = _state.Execute(entry);
            _haptics.PulseConfirm();
            if (closesAfterExecute) Close();
            else Refresh();
        }

        private void FollowAnchor()
        {
            if (menuAnchor == null) return;
            Transform menu = menuCanvas != null ? menuCanvas.transform : transform;

            Vector3 targetPos = menuAnchor.TransformPoint(localOffset);
            menu.position = Vector3.Lerp(menu.position, targetPos, Time.deltaTime * followLerp);

            Quaternion targetRot = menuAnchor.rotation;
            if (headAnchor != null)
            {
                Vector3 forward = headAnchor.position - menu.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.001f)
                    targetRot = Quaternion.LookRotation(forward.normalized, headAnchor.up);
            }
            menu.rotation = Quaternion.Slerp(menu.rotation, targetRot, Time.deltaTime * followLerp);
        }
    }
}
