using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;

namespace ShotField.Tests.EditMode
{
    [TestFixture]
    public class Tier1_Gate2And3Tests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown() => E2ETestHelpers.DestroyAllTracked(_created);

        [Test]
        public void F21_SceneOrbController_ExistsAndNoMultiCameraStencilPortalInSC01()
        {
            var orbType = E2ETestHelpers.FindTypeByName("SceneOrbController");
            Assert.IsNotNull(orbType, "F2.1: SceneOrbController contract type must exist");

            string scene = E2ETestHelpers.ReadProjectText("Assets/Scenes/SC01_Main.unity");
            Assert.IsFalse(scene.Contains("StencilPortalCamera"),
                "F2.1: Multi-camera Stencil Portal is forbidden on mobile XR");
        }

        [Test]
        public void F22_TabletopDirector_And_Aligner_RoundtripCoordinatesAndProvideFallback()
        {
            var host = new GameObject("TabletopMath_Test");
            _created.Add(host);
            var director = host.AddComponent<TabletopDirector>();
            director.miniScale = 0.05f;
            director.TabletopAnchor.SetPositionAndRotation(new Vector3(0.2f, 0.8f, 1.0f), Quaternion.Euler(0f, 45f, 0f));

            Vector3 world = new Vector3(1.8f, 1.2f, -0.4f);
            Vector3 mini = director.WorldToTabletop(world);
            Assert.Less(Vector3.Distance(world, director.TabletopToWorld(mini)), 0.001f);

            Quaternion wRot = Quaternion.Euler(12f, 80f, 0f);
            Quaternion mRot = director.WorldToTabletopRot(wRot);
            Assert.Less(Quaternion.Angle(wRot, director.TabletopToWorldRot(mRot)), 0.01f);

            var aligner = host.AddComponent<PicoPlaneTabletopAligner>();
            TabletopSurface fallback = aligner.CreateFallbackSurface();
            Assert.IsTrue(fallback.FacesUp);
            Assert.IsTrue(fallback.HasExtent);
            Assert.IsFalse(NullTabletopSurfaceProvider.Instance.TryGetSurface(out _));
        }

        [Test]
        public void F23_Sc01PrevisShotData_Evaluates7ShotsAnd60SecondTimeline()
        {
            Assert.AreEqual(7, Sc01PrevisShotData.Shots.Length);
            Assert.AreEqual(60.0f, Sc01PrevisShotData.TotalDuration, 0.001f);

            Sc01PrevisShotData.EvaluateCamera(20.0f, out var pos, out var rot, out float fov);
            Assert.Greater(fov, 20f);
            Assert.Less(fov, 90f);
            Assert.Greater(pos.magnitude, 0.01f);
            Assert.AreNotEqual(0f, rot.w * rot.w + rot.x * rot.x + rot.y * rot.y + rot.z * rot.z);

            var clip = new ActorPerformanceClip();
            clip.AddFrame(0f, Vector3.zero, Quaternion.identity, Vector3.up * 1.6f, Quaternion.identity,
                Vector3.left * 0.3f, Quaternion.identity, Vector3.right * 0.3f, Quaternion.identity);
            clip.AddFrame(2f, Vector3.forward * 2f, Quaternion.identity, Vector3.up * 1.6f + Vector3.forward * 2f,
                Quaternion.identity, Vector3.left * 0.3f, Quaternion.identity, Vector3.right * 0.3f, Quaternion.identity);

            var mid = clip.Evaluate(1f);
            Assert.AreEqual(1.0f, mid.rootPosition.z, 0.001f);
            Assert.AreEqual(2, clip.ToActorTake().keyframes.Count);
        }

        [Test]
        public void F24_MobileRPAsset_EnforcesMobileXrBudgetSettings()
        {
            string rpYaml = E2ETestHelpers.ReadProjectText("Assets/Settings/Mobile_RPAsset.asset");
            Assert.AreEqual("0", E2ETestHelpers.ExtractYamlValue(rpYaml, "m_RequireDepthTexture"),
                "F2.4: DepthTexture must be Off");
            Assert.AreEqual("0", E2ETestHelpers.ExtractYamlValue(rpYaml, "m_RequireOpaqueTexture"),
                "F2.4: OpaqueTexture must be Off");
            Assert.AreEqual("0", E2ETestHelpers.ExtractYamlValue(rpYaml, "m_SupportsHDR"),
                "F2.4: HDR must be Off for mobile XR bandwidth");

            string storeActions = E2ETestHelpers.ExtractYamlValue(rpYaml, "m_StoreActionsOptimization");
            Assert.IsTrue(storeActions == "0" || storeActions == "2",
                "F2.4: m_StoreActionsOptimization must be Auto (0) or Discard (2)");
        }

        [Test]
        public void F31_And_F32_PromoMp4AndGateLogsDirectoryContract()
        {
            string evidenceDir = @"F:\pico\05_验证记录\2026-11";
            Assert.IsTrue(Directory.Exists(evidenceDir), "Evidence directory 05_验证记录/2026-11 must exist");
            string[] mp4s = Directory.GetFiles(evidenceDir, "*.mp4", SearchOption.TopDirectoryOnly);
            Assert.GreaterOrEqual(mp4s.Length, 1,
                "F3.1: 45-second 1080p promo MP4 must be exported to 05_验证记录/2026-11/");
            Assert.Greater(new FileInfo(mp4s[0]).Length, 1024 * 100, "Promo MP4 must be non-trivial");
        }

        [Test]
        public void F33_BuildSettings_And_AssetsAsciiMetaCompliance()
        {
            string buildYaml = E2ETestHelpers.ReadProjectText("ProjectSettings/EditorBuildSettings.asset");
            var enabledScenes = Regex.Matches(buildYaml, @"enabled:\s*1\s*\r?\n\s*path:\s*(.+)");
            Assert.AreEqual(1, enabledScenes.Count, "Exactly 1 scene must be enabled in EditorBuildSettings");
            Assert.AreEqual("Assets/Scenes/SC01_Main.unity", enabledScenes[0].Groups[1].Value.Trim());

            string assetsRoot = E2ETestHelpers.AssetFullPath("Assets");
            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Substring(assetsRoot.Length);
                foreach (char ch in rel)
                    Assert.LessOrEqual((int)ch, 127, "F3.3: Non-ASCII path under Assets/: " + rel);
                Assert.IsTrue(File.Exists(file + ".meta"), "F3.3: Missing .meta for " + rel);
                int lines = File.ReadAllLines(file).Length;
                Assert.LessOrEqual(lines, 200, "F3.3: C# file exceeds 200 lines: " + rel);
            }
        }
    }
}