#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShotField.EditorTools
{
    /// <summary>用真实 Tripo 样本渲染资产对账 UI，并保存可回读的截图与实测 JSON。</summary>
    public static class AssetReconciliationEvidence
    {
        public static string OutputDirectory;
        [Serializable]
        internal sealed class BoundsReport
        {
            public float x, y, z;
        }

        [Serializable]
        internal sealed class Report
        {
            public string status, error, source, assetId, assetPath, state, screenshot, uiText;
            public bool measured;
            public int triangles;
            public BoundsReport boundsMeters;
            public string androidImport = "pending-APK";
        }

        [MenuItem("ShotField/Evidence/Capture Asset Reconciliation UI")]
        public static void Run()
        {
            string directory = OutputDirectory;
            if (string.IsNullOrEmpty(directory))
                directory = Environment.GetEnvironmentVariable("SHOTFIELD_ASSET_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                string month = Path.Combine(B5EvidenceSupport.FindEvidenceRoot(), DateTime.Now.ToString("yyyy-MM"));
                Directory.CreateDirectory(month);
                directory = Path.Combine(month, "asset_reconciliation_" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
            }
            Directory.CreateDirectory(directory);
            string screenshot = Path.Combine(directory, "asset_reconciliation_tripo.png");
            string reportPath = Path.Combine(directory, "asset_reconciliation_evidence.json");
            if (File.Exists(screenshot) || File.Exists(reportPath))
                throw new InvalidOperationException("Evidence files already exist; no files were overwritten.");

            var host = new GameObject("[AssetReconciliationEvidence]");
            try
            {
                var browser = host.AddComponent<PropBrowserUI>(); browser.sceneRoot = host.transform;
                var card = SpatialBlockCard.Create(host.transform, "drawer", "剧本 / 资产",
                    new Vector2(620f, 420f), 0, null, null);
                card.transform.localPosition = new Vector3(0f, 1.6f, 0.72f);
                card.transform.localRotation = Quaternion.identity;
                foreach (var child in card.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                card.gameObject.SetActive(true);
                var drawer = card.gameObject.AddComponent<AssetDrawerCardView>();
                drawer.Bind(card, browser, null);
                string glb = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                    "06_素材", "Tripo生成", "door_v01.glb"));
                if (!drawer.ImportTripo(glb)) throw new InvalidDataException(drawer.LastError);
                var record = drawer.LastReconciliation;
                var status = card.transform.Find("Content/TripoMode/Status")?.GetComponent<TMPro.TMP_Text>();
                if (status == null || !status.text.Contains(glb)) throw new InvalidDataException("对账 UI 未显示真实路径");

                var cameraGo = new GameObject("EvidenceCamera");
                cameraGo.transform.SetParent(host.transform, false);
                cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                cameraGo.transform.localRotation = Quaternion.identity;
                var camera = cameraGo.AddComponent<Camera>();
                camera.fieldOfView = 40f; camera.nearClipPlane = 0.01f; camera.farClipPlane = 20f;
                camera.cullingMask = 1 << 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
                B5EvidenceRenderer.RenderPng(camera, screenshot, 1240, 840);
                if (new FileInfo(screenshot).Length == 0) throw new InvalidDataException("截图为空");

                var report = new Report
                {
                    status = "PASS", source = record.source, assetId = record.assetId,
                    assetPath = record.assetPath, state = record.state, measured = record.measured,
                    triangles = record.triangles, screenshot = screenshot, uiText = status.text,
                    boundsMeters = new BoundsReport
                        { x = record.bounds.size.x, y = record.bounds.size.y, z = record.bounds.size.z }
                };
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log($"ASSET-RECONCILIATION-EVIDENCE=PASS triangles={record.triangles} path={screenshot}");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
