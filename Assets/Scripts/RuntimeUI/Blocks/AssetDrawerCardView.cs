using System;
using ShotField.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ShotField.RuntimeUI
{
    /// <summary>剧本/资产抽屉：灰盒、真实资产库与本地 Tripo GLB，失败不伪造成功。</summary>
    public sealed class AssetDrawerCardView : MonoBehaviour
    {
        public string CurrentSource { get; private set; } = "graybox";
        public int SpawnedCount { get; private set; }
        public int FallbackCount { get; private set; }
        public string LastError { get; private set; } = "";
        public string LastNote { get; private set; } = "默认灰盒";
        public int CurrentActIndex { get; private set; }
        private SpatialBlockCard _card;
        private PropBrowserUI _browser; private ScriptSelectionPanel _scriptPanel; private FiveActsProductionPipeline _pipeline;
        private GameObject _graybox, _grayboxPanel, _assetsPanel, _tripoPanel;
        private TMP_InputField _pathInput; private TMP_Text _grayboxStatus, _assetsStatus, _tripoStatus, _scriptStatus; private Material _grayMaterial; private bool _built;
        public void Bind(SpatialBlockCard card, PropBrowserUI browser, ScriptSelectionPanel scriptPanel)
        {
            _card = card; _browser = browser; _scriptPanel = scriptPanel;
            if (_built) { RefreshScript(); return; }
            if (_card == null || _card.ContentRoot == null)
            { LastError = "AssetDrawerCardView 缺少 SpatialBlockCard.ContentRoot"; return; }
            Build(); _built = true; SelectSource("graybox"); RefreshScript();
        }
        private void Build()
        {
            var root = _card.ContentRoot; float w = _card.PixelSize.x - 32f;
            RuntimeUiFactory.CreateTmpButton(root, "SourceGraybox", "灰盒", new Vector2(-w / 3f, 145f),
                new Vector2(w / 3f - 8f, 44f), onClick: () => SelectSource("graybox"));
            RuntimeUiFactory.CreateTmpButton(root, "SourceAssets", "现有资产", Vector2.zero,
                new Vector2(w / 3f - 8f, 44f), onClick: () => SelectSource("assets"));
            RuntimeUiFactory.CreateTmpButton(root, "SourceTripo", "Tripo", new Vector2(w / 3f, 145f),
                new Vector2(w / 3f - 8f, 44f), onClick: () => SelectSource("tripo"));
            _grayboxPanel = ModePanel(root, "GrayboxMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_grayboxPanel.transform, "Hint", "运行时 Unity primitive | 放置于 spawnAnchor 前方",
                14, new Vector2(0f, 62f), new Vector2(w - 24f, 30f));
            RuntimeUiFactory.CreateTmpButton(_grayboxPanel.transform, "GrayboxHalf", "生成 0.5 m 灰盒",
                new Vector2(-110f, 5f), new Vector2(200f, 52f), onClick: () => SpawnGraybox(0.5f));
            RuntimeUiFactory.CreateTmpButton(_grayboxPanel.transform, "GrayboxOne", "生成 1.0 m 灰盒",
                new Vector2(110f, 5f), new Vector2(200f, 52f), onClick: () => SpawnGraybox(1f));
            _grayboxStatus = Label(_grayboxPanel.transform, "Status", LastNote, -65f, w - 24f);
            _assetsPanel = ModePanel(root, "AssetsMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_assetsPanel.transform, "LibraryHint",
                $"PropLibrary.All | {PropLibrary.Count} 条真实登记；未绑定会明确报错",
                13, new Vector2(0f, 76f), new Vector2(w - 24f, 30f));
            var entries = PropLibrary.All; float cw = (w - 40f) / 6f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float x = (i % 6 - 2.5f) * (cw + 8f);
                float y = 36f - (i / 6) * 38f;
                RuntimeUiFactory.CreateTmpButton(_assetsPanel.transform, "Prop_" + entry.id, entry.displayName,
                    new Vector2(x, y), new Vector2(cw, 40f), onClick: () => SpawnExisting(entry.id));
            }
            _assetsStatus = Label(_assetsPanel.transform, "Status", LastNote, -76f, w - 24f);
            _tripoPanel = ModePanel(root, "TripoMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_tripoPanel.transform, "PathHint", "本地 GLB 路径（同步导入）",
                14, new Vector2(0f, 68f), new Vector2(w - 24f, 30f));
            _pathInput = InputField(_tripoPanel.transform, "GlbPath", System.IO.Path.Combine(Application.persistentDataPath, "TripoModels", "door_v01.glb"), w - 24f);
            ((RectTransform)_pathInput.transform).anchoredPosition = new Vector2(0f, 28f);
            RuntimeUiFactory.CreateTmpButton(_tripoPanel.transform, "Import", "导入 Tripo GLB", new Vector2(0f, -20f),
                new Vector2(220f, 44f), onClick: () => ImportTripo(_pathInput.text));
            _tripoStatus = Label(_tripoPanel.transform, "Status", LastNote, -66f, w - 24f);
            var scriptRoot = RuntimeUiFactory.CreatePanel(root, "ScriptMode", new Color(0.12f, 0.13f, 0.16f, 0.97f), new Vector2(0f, -127f), new Vector2(w, 88f));
            _scriptStatus = RuntimeUiFactory.CreateTmpLabel(scriptRoot.transform, "ActStatus", "",
                13, new Vector2(0f, 29f), new Vector2(w - 20f, 24f));
            for (int i = 0; i < 5; i++)
            {
                int act = i; float aw = (w - 40f) / 5f;
                RuntimeUiFactory.CreateTmpButton(scriptRoot.transform, "Act" + (i + 1), $"第{ToChinese(i + 1)}幕",
                    new Vector2((i - 2f) * (aw + 10f), -2f), new Vector2(aw, 28f), onClick: () => SelectAct(act));
            }
            RuntimeUiFactory.CreateTmpButton(scriptRoot.transform, "PreviousAct", "上一幕", new Vector2(-w / 4f, -34f),
                new Vector2(120f, 24f), onClick: PreviousAct);
            RuntimeUiFactory.CreateTmpButton(scriptRoot.transform, "NextAct", "下一幕", new Vector2(w / 4f, -34f),
                new Vector2(120f, 24f), onClick: NextAct);
        }
        private static GameObject ModePanel(Transform root, string name, Color color, float width) => RuntimeUiFactory.CreatePanel(root, name, color, new Vector2(0f, 32f), new Vector2(width, 184f));
        private static TMP_Text Label(Transform parent, string name, string text, float y, float width) => RuntimeUiFactory.CreateTmpLabel(parent, name, text, 13, new Vector2(0f, y), new Vector2(width, 24f), new Color(0.72f, 0.75f, 0.80f));
        private static string ToChinese(int n) => n == 1 ? "一" : n == 2 ? "二" : n == 3 ? "三" : n == 4 ? "四" : "五";
        private static TMP_InputField InputField(Transform parent, string name, string value, float width)
        {
            var go = RuntimeUiFactory.CreatePanel(parent, name, new Color(0.08f, 0.09f, 0.11f, 0.98f), Vector2.zero, new Vector2(width, 40f));
            var field = go.AddComponent<TMP_InputField>();
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/ShotFieldSansSC SDF");
            text.fontSize = 13; text.raycastTarget = false; text.alignment = TextAlignmentOptions.Left;
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(10f, 5f); rt.offsetMax = new Vector2(-10f, -5f);
            field.textComponent = text; field.lineType = TMP_InputField.LineType.SingleLine; field.characterLimit = 1024; field.text = value;
            return field;
        }
        public bool SelectSource(string source)
        {
            string key = (source ?? "").Trim().ToLowerInvariant();
            if (key == "灰盒") key = "graybox";
            if (key == "现有资产" || key == "assets" || key == "existing") key = "assets";
            if (key != "graybox" && key != "assets" && key != "tripo")
            { LastError = "未知资产来源: " + source; Refresh(); return false; }
            CurrentSource = key; LastNote = "来源: " + (key == "graybox" ? "灰盒" : key == "assets" ? "现有资产" : "Tripo"); Refresh(); return true;
        }
        public bool SpawnGraybox(float size)
        {
            if (Mathf.Abs(size - 0.5f) > 0.001f && Mathf.Abs(size - 1f) > 0.001f)
            { LastError = "灰盒尺寸仅支持 0.5 m 或 1.0 m"; Refresh(); return false; }
            try
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Graybox_" + (size == 0.5f ? "0p5m" : "1m") + "_" + (SpawnedCount + 1);
                go.transform.SetParent(_browser != null && _browser.sceneRoot != null ? _browser.sceneRoot : null, true);
                go.transform.position = SpawnPosition(); go.transform.localScale = Vector3.one * size;
                if (_grayMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    _grayMaterial = new Material(shader) { color = new Color(0.55f, 0.57f, 0.60f, 1f) };
                    if (_grayMaterial.HasProperty("_BaseColor")) _grayMaterial.SetColor("_BaseColor", _grayMaterial.color);
                }
                go.GetComponent<Renderer>().sharedMaterial = _grayMaterial;
                PropBrowserUI.EnsureGrabReady(go, "graybox");
                _graybox = go; CurrentSource = "graybox"; SpawnedCount++; LastError = ""; LastNote = $"已生成 {size:0.0} m 灰盒"; Refresh(); return true;
            }
            catch (Exception ex) { LastError = ex.Message; Refresh(); return false; }
        }
        private void SpawnExisting(string propId)
        {
            var go = _browser != null ? _browser.SpawnProp(propId) : null;
            if (go == null) { LastError = "现有资产生成失败: " + propId; LastNote = "未生成"; }
            else { SpawnedCount++; LastError = ""; LastNote = "已放置真实资产: " + propId; }
            Refresh();
        }
        public bool ImportTripo(string glbPath)
        {
            try
            {
                var model = RuntimeGltfImporter.Import(glbPath, _browser != null ? _browser.sceneRoot : null, SpawnPosition(), 1.5f, "tripo");
                if (model == null) throw new System.IO.InvalidDataException("GLB 导入返回空对象");
                CurrentSource = "tripo"; SpawnedCount++; LastError = ""; LastNote = "Tripo 导入成功: " + model.name; Refresh(); return true;
            }
            catch (Exception ex)
            {
                FallbackCount++; LastError = ex.Message; CurrentSource = "graybox";
                bool ready = _graybox != null;
                if (!ready) ready = SpawnGraybox(1f);
                if (ready) { _graybox.SetActive(true); LastNote = "Tripo 导入失败，已切回并保留/生成灰盒"; }
                else LastNote = "Tripo 导入失败，灰盒回退也失败";
                LastError = ex.Message + (ready ? "" : "；灰盒回退失败: " + LastError);
                Refresh(); return false;
            }
        }
        private Vector3 SpawnPosition()
        {
            var anchor = _browser != null ? _browser.spawnAnchor : null;
            var pos = anchor != null ? anchor.position + anchor.forward * 1.2f : new Vector3(0f, 0f, 1.2f);
            pos.y = Mathf.Max(0f, pos.y); return pos;
        }
        public void SelectAct(int index)
        {
            if (_scriptPanel != null) { _scriptPanel.SelectAct(index); CurrentActIndex = _scriptPanel.CurrentActIndex; }
            else { CurrentActIndex = Mathf.Clamp(index, 0, 4); EnsurePipeline(); }
            RefreshScript();
        }
        public void NextAct()
        {
            if (_scriptPanel != null) { _scriptPanel.NextAct(); CurrentActIndex = _scriptPanel.CurrentActIndex; RefreshScript(); }
            else SelectAct((CurrentActIndex + 1) % 5);
        }
        public void PreviousAct()
        {
            if (_scriptPanel != null) { _scriptPanel.PreviousAct(); CurrentActIndex = _scriptPanel.CurrentActIndex; RefreshScript(); }
            else SelectAct((CurrentActIndex + 4) % 5);
        }
        private void EnsurePipeline() { if (_pipeline == null) _pipeline = new FiveActsProductionPipeline(); }
        private void RefreshScript()
        {
            if (_scriptStatus == null) return;
            if (_scriptPanel != null)
            { CurrentActIndex = _scriptPanel.CurrentActIndex; _scriptStatus.text = "剧本面板已接入 | " + _scriptPanel.CurrentAct; }
            else
            {
                EnsurePipeline(); var desc = _pipeline.GetActDescriptor((ProductionAct)CurrentActIndex);
                _scriptStatus.text = "ScriptSelectionPanel 为空 | 本地五幕预览 | " + desc.actTitle;
            }
        }
        private void Refresh()
        {
            if (!_built) return;
            _grayboxPanel.SetActive(CurrentSource == "graybox"); _assetsPanel.SetActive(CurrentSource == "assets"); _tripoPanel.SetActive(CurrentSource == "tripo");
            if (_grayboxStatus != null) _grayboxStatus.text = LastNote + (LastError == "" ? "" : " | 错误: " + LastError);
            if (_assetsStatus != null) _assetsStatus.text = LastNote + (LastError == "" ? "" : " | 错误: " + LastError);
            if (_tripoStatus != null) _tripoStatus.text = LastNote + (LastError == "" ? "" : " | 错误: " + LastError);
        }
        private void OnDestroy() { if (_grayMaterial != null) Destroy(_grayMaterial); }
    }
}
