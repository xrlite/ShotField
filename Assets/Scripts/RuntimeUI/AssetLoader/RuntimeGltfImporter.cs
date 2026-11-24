using System;
using System.IO;
using Siccity.GLTFUtility;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// Tripo 运行时 GLB 导入器：解析网格与骨骼层级，挂载 SkinnedMeshRenderer、
    /// CapsuleCollider 与带 Carving 的 NavMeshObstacle，对静态道具自愈回退为 Box 碰撞体。
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
            Importer.LoadFromFileAsync(glbPath, new ImportSettings(), (model, animations) =>
            {
                if (model == null) { onError?.Invoke(new InvalidDataException("GLB 导入返回空对象")); return; }
                try
                {
                    model.name = Path.GetFileNameWithoutExtension(glbPath);
                    model.transform.SetParent(parent, false);
                    Normalize(model, position, targetHeight);
                    EnsurePhysicsAndObstacle(model);
                    PropBrowserUI.EnsureGrabReady(model, propId);
                    var record = RuntimeAssetReconciliation.Inspect(model, "tripo", propId, glbPath, "导入成功");
                    if (!record.measured)
                    {
                        UnityEngine.Object.Destroy(model);
                        throw new InvalidDataException("GLB 几何对账失败: 面数或包围盒无效");
                    }
                    onSuccess?.Invoke(model);
                }
                catch (Exception ex)
                {
                    UnityEngine.Object.Destroy(model);
                    onError?.Invoke(ex);
                }
            });
        }

        /// <summary>
        /// 挂载物理与寻路避障：若包含骨骼网格挂载胶囊体与胶囊避障，否则自愈回退为盒状碰撞体。
        /// 碰撞体与避障在局部坐标系下严格根据 lossyScale 换算以贴合真实渲染包围盒。
        /// </summary>
        public static void EnsurePhysicsAndObstacle(GameObject model)
        {
            var bounds = GetRenderBounds(model);
            var localCenter = model.transform.InverseTransformPoint(bounds.center);
            var lossy = model.transform.lossyScale;
            float sx = Mathf.Abs(lossy.x) > 0.0001f ? Mathf.Abs(lossy.x) : 1f;
            float sy = Mathf.Abs(lossy.y) > 0.0001f ? Mathf.Abs(lossy.y) : 1f;
            float sz = Mathf.Abs(lossy.z) > 0.0001f ? Mathf.Abs(lossy.z) : 1f;
            var localSize = new Vector3(bounds.size.x / sx, bounds.size.y / sy, bounds.size.z / sz);
            var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();

            if (skinned != null)
            {
                var capsule = model.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = model.AddComponent<CapsuleCollider>();
                capsule.center = localCenter;
                capsule.height = Mathf.Max(0.2f, localSize.y);
                capsule.radius = Mathf.Max(0.1f, Mathf.Max(localSize.x, localSize.z) * 0.4f);

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
                    box.center = localCenter;
                    box.size = Vector3.Max(localSize, Vector3.one * 0.1f);
                }

                var obstacle = model.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = model.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = localCenter;
                obstacle.size = Vector3.Max(localSize, Vector3.one * 0.1f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
        }

        private static Bounds GetRenderBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.one * 0.5f);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void Normalize(GameObject model, Vector3 position, float targetHeight)
        {
            var bounds = GetRenderBounds(model);
            if (bounds.size.y <= 0.0001f) throw new InvalidDataException("GLB 高度为 0");

            float scale = targetHeight / bounds.size.y;
            model.transform.localScale = new Vector3(scale, scale, scale);
            model.transform.position = position;
            Physics.SyncTransforms();

            bounds = GetRenderBounds(model);
            var offset = new Vector3(position.x - bounds.center.x, position.y - bounds.min.y,
                position.z - bounds.center.z);
            model.transform.position += offset;
            Physics.SyncTransforms();
        }
    }
}
