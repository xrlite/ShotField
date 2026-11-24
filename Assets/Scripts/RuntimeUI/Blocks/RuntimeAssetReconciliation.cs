using System.Globalization;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>运行时资产对账：只记录导入后实际测得的几何，不预填估计值。</summary>
    public sealed class RuntimeAssetReconciliation
    {
        public string source;
        public string assetId;
        public string assetPath;
        public string state;
        public int triangles;
        public Bounds bounds;
        public bool measured;

        public static RuntimeAssetReconciliation Inspect(GameObject model, string source,
            string assetId, string assetPath, string state)
        {
            var record = new RuntimeAssetReconciliation
            {
                source = source, assetId = assetId, assetPath = assetPath, state = state
            };
            if (model == null) return record;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return record;
            Physics.SyncTransforms();
            record.bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) record.bounds.Encapsulate(renderers[i].bounds);
            for (int i = 0; i < renderers.Length; i++)
            {
                var filter = renderers[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    record.triangles += filter.sharedMesh.triangles.Length / 3;
            }
            Vector3 size = record.bounds.size;
            record.measured = record.triangles > 0 && IsFinitePositive(size.x) &&
                IsFinitePositive(size.y) && IsFinitePositive(size.z);
            return record;
        }

        public string SourceLabel => source == "tripo" ? "Tripo" : source == "assets" ? "现有资产" : "灰盒";

        public string Describe()
        {
            string geometry = measured
                ? $"{triangles.ToString("N0", CultureInfo.InvariantCulture)} tris | " +
                  $"{bounds.size.x:0.00}×{bounds.size.y:0.00}×{bounds.size.z:0.00} m"
                : "几何未测得";
            string path = string.IsNullOrEmpty(assetPath) ? "无文件路径" : assetPath;
            return $"{SourceLabel} | {state}\n{path}\n{geometry}";
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0.0001f;
    }
}
