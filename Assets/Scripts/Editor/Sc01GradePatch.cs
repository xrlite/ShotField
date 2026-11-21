#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B3a 调色与曝光：把 -0.8 EV 的曝光补偿归零，并把 Flat 环境光换成立体三色。
    /// Flat 模式下朝下的天花板与朝上的地面一样亮 —— 这是"白模感"的物理源头。
    /// </summary>
    internal static class Sc01GradePatch
    {
        private const string VolumePath = "Assets/Models/Studio/SC01StudioVolume.asset";

        private static readonly Color Sky = new Color(0.72f, 0.70f, 0.66f);
        private static readonly Color Equator = new Color(0.56f, 0.53f, 0.48f);
        private static readonly Color Ground = new Color(0.26f, 0.24f, 0.21f);

        [MenuItem("ShotField/SC01/Patch B3a 曝光与环境光")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("Grade");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("GRADE");
        }

        internal static bool Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Sky;
            RenderSettings.ambientEquatorColor = Equator;
            RenderSettings.ambientGroundColor = Ground;
            RenderSettings.ambientIntensity = 1.0f;

            Sc01PatchCore.Check(RenderSettings.ambientMode == AmbientMode.Trilight, "B3.1 环境光模式 Trilight");
            Sc01PatchCore.Check(Sc01PatchCore.Near(RenderSettings.ambientSkyColor, Sky), "B3.2 天顶色",
                RenderSettings.ambientSkyColor.ToString());
            Sc01PatchCore.Check(Sc01PatchCore.Near(RenderSettings.ambientEquatorColor, Equator), "B3.3 侧面色");
            Sc01PatchCore.Check(Sc01PatchCore.Near(RenderSettings.ambientGroundColor, Ground), "B3.4 地面色");
            Sc01PatchCore.Check(Mathf.Abs(RenderSettings.ambientIntensity - 1f) < 0.001f, "B3.5 环境光强度 1.0",
                RenderSettings.ambientIntensity.ToString("F3"));
            Sc01PatchCore.Check(ResetExposure(), "B3.6 曝光补偿归零");
            return Sc01PatchCore.AllPassed;
        }

        private static bool ResetExposure()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(VolumePath))
            {
                var so = new SerializedObject(asset);
                var parameter = so.FindProperty("postExposure");
                if (parameter == null) continue;
                var value = parameter.FindPropertyRelative("m_Value");
                var overridden = parameter.FindPropertyRelative("m_OverrideState");
                if (value == null) continue;
                value.floatValue = 0f;
                if (overridden != null) overridden.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log("[SC01-PATCH] postExposure=0 on " + asset.name);
                return Mathf.Abs(value.floatValue) < 0.001f;
            }
            Debug.LogError("[SC01-PATCH] " + VolumePath + " 里找不到 postExposure（ColorAdjustments）");
            return false;
        }
    }
}
#endif
