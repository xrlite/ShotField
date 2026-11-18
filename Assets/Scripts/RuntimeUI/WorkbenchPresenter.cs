using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.RuntimeUI
{
    public partial class SpatialWorkbench
    {
        // 空间 UI 预留：由 SpatialWorkbenchBuilder 生成按钮对象后，BindHistoryButtons 自动接线
        public Text historyText;
        public Button undoBtn, redoBtn;

        private VideoComparisonPlayer reviewPlayer;
        private FiveActsProductionPipeline pipeline;
        private string aiStatus = "AI 故事圣经: 未运行";

        public string HistoryState => library == null
            ? "撤销栈 未知"
            : $"撤销 {library.UndoStack.UndoCount}/{library.UndoStack.Capacity} · 重做 {library.UndoStack.RedoCount}";

        public string AiState => aiStatus;
        public string ReviewState => reviewPlayer != null && reviewPlayer.IsPlaying ? "成片播放中" : "成片未加载";
        public string CompareModeName => reviewPlayer != null ? reviewPlayer.displayMode.ToString() : "未初始化";

        public float ReviewProgress => reviewPlayer != null && reviewPlayer.Duration > 0
            ? (float)(reviewPlayer.CurrentTime / reviewPlayer.Duration) : 0f;

        public void BindHistoryButtons()
        {
            if (undoBtn != null) undoBtn.onClick.AddListener(UndoShot);
            if (redoBtn != null) redoBtn.onClick.AddListener(RedoShot);
        }

        // 场景未预置按钮时在世界画布下补建，保证 Android 头显里也有可点的撤销/重做。
        // Builder 生成的按钮会被优先沿用，不重复创建、不重复绑定。
        public void EnsureRuntimeButtons(Canvas canvas)
        {
            if (canvas == null) return;
            if (undoBtn == null) undoBtn = MakeButton(canvas.transform, "UndoBtn", "撤销", new Vector2(-330f, -255f));
            if (redoBtn == null) redoBtn = MakeButton(canvas.transform, "RedoBtn", "重做", new Vector2(-220f, -255f));
            if (historyText == null) historyText = MakeText(canvas.transform, "HistoryText", new Vector2(0f, -255f));
            BindHistoryButtons();
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(100f, 45f);
            go.GetComponent<Image>().color = new Color(0.24f, 0.26f, 0.30f, 0.95f);
            MakeText(go.transform, "Label", Vector2.zero, label, new Vector2(100f, 45f));
            return go.GetComponent<Button>();
        }

        private static Text MakeText(Transform parent, string name, Vector2 pos,
            string label = "", Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size ?? new Vector2(320f, 30f);
            var text = go.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        public void UndoShot()
        {
            bool ok = library != null && library.Undo();
            RefreshUI();
            ShowNotify(ok ? "已撤销一步" : "无可撤销操作");
        }

        public void RedoShot()
        {
            bool ok = library != null && library.Redo();
            RefreshUI();
            ShowNotify(ok ? "已重做一步" : "无可重做操作");
        }

        public void ParseStoryBible()
        {
            if (pipeline == null) pipeline = new FiveActsProductionPipeline();
            var result = pipeline.ParseActScript(ProductionAct.Act2_Shooting);
            aiStatus = $"故事圣经: {result.sceneTitle} / {result.characters.Count} 角色 / {result.beats.Count} 节拍";
            ShowNotify(aiStatus);
        }

        public void ExportReferencePackage()
        {
            var shot = SelectedShot;
            if (shot == null) { ShowNotify("请先选择镜头"); return; }
            var path = Path.Combine(ShotPassportStore.ProjectFolder, "Exports",
                $"{shot.shotId}_rev{shot.revision:D3}.zip");
            try
            {
                ShotPackageExporter.ExportPackageToFile(shot, null, path);
                ShowNotify("参考包已导出: " + path);
            }
            catch (Exception ex) { ShowNotify("导出失败: " + ex.Message); }
        }

        public void PlayReviewVideo(string source)
        {
            var player = EnsureReview();
            if (!string.IsNullOrWhiteSpace(source)) player.LoadVideo(source);
            player.Play();
            ShowNotify("成片播放: " + (string.IsNullOrWhiteSpace(source) ? "当前源" : source));
        }

        public void CycleCompareMode()
        {
            var player = EnsureReview();
            var next = (ComparisonDisplayMode)(((int)player.displayMode + 1) % 3);
            player.SetMode(next);
            ShowNotify("比对模式: " + next);
        }

        public void SeekReview(float t)
        {
            if (reviewPlayer != null) reviewPlayer.SeekNormalized(t);
        }

        private VideoComparisonPlayer EnsureReview()
        {
            if (reviewPlayer == null) reviewPlayer = GetComponent<VideoComparisonPlayer>();
            if (reviewPlayer == null) reviewPlayer = gameObject.AddComponent<VideoComparisonPlayer>();
            return reviewPlayer;
        }

        public void RefreshUI()
        {
            if (historyText != null) historyText.text = HistoryState;
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
    }
}
