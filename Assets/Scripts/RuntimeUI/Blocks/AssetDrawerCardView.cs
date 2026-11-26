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
        public RuntimeAssetReconciliation LastReconciliation { get; private set; }
        public int CurrentActIndex => _scripts != null ? _scripts.CurrentActIndex : 0;
        private SpatialBlockCard _card;
        private PropBrowserUI _browser; private ScriptSelectionPanel _scriptPanel; private AssetDrawerScriptSection _scripts;
        private GameObject _graybox, _grayboxPanel, _assetsPanel, _tripoPanel;
        private TMP_InputField _pathInput; private TMP_Text _grayboxStatus, _assetsStatus, _tripoStatus; private Material _grayMaterial; private bool _built;
        public void Bind(SpatialBlockCard card, PropBrowserUI browser, ScriptSelectionPanel scriptPanel)
        {
            _card = card; _browser = browser; _scriptPanel = scriptPanel;
            if (_built) { _scripts?.Refresh(); return; }
            if (_card == null || _card.ContentRoot == null)
            { LastError = "AssetDrawerCardView 缺少 SpatialBlockCard.ContentRoot"; return; }
            Build(); _built = true; SelectSource("graybox"); _scripts?.Refresh();
        }
        private void Build()
        {
            var root = _card.ContentRoot; float w = _card.PixelSize.x - 32f; float q = w / 4f;
            RuntimeUiFactory.CreateTmpButton(root, "SourceGraybox", "灰盒", new Vector2(-1.5f * q, 145f),
                new Vector2(q - 8f, 44f), onClick: () => SelectSource("graybox"));
            RuntimeUiFactory.CreateTmpButton(root, "SourceAssets", "现有资产", new Vector2(-0.5f * q, 145f),
                new Vector2(q - 8f, 44f), onClick: () => SelectSource("assets"));
            RuntimeUiFactory.CreateTmpButton(root, "SourceTripo", "Tripo", new Vector2(0.5f * q, 145f),
                new Vector2(q - 8f, 44f), onClick: () => SelectSource("tripo"));
            RuntimeUiFactory.CreateTmpButton(root, "DemoProposal", "AI 提案", new Vector2(1.5f * q, 145f),
                new Vector2(q - 8f, 44f), onClick: ImportDemoProposal);
            _grayboxPanel = ModePanel(root, "GrayboxMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_grayboxPanel.transform, "Hint", "运行时 Unity primitive | 放置于 spawnAnchor 前方",
                14, new Vector2(0f, 62f), new Vector2(w - 24f, 30f));
            RuntimeUiFactory.CreateTmpButton(_grayboxPanel.transform, "GrayboxHalf", "生成 0.5 m 灰盒",
                new Vector2(-110f, 25f), new Vector2(200f, 52f), onClick: () => SpawnGraybox(0.5f));
            RuntimeUiFactory.CreateTmpButton(_grayboxPanel.transform, "GrayboxOne", "生成 1.0 m 灰盒",
                new Vector2(110f, 25f), new Vector2(200f, 52f), onClick: () => SpawnGraybox(1f));
            _grayboxStatus = Label(_grayboxPanel.transform, "Status", LastNote, -34f, w - 24f, 52f);
            _assetsPanel = ModePanel(root, "AssetsMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_assetsPanel.transform, "LibraryHint",
                $"PropLibrary.All | {PropLibrary.Count} 条真实登记；未绑定会明确报错",
                13, new Vector2(0f, 78f), new Vector2(w - 24f, 24f));
            var entries = PropLibrary.All; float cw = (w - 40f) / 6f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float x = (i % 6 - 2.5f) * (cw + 8f);
                float y = 55f - (i / 6) * 30f;
                RuntimeUiFactory.CreateTmpButton(_assetsPanel.transform, "Prop_" + entry.id, entry.displayName,
                    new Vector2(x, y), new Vector2(cw, 30f), onClick: () => SpawnExisting(entry.id));
            }
            _assetsStatus = Label(_assetsPanel.transform, "Status", LastNote, -40f, w - 24f, 40f);
            _tripoPanel = ModePanel(root, "TripoMode", new Color(0.15f, 0.16f, 0.18f, 0.96f), w);
            RuntimeUiFactory.CreateTmpLabel(_tripoPanel.transform, "PathHint", "本地 GLB 路径（同步导入）",
                13, new Vector2(0f, 78f), new Vector2(w - 24f, 20f));
            _pathInput = RuntimeUiFactory.CreateTmpInputField(_tripoPanel.transform, "GlbPath",
                System.IO.Path.Combine(Application.persistentDataPath, "TripoModels", "door_v01.glb"),
                new Vector2(w - 24f, 40f), 1024);
            ((RectTransform)_pathInput.transform).anchoredPosition = new Vector2(0f, 42f);
            RuntimeUiFactory.CreateTmpButton(_tripoPanel.transform, "Import", "导入 Tripo GLB", new Vector2(0f, 10f),
                new Vector2(220f, 30f), onClick: () => ImportTripo(_pathInput.text));
            _tripoStatus = Label(_tripoPanel.transform, "Status", LastNote, -34f, w - 24f, 52f);
            var scriptRoot = RuntimeUiFactory.CreatePanel(root, "ScriptMode", new Color(0.12f, 0.13f, 0.16f, 0.97f), new Vector2(0f, -127f), new Vector2(w, 88f));
            _scripts = scriptRoot.AddComponent<AssetDrawerScriptSection>();
            _scripts.Bind(scriptRoot, _scriptPanel, w);
        }
        private static GameObject ModePanel(Transform root, string name, Color color, float width) => RuntimeUiFactory.CreatePanel(root, name, color, new Vector2(0f, 33f), new Vector2(width, 186f));
        private static TMP_Text Label(Transform parent, string name, string text, float y, float width, float height = 24f) =>
            RuntimeUiFactory.CreateTmpLabel(parent, name, text, 11, new Vector2(0f, y), new Vector2(width, height), new Color(0.72f, 0.75f, 0.80f));
        private void ImportDemoProposal()
        {
            var orchestrator = FindAnyObjectByType<ChainOrchestrator>();
            if (orchestrator == null) { LastError = "未找到 ChainOrchestrator"; Refresh(); return; }
            orchestrator.ImportDemoAndApply();
            LastError = ""; LastNote = "示例提案已送入四道闸"; Refresh();
        }

        public bool SelectSource(string source)
        {
            string key = (source ?? "").Trim().ToLowerInvariant();
            if (key == "灰盒") key = "graybox";
            if (key == "现有资产" || key == "assets" || key == "existing") key = "assets";
            if (key != "graybox" && key != "assets" && key != "tripo")
            { LastError = "未知资产来源: " + source; Refresh(); return false; }
            CurrentSource = key; LastReconciliation = null; LastError = "";
            LastNote = "来源: " + (key == "graybox" ? "灰盒" : key == "assets" ? "现有资产" : "Tripo"); Refresh(); return true;
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
                _graybox = go; CurrentSource = "graybox"; SpawnedCount++;
                LastReconciliation = RuntimeAssetReconciliation.Inspect(go, "graybox", "graybox", "Unity primitive", "生成成功");
                LastError = ""; LastNote = $"已生成 {size:0.0} m 灰盒"; Refresh(); return true;
            }
            catch (Exception ex) { LastError = ex.Message; Refresh(); return false; }
        }
        private void SpawnExisting(string propId)
        {
            var go = _browser != null ? _browser.SpawnProp(propId) : null;
            if (go == null) { LastError = "现有资产生成失败: " + propId; LastNote = "未生成"; }
            else
            {
                SpawnedCount++; LastError = ""; LastNote = "已放置真实资产: " + propId;
                var entry = PropLibrary.Find(propId);
                LastReconciliation = RuntimeAssetReconciliation.Inspect(go, "assets", propId,
                    entry != null ? entry.prefabPath : "运行时绑定", "放置成功");
            }
            Refresh();
        }
        public bool ImportTripo(string glbPath)
        {
            try
            {
                var model = RuntimeGltfImporter.Import(glbPath, _browser != null ? _browser.sceneRoot : null,
                    SpawnPosition(), out var record, 1.5f, "tripo");
                LastReconciliation = record;
                CurrentSource = "tripo"; SpawnedCount++; LastError = ""; LastNote = "Tripo 导入成功: " + model.name; Refresh(); return true;
            }
            catch (Exception ex)
            {
                FallbackCount++; LastError = ex.Message; CurrentSource = "graybox";
                bool ready = _graybox != null;
                if (!ready) ready = SpawnGraybox(1f);
                if (ready)
                {
                    _graybox.SetActive(true); LastNote = "Tripo 导入失败，已切回并保留/生成灰盒";
                    LastReconciliation = RuntimeAssetReconciliation.Inspect(_graybox, "graybox", "graybox", "Unity primitive", "导入失败回退");
                }
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
        public void SelectAct(int index) => _scripts?.SelectAct(index);
        public void NextAct() => _scripts?.NextAct();
        public void PreviousAct() => _scripts?.PreviousAct();
        private void Refresh()
        {
            if (!_built) return;
            _grayboxPanel.SetActive(CurrentSource == "graybox"); _assetsPanel.SetActive(CurrentSource == "assets"); _tripoPanel.SetActive(CurrentSource == "tripo");
            string status = LastReconciliation != null ? LastReconciliation.Describe() : LastNote;
            if (LastError != "") status += "\n错误: " + LastError;
            if (_grayboxStatus != null) _grayboxStatus.text = status;
            if (_assetsStatus != null) _assetsStatus.text = status;
            if (_tripoStatus != null) _tripoStatus.text = status;
        }
        private void OnDestroy() { if (_grayMaterial != null) Destroy(_grayMaterial); }
    }
}
