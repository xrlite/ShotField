using System.Collections.Generic;
using ByteDance.PICO.IconConfigurator.Editor;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 一键执行 PICO Icon Configurator 3D 分层图标（含自动 SDF 距离场生成与 LauncherManifest 注入）
    /// 并绑定 System Splash Screen 启动图。
    /// </summary>
    public static class ShotFieldPicoIconApply
    {
        private const string BgPath = "Assets/Art/LauncherIcon/icon_bg.png";
        private const string Fg2Path = "Assets/Art/LauncherIcon/icon_fg2.png";
        private const string Fg3Path = "Assets/Art/LauncherIcon/icon_fg3.png";
        private const string SplashPath = "Assets/Art/Splash/splash_shotfield_1024.png";

        [MenuItem("ShotField/应用 PICO 3D 分层图标与启动图")]
        public static void ApplyAll()
        {
            EnsureReadable(BgPath);
            EnsureReadable(Fg2Path);
            EnsureReadable(Fg3Path);

            var store = new IconConfiguratorStateStore();
            var config = store.LoadOrCreateConfigAsset();
            config.LastMode = IconConfiguratorMode.Manual;

            SetLayer(config.Manual.Layers, 0, BgPath);
            SetLayer(config.Manual.Layers, 1, Fg2Path);
            SetLayer(config.Manual.Layers, 2, Fg3Path);

            config.Localizations.Clear();
            config.Localizations.Add(new LocalizationEntry { LocaleCode = "en-US", AppName = "ShotField" });
            config.Localizations.Add(new LocalizationEntry { LocaleCode = "zh-CN", AppName = "镜场 ShotField" });
            store.Save();

            var validator = new IconConfiguratorValidator();
            var vr = validator.Validate(config);
            if (!vr.CanApply)
            {
                foreach (var e in vr.GeneralErrors) Debug.LogError("[IconValidator] General: " + e);
                foreach (var kv in vr.LayerErrors) Debug.LogError("[IconValidator] Layer " + kv.Key + ": " + kv.Value);
                foreach (var kv in vr.LocalizationErrors) Debug.LogError("[IconValidator] Locale " + kv.Key + ": " + kv.Value);
                return;
            }

            var applyService = new IconApplyService(
                validator,
                new IconCompositePreviewService(),
                new List<IIconExportAdapter>
                {
                    new LayeredIconExportAdapter(),
                    new AndroidAppNameExportAdapter(),
                    new AndroidManifestExportAdapter()
                });

            var result = applyService.Apply(config, store.GetConfigGuid());
            ApplySplashScreen();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ShotFieldPicoIconApply] PICO 3D 分层图标与 Splash 应用完成，写入/更新文件数={result.OverwrittenPaths.Count}");
        }

        private static void EnsureReadable(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (!imp.isReadable || imp.textureCompression != TextureImporterCompression.Uncompressed))
            {
                imp.isReadable = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
        }

        private static void SetLayer(List<IconLayerConfig> layers, int idx, string assetPath)
        {
            while (layers.Count <= idx) layers.Add(new IconLayerConfig());
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            layers[idx].AssetPath = assetPath;
            layers[idx].AssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            layers[idx].Texture = tex;
        }

        private static void ApplySplashScreen()
        {
            var splashTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashPath);
            if (splashTex == null) { Debug.LogWarning("[ShotFieldPicoIconApply] 未找到启动图: " + SplashPath); return; }
            PlayerSettings.virtualRealitySplashScreen = splashTex;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.953f, 0.933f, 0.894f, 1f);
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var prop = so.FindProperty("androidSplashScreen");
            if (prop != null) { prop.objectReferenceValue = splashTex; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
