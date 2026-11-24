#if UNITY_EDITOR
using System.IO;
using ShotField.Core;
using ShotField.RuntimeUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.EditorTools
{
    /// <summary>对账 UI 必须展示导入后实测数据；失败只回退灰盒，不伪造 Tripo 成功。</summary>
    public static class AssetReconciliationVerifier
    {
        private const int Expected = 10;

        [MenuItem("ShotField/Verifiers/Run Asset Reconciliation Verifier")]
        public static void Run()
        {
            int passed = 0;
            var host = new GameObject("[AssetReconciliationVerifier]");
            try
            {
                var head = new GameObject("Head").transform;
                head.SetParent(host.transform, false); head.position = new Vector3(0f, 1.6f, 0f);
                var registry = host.AddComponent<BlockPanelRegistry>(); registry.headAnchor = head;
                var browser = host.AddComponent<PropBrowserUI>(); browser.sceneRoot = host.transform;
                var card = SpatialBlockCard.Create(host.transform, "drawer", "剧本 / 资产",
                    new Vector2(620f, 420f), 0, registry, head);
                var drawer = card.gameObject.AddComponent<AssetDrawerCardView>();
                drawer.Bind(card, browser, null);

                var empty = new GameObject("Empty");
                var unmeasured = RuntimeAssetReconciliation.Inspect(empty, "graybox", "empty", "", "无几何");
                Check("无 Renderer 不伪造面数或包围盒", !unmeasured.measured && unmeasured.triangles == 0, ref passed);
                Object.DestroyImmediate(empty);

                Check("灰盒生成真实落场", drawer.SpawnGraybox(0.5f), ref passed);
                var gray = drawer.LastReconciliation;
                Check("灰盒对账为 12 三角形 / 0.5m", gray != null && gray.measured && gray.triangles == 12 &&
                    Mathf.Abs(gray.bounds.size.x - 0.5f) < 0.001f && gray.assetPath == "Unity primitive", ref passed);

                var entry = PropLibrary.Find("sofa");
                var prefab = entry != null ? AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath) : null;
                browser.bindings.Add(new PropPrefabBinding { propId = "sofa", displayName = "沙发", prefab = prefab });
                drawer.SelectSource("assets");
                var propButton = card.transform.Find("Content/AssetsMode/Prop_sofa")?.GetComponent<Button>();
                if (propButton != null) propButton.onClick.Invoke();
                var existing = drawer.LastReconciliation;
                Check("现有资产对账路径与实测几何", existing != null && existing.source == "assets" &&
                    existing.assetPath == entry.prefabPath && existing.measured && existing.triangles > 0, ref passed);

                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                    "06_素材", "Tripo生成", "door_v01.glb"));
                Check("真实 Tripo GLB 导入", drawer.ImportTripo(path), ref passed);
                var tripo = drawer.LastReconciliation;
                Check("Tripo 对账含真实路径/面数/1.5m 包围盒", tripo != null && tripo.measured &&
                    tripo.assetPath == path && tripo.triangles > 0 && Mathf.Abs(tripo.bounds.size.y - 1.5f) < 0.05f, ref passed);
                var status = card.transform.Find("Content/TripoMode/Status")?.GetComponent<TMP_Text>();
                Check("抽屉状态可见路径、导入状态、面数与尺寸", status != null && status.text.Contains(path) &&
                    status.text.Contains("导入成功") && status.text.Contains("tris") && status.text.Contains(" m"), ref passed);

                drawer.SelectSource("tripo");
                Check("切换来源清空旧对账", drawer.LastReconciliation == null && drawer.LastError == "", ref passed);
                Check("缺失 GLB 回退且不伪造成功", !drawer.ImportTripo("missing_asset_reconciliation.glb") &&
                    drawer.CurrentSource == "graybox" && drawer.LastReconciliation.state == "导入失败回退" &&
                    drawer.LastReconciliation.measured && drawer.LastError != "", ref passed);
                int act = drawer.CurrentActIndex; drawer.NextAct();
                Check("拆出的五幕段仍可导航", drawer.CurrentActIndex == (act + 1) % 5, ref passed);
            }
            catch (System.Exception ex) { Debug.LogError("[ASSET-RECONCILIATION] " + ex); }
            finally { Object.DestroyImmediate(host); }
            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"ASSET-RECONCILIATION-RESULT={result} ({passed}/{Expected})");
            if (result == "FAIL" && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (ok) { passed++; Debug.Log($"[ASSET-RECONCILIATION] PASS: {label}"); }
            else Debug.LogError($"[ASSET-RECONCILIATION] FAIL: {label}");
        }
    }
}
#endif
