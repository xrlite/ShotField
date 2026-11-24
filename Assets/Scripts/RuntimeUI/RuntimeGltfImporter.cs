using System;
using System.IO;
using Siccity.GLTFUtility;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>Tripo GLB 的运行时导入入口；失败不替换已有灰盒。</summary>
    public static class RuntimeGltfImporter
    {
        public static GameObject Import(string glbPath, Transform parent, Vector3 position,
            float targetHeight = 1.5f, string propId = "tripo")
        {
            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
                throw new FileNotFoundException("GLB 不存在", glbPath);

            GameObject model;
            try { model = Importer.LoadFromFile(glbPath); }
            catch (Exception ex) { throw new InvalidDataException("GLB 导入失败: " + ex.Message, ex); }
            if (model == null) throw new InvalidDataException("GLB 导入返回空对象");

            model.name = Path.GetFileNameWithoutExtension(glbPath);
            model.transform.SetParent(parent, false);
            Normalize(model, position, targetHeight);
            PropBrowserUI.EnsureGrabReady(model, propId);
            return model;
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
                    PropBrowserUI.EnsureGrabReady(model, propId);
                    onSuccess?.Invoke(model);
                }
                catch (Exception ex) { onError?.Invoke(ex); }
            });
        }

        private static void Normalize(GameObject model, Vector3 position, float targetHeight)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidDataException("GLB 内没有 Renderer");
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.0001f) throw new InvalidDataException("GLB 高度为 0");

            float scale = targetHeight / bounds.size.y;
            model.transform.localScale = new Vector3(scale, scale, scale);
            model.transform.position = position;
            Physics.SyncTransforms();

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var offset = new Vector3(position.x - bounds.center.x, position.y - bounds.min.y,
                position.z - bounds.center.z);
            model.transform.position += offset;
            Physics.SyncTransforms();
        }
    }
}
