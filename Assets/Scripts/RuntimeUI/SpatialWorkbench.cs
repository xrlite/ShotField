using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.RuntimeUI
{
    public class SpatialWorkbench : MonoBehaviour
    {
        [Header("核心引用")]
        public ShotLibrary library;
        public SpatialMonitor monitor;

        [Header("UI 文本组件")]
        public Text shotTitleText;
        public Text shotInfoText;
        public Text visibilityStatusText;
        public Text axisStatusText;
        public Text notificationText;

        [Header("质检状态指示卡")]
        public Image visibilityPill;
        public Image axisPill;

        [Header("空间按钮")]
        public Button prevBtn, nextBtn, newShotBtn, toggleAspectBtn, savePassportBtn, captureViewBtn;

        private int currentShotIndex = 0;
        private ShotConstraint[] rules;
        private readonly InputAction captureAction = new InputAction("CaptureView", InputActionType.Button);

        void Awake()
        {
            captureAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            captureAction.AddBinding("<Keyboard>/space");
        }

        void OnEnable() => captureAction.Enable();
        void OnDisable() => captureAction.Disable();
        void OnDestroy() => captureAction.Dispose();

        void Start()
        {
            if (library == null) library = Object.FindAnyObjectByType<ShotLibrary>();
            rules = Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Include);
            if (prevBtn != null) prevBtn.onClick.AddListener(PreviousShot);
            if (nextBtn != null) nextBtn.onClick.AddListener(NextShot);
            if (newShotBtn != null) newShotBtn.onClick.AddListener(CreateNewShot);
            if (captureViewBtn != null) captureViewBtn.onClick.AddListener(CaptureCurrentViewToShot);
            if (toggleAspectBtn != null && monitor != null) toggleAspectBtn.onClick.AddListener(ToggleAspect);
            if (savePassportBtn != null) savePassportBtn.onClick.AddListener(SavePassport);
            RefreshUI();
        }

        void LateUpdate()
        {
            if (!captureAction.WasPressedThisFrame()) return;
            var device = captureAction.activeControl?.device;
            if (device != null && !(device is Keyboard) && EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(device.deviceId)) return;
            CaptureCurrentViewToShot();
        }

        public void CaptureCurrentViewToShot()
        {
            var mainCam = Camera.main;
            if (library == null || mainCam == null || library.ShotCamera == null || mainCam == library.ShotCamera) return;
            library.ShotCamera.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            CreateNewShot();
            ShowNotify("[扳机捕获] 机位已同步至当前视线");
        }

        public void RefreshUI()
        {
            if (library == null || library.Count == 0)
            {
                WorkbenchStatus.UpdatePill(visibilityPill, visibilityStatusText, null, "信封");
                WorkbenchStatus.UpdatePill(axisPill, axisStatusText, null, "轴线");
                if (shotTitleText != null) shotTitleText.text = "暂无镜头";
                if (shotInfoText != null) shotInfoText.text = "点击下方按钮以当前机位创建分镜";
                return;
            }

            currentShotIndex = Mathf.Clamp(currentShotIndex, 0, library.Count - 1);
            var shot = library.Shots[currentShotIndex];
            if (monitor != null)
            {
                bool portrait = shot.camera.aspect < 1f;
                if (monitor.IsPortrait != portrait) monitor.SetAspect(portrait);
                monitor.RequestRender();
            }

            if (shotTitleText != null)
                shotTitleText.text = $"分镜 [{shot.shotId}] (第 {currentShotIndex + 1}/{library.Count} 镜)";

            if (shotInfoText != null)
                shotInfoText.text = $"意图: {shot.intent}\n机位: {shot.camera.position}";

            EvaluateConstraints(shot);
        }

        public void NextShot()
        {
            if (library == null || library.Count == 0) return;
            currentShotIndex = (currentShotIndex + 1) % library.Count;
            library.ApplyShot(library.Shots[currentShotIndex]);
            RefreshUI();
        }

        public void PreviousShot()
        {
            if (library == null || library.Count == 0) return;
            currentShotIndex = (currentShotIndex - 1 + library.Count) % library.Count;
            library.ApplyShot(library.Shots[currentShotIndex]);
            RefreshUI();
        }

        public void CreateNewShot()
        {
            if (library == null) return;
            var newShot = library.CreateShot(SceneCaptureMode.Current, "空间创建");
            currentShotIndex = library.Count - 1;
            ShowNotify($"已创建分镜: {newShot.shotId}");
            RefreshUI();
        }

        public void SavePassport()
        {
            if (library == null || library.Count == 0) return;
            var shot = library.Shots[currentShotIndex];
            try
            {
                EvaluateConstraints(shot);

                var specs = new System.Collections.Generic.List<ConstraintSpec>(rules.Length);
                foreach (var rule in rules)
                    if (IsApplicable(rule)) specs.Add(rule.ToSpec());
                shot.recordedConstraints = specs.ToArray();

                var dto = ShotPassportDto.FromShot(shot, library.SceneId, shot.recordedConstraints);
                var path = ShotPassportStore.SaveNextRevision(dto, ShotPassportStore.ProjectFolder);
                shot.revision = dto.revision;
                ShowNotify($"护照已保存: rev{dto.revision:D3}");
                Debug.Log($"[镜场 ShotField] 护照已存至: {path}");
            }
            catch (System.Exception ex)
            {
                ShowNotify($"保存失败: {ex.Message}");
                Debug.LogError($"[镜场 ShotField] 护照保存异常: {ex}");
            }
        }

        private void ToggleAspect()
        {
            monitor.ToggleAspect();
            if (library == null || library.Count == 0) return;
            var shot = library.Shots[currentShotIndex];
            shot.camera.aspect = monitor.IsPortrait ? ShotCameraState.PortraitAspect : ShotCameraState.DefaultAspect;
            RefreshUI();
        }

        private void EvaluateConstraints(ShotData shot)
        {
            shot.lastCheck.Clear();
            ConstraintResult vis = null, axis = null;
            var previous = currentShotIndex > 0 ? library.Shots[currentShotIndex - 1] : null;
            foreach (var rule in rules)
            {
                if (!IsApplicable(rule)) continue;
                var result = rule.Evaluate(shot, previous);
                shot.lastCheck.Add(result);
                if (rule is VisibilityConstraint && vis == null) vis = result;
                if (rule is AxisConstraint && axis == null) axis = result;
            }
            WorkbenchStatus.UpdatePill(visibilityPill, visibilityStatusText, vis?.status, "信封");
            WorkbenchStatus.UpdatePill(axisPill, axisStatusText, axis?.status, "轴线");
            shot.checkedAt = System.DateTimeOffset.UtcNow.ToString("O");
        }

        private bool IsApplicable(ShotConstraint rule) =>
            rule != null && rule.isActiveAndEnabled && rule.gameObject.scene == library.gameObject.scene;

        private void ShowNotify(string msg)
        {
            if (notificationText != null)
            {
                notificationText.text = msg;
                CancelInvoke(nameof(ClearNotify));
                Invoke(nameof(ClearNotify), 2.5f);
            }
        }

        private void ClearNotify()
        {
            if (notificationText != null) notificationText.text = "";
        }
    }
}
