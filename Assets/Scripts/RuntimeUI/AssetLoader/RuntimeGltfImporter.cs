using System;
using System.IO;
using System.Threading;
using Siccity.GLTFUtility;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Tripo 运行时 GLB 导入器：解析网格与骨骼层级，局部空间精确算力包围盒，
    /// 挂载 SkinnedMeshRenderer/Collider 与 NavMeshObstacle，并确保异步跨线程回调派发至主线程。
    /// </summary>
    public static class RuntimeGltfImporter
    {
        public static GameObject Import(string glbPath, Transform parent, Vector3 position,
            float targetHeight = 1.5f, string propId = "tripo") =>
            Import(glbPath, parent, position, out _, targetHeight, propId);

        public static GameObject Import(string glbPath, Transform parent, Vector3 position,
            out RuntimeAssetReconciliation reconciliation, float targetHeight = 1.5f,
            string propId = "tripo")
        {
            reconciliation = new RuntimeAssetReconciliation
                { source = "tripo", assetId = propId, assetPath = glbPath, state = "待导入" };
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
                throw new FileNotFoundException("GLB 不存在", glbPath);

            GameObject model;
            try { model = Importer.LoadFromFile(glbPath); }
            catch (Exception ex) { throw new InvalidDataException("GLB 导入失败: " + ex.Message, ex); }
            if (model == null) throw new InvalidDataException("GLB 导入返回空对象");

            model.name = Path.GetFileNameWithoutExtension(glbPath);
            model.transform.SetParent(parent, false);
            try
            {
                Normalize(model, position, targetHeight);
                EnsurePhysicsAndObstacle(model);
                PropBrowserUI.EnsureGrabReady(model, propId);
                reconciliation = RuntimeAssetReconciliation.Inspect(model, "tripo", propId, glbPath, "导入成功");
                if (!reconciliation.measured)
                    throw new InvalidDataException("GLB 几何对账失败: 面数或包围盒无效");
                return model;
            }
            catch
            {
                UnityEngine.Object.Destroy(model);
                throw;
            }
        }

        public static void ImportAsync(string glbPath, Transform parent, Vector3 position,
            Action<GameObject> onSuccess, Action<Exception> onError, float targetHeight = 1.5f,
            string propId = "tripo")
        {
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
            {
                onError?.Invoke(new FileNotFoundException("GLB 不存在", glbPath));
                return;
            }
            var mainCtx = SynchronizationContext.Current;
            Importer.LoadFromFileAsync(glbPath, new ImportSettings(), (model, animations) =>
            {
                void Complete()
                {
                    if (model == null) { onError?.Invoke(new InvalidDataException("GLB 导入返回空对象")); return; }
                    try
                    {
                        model.name = Path.GetFileNameWithoutExtension(glbPath);
                        model.transform.SetParent(parent, false);
                        Normalize(model, position, targetHeight);
                        EnsurePhysicsAndObstacle(model);
                        PropBrowserUI.EnsureGrabReady(model, propId);
                        var rec = RuntimeAssetReconciliation.Inspect(model, "tripo", propId, glbPath, "导入成功");
                        if (!rec.measured) throw new InvalidDataException("GLB 几何对账失败: 面数或包围盒无效");
                        onSuccess?.Invoke(model);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Object.Destroy(model);
                        onError?.Invoke(ex);
                    }
                }
                if (mainCtx != null && SynchronizationContext.Current != mainCtx)
                    mainCtx.Post(_ => Complete(), null);
                else Complete();
            });
        }

        public static void EnsurePhysicsAndObstacle(GameObject model)
        {
            var bounds = GetLocalBounds(model);
            var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                var capsule = model.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = model.AddComponent<CapsuleCollider>();
                capsule.center = bounds.center;
                capsule.height = Mathf.Max(0.2f, bounds.size.y);
                capsule.radius = Mathf.Max(0.05f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.4f);

                var obstacle = model.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = model.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = capsule.center;
                obstacle.height = capsule.height;
                obstacle.radius = capsule.radius;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
            else
            {
                if (model.GetComponentInChildren<Collider>() == null)
                {
                    var box = model.AddComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
                }
                var obstacle = model.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = model.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = bounds.center;
                obstacle.size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
        }

        public static Bounds GetLocalBounds(GameObject model)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>();
            var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (filters.Length == 0 && skinned.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.5f);

            Bounds b = new Bounds();
            bool hasInit = false;
            void EncapsulateLocal(Bounds src, Transform t)
            {
                var min = src.min; var max = src.max;
                Vector3[] pts = { new Vector3(min.x,min.y,min.z), new Vector3(min.x,min.y,max.z),
                    new Vector3(min.x,max.y,min.z), new Vector3(min.x,max.y,max.z),
                    new Vector3(max.x,min.y,min.z), new Vector3(max.x,min.y,max.z),
                    new Vector3(max.x,max.y,min.z), new Vector3(max.x,max.y,max.z) };
                for (int i = 0; i < 8; i++)
                {
                    var lp = model.transform.InverseTransformPoint(t.TransformPoint(pts[i]));
                    if (!hasInit) { b = new Bounds(lp, Vector3.zero); hasInit = true; }
                    else b.Encapsulate(lp);
                }
            }
            foreach (var mf in filters) if (mf.sharedMesh != null) EncapsulateLocal(mf.sharedMesh.bounds, mf.transform);
            foreach (var smr in skinned) if (smr.sharedMesh != null) EncapsulateLocal(smr.sharedMesh.bounds, smr.transform);
            return hasInit ? b : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        private static void Normalize(GameObject model, Vector3 position, float targetHeight)
        {
            var bounds = GetLocalBounds(model);
            if (bounds.size.y <= 0.0001f) throw new InvalidDataException("GLB 高度为 0");
            float scale = targetHeight / bounds.size.y;
            model.transform.localScale = new Vector3(scale, scale, scale);
            var scaledBounds = GetLocalBounds(model);
            model.transform.position = position - new Vector3(scaledBounds.center.x, scaledBounds.min.y, scaledBounds.center.z);
            Physics.SyncTransforms();
        }
    }
}
