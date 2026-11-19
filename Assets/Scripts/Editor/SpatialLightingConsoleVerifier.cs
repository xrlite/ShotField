#if UNITY_EDITOR
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class SpatialLightingConsoleVerifier
    {
        private const int Expected = 8;

        [MenuItem("ShotField/Verifiers/Run Spatial Lighting Console Verifier")]
        public static void Run()
        {
            int passed = 0;
            var root = new GameObject("LightingConsoleVerifyRoot");
            var spotGo = new GameObject("SpotLight");
            var targetGo = new GameObject("TableTarget");
            spotGo.transform.SetParent(root.transform);
            targetGo.transform.SetParent(root.transform);
            targetGo.transform.position = new Vector3(1.1f, 0.45f, 2.3f);

            try
            {
                var lightComp = spotGo.AddComponent<Light>();
                lightComp.type = LightType.Spot;

                var console = root.AddComponent<SpatialLightingConsole>();
                console.InitializeDefaultSlots();

                VerifyAssert.Require("Console initializes 3 slots with default data",
                    console.slots.Length == 3 && console.CurrentSlot.slotName == "槽位1", ref passed);

                console.slots[0].lightSource = lightComp;
                console.slots[0].focusTarget = targetGo.transform;

                // 1. Inner spot angle adjustment
                console.SetInnerSpotAngle(10f);
                VerifyAssert.Require("InnerSpotAngle clamped and applied to light",
                    Mathf.Approximately(lightComp.spotAngle, 10f), ref passed);

                // 2. Mode cycling
                int initialMode = console.CurrentSlot.modeIndex;
                console.CycleMode(1);
                VerifyAssert.Require("CycleMode wraps cleanly through lighting modes",
                    console.CurrentSlot.modeIndex != initialMode, ref passed);

                // 3. Range adjustment
                console.SetRangePercent(0.60f);
                VerifyAssert.Require("Range percent updates light range & intensity",
                    lightComp.range > 0f && lightComp.intensity > 0f, ref passed);

                // 4. HSV Color calculation
                console.SetHue(0f);
                console.SetSaturation(0.11f);
                Color expectedColor = Color.HSVToRGB(0f, 0.11f, 1f);
                VerifyAssert.Require("Hue and Saturation correctly update light color",
                    Mathf.Abs(lightComp.color.r - expectedColor.r) < 0.05f &&
                    Mathf.Abs(lightComp.color.g - expectedColor.g) < 0.05f, ref passed);

                // 5. Focus Target tracking
                console.SetFocusTarget(targetGo.transform);
                Vector3 toTarget = (targetGo.transform.position - spotGo.transform.position).normalized;
                float dot = Vector3.Dot(spotGo.transform.forward, toTarget);
                VerifyAssert.Require("Focus target directs spot light forward vector",
                    dot > 0.95f, ref passed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // 7. 装配断言：SC01 场景里必须真的挂了这个控制台（前 6 条只是单元行为，不验装配）
            EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity", OpenSceneMode.Single);
            var inScene = UnityEngine.Object.FindAnyObjectByType<SpatialLightingConsole>();
            VerifyAssert.Require("Lighting console is assembled inside SC01_Blockout scene", inScene != null, ref passed);
            VerifyAssert.Require("Assembled console has 3 light slots bound",
                inScene != null && inScene.slots != null && inScene.slots.Length == 3 &&
                inScene.slots[0].lightSource != null && inScene.slots[1].lightSource != null &&
                inScene.slots[2].lightSource != null, ref passed);

            VerifyAssert.Finish("LIGHTING-CONSOLE-RESULT", passed, Expected);
        }
    }
}
#endif
