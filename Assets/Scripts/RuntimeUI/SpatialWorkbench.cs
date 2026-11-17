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
        public Button prevBtn;
        public Button nextBtn;
        public Button newShotBtn;
        public Button toggleAspectBtn;
        public Button savePassportBtn;

        private static readonly Color PassGreen = new Color(0.32f, 0.77f, 0.10f, 0.95f);
        private static readonly Color FailRed = new Color(1.00f, 0.30f, 0.31f, 0.95f);
        private static readonly Color NeutralMuted = new Color(0.20f, 0.22f, 0.25f, 0.85f);

        private int currentShotIndex = 0;

        void Start()
        {
            if (library == null) library = Object.FindAnyObjectByType<ShotLibrary>();

            if (prevBtn != null) prevBtn.onClick.AddListener(PreviousShot);
            if (nextBtn != null) nextBtn.onClick.AddListener(NextShot);
            if (newShotBtn != null) newShotBtn.onClick.AddListener(CreateNewShot);
            if (toggleAspectBtn != null && monitor != null) toggleAspectBtn.onClick.AddListener(monitor.ToggleAspect);
            if (savePassportBtn != null) savePassportBtn.onClick.AddListener(SavePassport);

            RefreshUI();
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
            var dto = ShotPassportDto.FromShot(shot, library.SceneId, null);
            var path = ShotPassportStore.Save(dto, ShotPassportStore.ProjectFolder);
            ShowNotify($"护照已保存: rev{dto.revision:D3}");
            Debug.Log($"[镜场 ShotField] 护照已存至: {path}");
        }

        private void EvaluateConstraints(ShotData shot)
        {
            var visRule = Object.FindAnyObjectByType<VisibilityConstraint>();
            if (visRule != null && visibilityStatusText != null)
            {
                var res = visRule.Evaluate(shot, null);
                bool pass = res.status == CheckStatus.Pass;
                visibilityStatusText.text = pass ? "信封正面不可见 [安全]" : "信封正面泄漏 [违规报警!]";
                if (visibilityPill != null) visibilityPill.color = pass ? PassGreen : FailRed;
            }

            var axisRule = Object.FindAnyObjectByType<AxisConstraint>();
            if (axisRule != null && axisStatusText != null)
            {
                var prev = currentShotIndex > 0 ? library.Shots[currentShotIndex - 1] : null;
                var res = axisRule.Evaluate(shot, prev);
                bool pass = res.status == CheckStatus.Pass;
                axisStatusText.text = pass ? "轴线连续性 [正常]" : "发生 180° 越轴 [违规!]";
                if (axisPill != null) axisPill.color = pass ? PassGreen : FailRed;
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
