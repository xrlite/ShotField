#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>把天工 Android FBX 合并为单网格白模预设，去掉脚本与材质依赖。</summary>
    public static class TianGongPresetBuilder
    {
        private const string SourcePath = "Assets/Models/Scenes/TianGong_v05_Android.fbx";
        private const string MeshPath = "Assets/Models/Scenes/TianGong_v05_Android_Merged.asset";
        private const string PrefabDir = "Assets/Prefabs/Stages";
        private const string PrefabPath = PrefabDir + "/TianGong_Android.prefab";
        private const string MaterialPath = "Assets/Models/Studio/Materials/MAT_Previs_Prop_Gray.mat";

        [MenuItem("ShotField/Art/Build TianGong Android Preset")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) { Debug.LogError("TIANGONG-PRESET=FAIL source missing"); return; }
            var instance = Object.Instantiate(source);
            try
            {
                var filters = instance.GetComponentsInChildren<MeshFilter>()
                    .Where(f => f.sharedMesh != null).ToArray();
                var rootInverse = instance.transform.worldToLocalMatrix;
                var combine = filters.Select(f => new CombineInstance
                {
                    mesh = f.sharedMesh,
                    transform = rootInverse * f.transform.localToWorldMatrix
                }).ToArray();

                var merged = new Mesh
                {
                    name = "TianGong_Android_Merged",
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                merged.CombineMeshes(combine, true, true);
                merged.RecalculateBounds();
                Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
                AssetDatabase.CreateAsset(merged, MeshPath);

                var go = new GameObject("TianGong_Android");
                go.AddComponent<MeshFilter>().sharedMesh = merged;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                Directory.CreateDirectory(PrefabDir);
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Object.DestroyImmediate(go);
                AssetDatabase.SaveAssets();
                Debug.Log($"TIANGONG-PRESET=PASS renderers=1 triangles={merged.triangles.Length / 3}");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
#endif
