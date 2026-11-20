#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 3D 影视灯具实体与空间光锥线框验证器（对齐 TLOU 参考图四叶遮光板与发光投射线框）。
    /// 确保场景内灯具具备 3D 白模、四叶遮扉、动态视锥计算与焦点跟随。
    /// </summary>
    public static class SpatialStudioLightVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Studio Light Fixture Verifier")]
        public static void Run()
        {
            int passed = 0;
            var hostGo = new GameObject("TestLightHost");
            var light = hostGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 40f;
            light.range = 5f;

            var targetGo = new GameObject("TestLightTarget");
            targetGo.transform.position = new Vector3(0f, 0f, 5f);

            try
            {
                var fixture = SpatialStudioLightFixture.AttachToLight(light, "TestFixture");
                VerifyAssert.Require("Studio fixture attached with Housing and 4 Barn Doors",
                    fixture != null && fixture.housing != null &&
                    fixture.barnDoorTop != null && fixture.barnDoorBottom != null &&
                    fixture.barnDoorLeft != null && fixture.barnDoorRight != null, ref passed);

                VerifyAssert.Require("Frustum LineRenderer configured with local-space 11 vertices",
                    fixture.frustumRenderer != null &&
                    !fixture.frustumRenderer.useWorldSpace &&
                    fixture.frustumRenderer.positionCount == 11, ref passed);

                fixture.UpdateFixture(30f, 4f, Color.red, targetGo.transform);
                float expectedSpread30 = Mathf.Tan(15f * Mathf.Deg2Rad) * 4f;
                Vector3 p1 = fixture.frustumRenderer.GetPosition(1); // TopLeft target corner
                VerifyAssert.Require("Frustum corner matches 30deg / 4m spread geometry",
                    Mathf.Abs(p1.z - 4f) < 0.05f &&
                    Mathf.Abs(p1.y - expectedSpread30) < 0.05f &&
                    Mathf.Abs(p1.x - (-expectedSpread30)) < 0.05f, ref passed);

                fixture.UpdateFixture(60f, 4f, Color.blue, targetGo.transform);
                float expectedSpread60 = Mathf.Tan(30f * Mathf.Deg2Rad) * 4f;
                Vector3 p1Updated = fixture.frustumRenderer.GetPosition(1);
                VerifyAssert.Require("Dynamic angle adjustment expands frustum spread correctly",
                    expectedSpread60 > expectedSpread30 &&
                    Mathf.Abs(p1Updated.y - expectedSpread60) < 0.05f, ref passed);

                var consoleGo = new GameObject("TestConsole");
                var console = consoleGo.AddComponent<SpatialLightingConsole>();
                console.InitializeDefaultSlots();
                console.slots[0].lightSource = light;
                console.slots[0].fixture = fixture;
                console.slots[0].focusTarget = targetGo.transform;
                console.SetInnerSpotAngle(25f);
                VerifyAssert.Require("Console SetInnerSpotAngle synchronizes with 3D studio fixture",
                    Mathf.Approximately(fixture.currentAngle, 25f), ref passed);

                targetGo.transform.position = new Vector3(3f, 1f, 4f);
                console.ApplySlot(console.slots[0]);
                Vector3 toTarget = (targetGo.transform.position - light.transform.position).normalized;
                float dot = Vector3.Dot(light.transform.forward, toTarget);
                VerifyAssert.Require("Focus target change redirects light & fixture orientation",
                    dot > 0.98f, ref passed);
            }
            finally
            {
                Object.DestroyImmediate(hostGo);
                Object.DestroyImmediate(targetGo);
                var cons = GameObject.Find("TestConsole");
                if (cons != null) Object.DestroyImmediate(cons);
            }

            VerifyAssert.Finish("STUDIO-LIGHT-FIXTURE", passed, Expected);
        }
    }
}
#endif
