using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.RuntimeUI
{
    public partial class SpatialWorkbench : MonoBehaviour
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

        public Button prevBtn, nextBtn, newShotBtn, toggleAspectBtn, savePassportBtn, captureViewBtn;

        public Button playMotion, toggleBoard, playActor, screenShot;
        public event System.Action<ShotPassportDto> PassportSaved;
        public ShotData SelectedShot => library != null && library.Count > 0 ? library.Shots[Mathf.Clamp(currentShotIndex, 0, library.Count - 1)] : null;

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
            EnsureRuntimeButtons(GetComponent<Canvas>());
            var motion = GetComponent<CameraMotionRecorder>();
            var board = GetComponent<TabletopDirector>();
            var actor = GetComponent<ActorTakePlayer>();
            var lab = GetComponent<AudienceLab>();
            EnsureAdvancedControls(GetComponent<Canvas>());
            if (motion && playMotion) playMotion.onClick.AddListener(motion.StartPlayback);
            if (actor && playActor) playActor.onClick.AddListener(() => actor.StartPlayback());
            if (board && toggleBoard) toggleBoard.onClick.AddListener(() =>
            {
                board.SyncWorldToProxy(actor.actorTransform, board.tabletopProxyRoot);
                board.SwitchMode(board.currentMode == TabletopDirector.ScaleMode.TabletopMini
                    ? TabletopDirector.ScaleMode.Immersive1to1 : TabletopDirector.ScaleMode.TabletopMini);
            });
            if (lab && screenShot) screenShot.onClick.AddListener(() =>
            {
                if (lab.isScreeningActive) lab.StopScreeningSession();
                else { lab.StartScreeningSession(library.ShotCamera); motion.StartPlayback(); }
            });
        }

        void LateUpdate()
        {
            if (reviewProgressSlider != null && reviewPlayer != null)
                reviewProgressSlider.SetValueWithoutNotify(ReviewProgress);
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



        public void NextShot() => StepShot(1);
        public void PreviousShot() => StepShot(-1);

        private void StepShot(int direction)
        {
            if (SelectedShot == null) return;
            currentShotIndex = (currentShotIndex + direction + library.Count) % library.Count;
            library.ApplyShot(SelectedShot);
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

                var dto = ShotPassportMapper.FromShot(shot, library.SceneId, shot.recordedConstraints);
                var path = ShotPassportStore.SaveNextRevision(dto, ShotPassportStore.ProjectFolder);
                shot.revision = dto.revision;
                PassportSaved?.Invoke(dto);
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
