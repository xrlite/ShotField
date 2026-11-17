using UnityEngine;
using UnityEngine.UI;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间 3D 浮动中控工作台（Spatial 3D Workbench）。
    /// 遵循 PICO 空间多窗口交互规范，悬浮于三维片场空间中。
    /// 包含：当前镜头信息、时间轴切换、约束质检卡片、画幅切换与护照落盘。
    /// </summary>
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

        private static readonly Color PassGreen = new Color(0.32f, 0.77f, 0.10f, 0.95f);
        private static readonly Color FailRed = new Color(1.00f, 0.30f, 0.31f, 0.95f);
        private static readonly Color PendingAmber = new Color(1.00f, 0.75f, 0.10f, 0.95f);
        private static readonly Color NeutralMuted = new Color(0.20f, 0.22f, 0.25f, 0.85f);

        private int currentShotIndex = 0;
        private VisibilityConstraint cachedVisRule;
        private AxisConstraint cachedAxisRule;

        void Start()
        {
            if (library == null) library = Object.FindAnyObjectByType<ShotLibrary>();
            cachedVisRule = Object.FindAnyObjectByType<VisibilityConstraint>();
            cachedAxisRule = Object.FindAnyObjectByType<AxisConstraint>();
            if (prevBtn != null) prevBtn.onClick.AddListener(PreviousShot);
            if (nextBtn != null) nextBtn.onClick.AddListener(NextShot);
            if (newShotBtn != null) newShotBtn.onClick.AddListener(CreateNewShot);
            if (captureViewBtn != null) captureViewBtn.onClick.AddListener(CaptureCurrentViewToShot);
            if (toggleAspectBtn != null && monitor != null) toggleAspectBtn.onClick.AddListener(monitor.ToggleAspect);
            if (savePassportBtn != null) savePassportBtn.onClick.AddListener(SavePassport);
            RefreshUI();
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CaptureCurrentViewToShot();
            }
        }

        public void CaptureCurrentViewToShot()
        {
            if (library == null) return;
            var mainCam = Camera.main;
            if (mainCam != null && library.ShotCamera != null && mainCam != library.ShotCamera)
            {
                library.ShotCamera.transform.position = mainCam.transform.position;
                library.ShotCamera.transform.rotation = mainCam.transform.rotation;
            }
            CreateNewShot();
            ShowNotify("[扳机捕获] 机位已同步至当前视线");
        }

        public void RefreshUI()
        {
            if (library == null || library.Count == 0)
            {
                if (shotTitleText != null) shotTitleText.text = "暂无镜头";
                if (shotInfoText != null) shotInfoText.text = "点击下方按钮以当前机位创建分镜";
                return;
            }

            currentShotIndex = Mathf.Clamp(currentShotIndex, 0, library.Count - 1);
            var shot = library.Shots[currentShotIndex];

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
            var newShot = library.CreateShot(true, $"分镜 S0{library.Count + 1} (空间创建)", true);
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

                var rules = Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
                var specs = new ConstraintSpec[rules.Length];
                for (int i = 0; i < rules.Length; i++) specs[i] = rules[i].ToSpec();
                shot.recordedConstraints = specs;

                var shotDir = System.IO.Path.Combine(ShotPassportStore.ProjectFolder, shot.shotId);
                while (System.IO.File.Exists(System.IO.Path.Combine(shotDir, $"rev{shot.revision:D3}.json")))
                {
                    shot.revision++;
                }

                var dto = ShotPassportDto.FromShot(shot, library.SceneId, specs);
                var path = ShotPassportStore.Save(dto, ShotPassportStore.ProjectFolder);
                ShowNotify($"护照已保存: rev{dto.revision:D3}");
                Debug.Log($"[镜场 ShotField] 护照已存至: {path}");
            }
            catch (System.Exception ex)
            {
                ShowNotify($"保存失败: {ex.Message}");
                Debug.LogError($"[镜场 ShotField] 护照保存异常: {ex}");
            }
        }

        private void EvaluateConstraints(ShotData shot)
        {
            shot.lastCheck.Clear();
            if (cachedVisRule == null) cachedVisRule = Object.FindAnyObjectByType<VisibilityConstraint>();
            ConstraintResult visRes = null;
            if (cachedVisRule != null) { visRes = cachedVisRule.Evaluate(shot, null); shot.lastCheck.Add(visRes); }
            UpdatePill(visibilityPill, visibilityStatusText, visRes?.status, "信封正面不可见 [安全]", "信封正面泄漏 [违规报警!]", "信封遮挡 [待确认]", "信封规则 [未配置]");

            if (cachedAxisRule == null) cachedAxisRule = Object.FindAnyObjectByType<AxisConstraint>();
            ConstraintResult axisRes = null;
            if (cachedAxisRule != null)
            {
                var prev = currentShotIndex > 0 ? library.Shots[currentShotIndex - 1] : null;
                axisRes = cachedAxisRule.Evaluate(shot, prev);
                shot.lastCheck.Add(axisRes);
            }
            UpdatePill(axisPill, axisStatusText, axisRes?.status, "轴线连续性 [正常]", "发生 180° 越轴 [违规!]", "轴线关系 [待确认]", "轴线规则 [未配置]");
            shot.checkedAt = System.DateTimeOffset.UtcNow.ToString("O");
        }

        private void UpdatePill(Image pill, Text text, CheckStatus? status, string pass, string fail, string pend, string none)
        {
            if (text == null) return;
            if (!status.HasValue) { text.text = none; if (pill != null) pill.color = NeutralMuted; return; }
            switch (status.Value)
            {
                case CheckStatus.Pass: text.text = pass; if (pill != null) pill.color = PassGreen; break;
                case CheckStatus.Violation: text.text = fail; if (pill != null) pill.color = FailRed; break;
                case CheckStatus.Pending: text.text = pend; if (pill != null) pill.color = PendingAmber; break;
            }
        }

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
