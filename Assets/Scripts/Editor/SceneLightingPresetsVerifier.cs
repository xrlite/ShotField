#if UNITY_EDITOR
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class SceneLightingPresetsVerifier
    {
        private const int Expected = 4;

        [MenuItem("ShotField/Verifiers/Run Lighting Presets Verifier")]
        public static void Run()
        {
            int passed = 0;
            var root = new GameObject("LightingVerifyRoot");
            var moonGo = new GameObject("WindowMoonlight");
            var spotGo = new GameObject("TableWarmLight");
            moonGo.transform.SetParent(root.transform);
            spotGo.transform.SetParent(root.transform);

            try
            {
                var moon = moonGo.AddComponent<Light>();
                moon.type = LightType.Directional;
                var spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;

                var presets = root.AddComponent<SceneLightingPresets>();
                presets.windowKeyLight = moon;
                presets.tableSpotLight = spot;

                presets.ApplyMood(StudioLightingMood.NightMystery);
                VerifyAssert.Require("NightMystery mood sets cool key + warm spot",
                    moon.color.b > moon.color.r && spot.intensity > 2.0f, ref passed);

                presets.ApplyMood(StudioLightingMood.MorningWarm);
                VerifyAssert.Require("MorningWarm mood elevates sunlight intensity",
                    moon.intensity > 1.3f && moon.color.r > 0.9f, ref passed);

                presets.ApplyMood(StudioLightingMood.InterviewStark);
                VerifyAssert.Require("InterviewStark mood tightens spotlight cone",
                    spot.spotAngle < 45f && spot.intensity > 3.5f, ref passed);

                presets.CycleMood();
                VerifyAssert.Require("CycleMood wraps cleanly back to NightMystery",
                    presets.currentMood == StudioLightingMood.NightMystery, ref passed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            VerifyAssert.Finish("LIGHTING-PRESETS-RESULT", passed, Expected);
        }
    }
}
#endif
