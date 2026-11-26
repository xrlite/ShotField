using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEngine;

namespace ShotField.Tests.EditMode
{
    [TestFixture]
    public class Tier1_Gate1VisualTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown() => E2ETestHelpers.DestroyAllTracked(_created);

        [Test]
        public void F11_SC01Main_IntegratesHallArtPrefabAndRemovesDuplicateWarmHall()
        {
            string hallMeta = E2ETestHelpers.ReadProjectText("Assets/Prefabs/Stages/HallArt.prefab.meta");
            string hallGuid = E2ETestHelpers.ExtractYamlValue(hallMeta, "guid");
            Assert.IsNotEmpty(hallGuid);

            string scene = E2ETestHelpers.ReadProjectText("Assets/Scenes/SC01_Main.unity");
            bool hasHallRef = scene.Contains(hallGuid) || scene.Contains("m_Name: HallArt");
            Assert.IsTrue(hasHallRef, "F1.1: SC01_Main.unity must integrate HallArt.prefab");
            Assert.IsFalse(scene.Contains("m_Name: '[WarmHall]'") || scene.Contains("m_Name: [WarmHall]"),
                "F1.1: Duplicate [WarmHall] root must be removed from SC01_Main.unity");
        }

        [Test]
        public void F11_HallArtBuilder_UsesTargetArchGoldHexC9A861()
        {
            string builderSrc = E2ETestHelpers.ReadProjectText("Assets/Scripts/Editor/HallArtBuilder.cs");
            bool hasGold = builderSrc.IndexOf("C9A861", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           Regex.IsMatch(builderSrc, @"0\.788f?.*0\.659f?.*0\.380f?");
            Assert.IsTrue(hasGold, "F1.1: Inner arch gold must match #C9A861");
        }

        [Test]
        public void F12_TripoConfig_LoadsSafeDefaultsAndResourcesTemplateHasZeroKeys()
        {
            TripoConfig cfg = TripoConfig.Load();
            Assert.IsNotNull(cfg);
            StringAssert.Contains("tripo3d.ai", cfg.Endpoint);
            Assert.Greater(cfg.FaceLimit, 0);
            Assert.IsNotEmpty(cfg.TextToModelVersion);

            string resJson = E2ETestHelpers.ReadProjectText("Assets/Resources/ai_config.local.json");
            Assert.AreEqual(2, E2ETestHelpers.CountMatches(resJson, "\"apiKey\"\\s*:\\s*\"\""),
                "F1.2/F3.3: Assets/Resources/ai_config.local.json must have empty apiKey fields");
        }

        [Test]
        public void F12_ThreeTripoHeroGlbAssets_ExistUnderAssetsModels()
        {
            string modelsDir = E2ETestHelpers.AssetFullPath("Assets/Models");
            Assert.IsTrue(Directory.Exists(modelsDir), "Assets/Models directory must exist");
            string[] glbs = Directory.GetFiles(modelsDir, "*.glb", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(glbs.Length, 3,
                "F1.2: At least 3 Tripo hero GLB assets (tabletop, camera/steadicam, living-room set) must exist");
        }

        [Test]
        public void F13_TripoSmoothnessMaskMaps_And_OfflineLightmapDataExist()
        {
            string modelsDir = E2ETestHelpers.AssetFullPath("Assets/Models");
            string[] maskMaps = Directory.GetFiles(modelsDir, "*MaskMap*.*", SearchOption.AllDirectories);
            int pngMasks = 0;
            foreach (string f in maskMaps)
                if (!f.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) pngMasks++;
            Assert.GreaterOrEqual(pngMasks, 1,
                "F1.3: Tripo Roughness->Smoothness (1-Roughness) URP MaskMap textures must be generated");

            string lightData = E2ETestHelpers.AssetFullPath("Assets/Scenes/SC01_Main/LightingData.asset");
            Assert.IsTrue(File.Exists(lightData), "F1.3: Offline Lightmapper LightingData.asset must exist");
        }

        [Test]
        public void F14_SpatialGlassShader_DefinesRequiredRoundedBorderAndHighlightProperties()
        {
            string shaderSrc = E2ETestHelpers.ReadProjectText("Assets/Shaders/SpatialGlass.shader");
            StringAssert.Contains("_BaseColor", shaderSrc);
            StringAssert.Contains("_CornerRadius", shaderSrc);
            StringAssert.Contains("_BorderWidth", shaderSrc);
            StringAssert.Contains("_HighlightColor", shaderSrc);
            StringAssert.Contains("_RectSize", shaderSrc);
        }

        [Test]
        public void F14_SpatialGlassMaterial_MatchesDarkCharcoal94PercentAnd20PxRadius()
        {
            string matYaml = E2ETestHelpers.ReadProjectText("Assets/Resources/UI/SpatialGlass.mat");
            var radiusMatch = Regex.Match(matYaml, @"-\s*_CornerRadius:\s*([0-9.]+)");
            Assert.IsTrue(radiusMatch.Success, "SpatialGlass.mat must define _CornerRadius");
            float radius = float.Parse(radiusMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(20f, radius, 0.5f, "F1.4: SpatialGlass.mat _CornerRadius must be 20px");

            var colorMatch = Regex.Match(matYaml, @"-\s*_BaseColor:\s*\{r:\s*([0-9.]+),\s*g:\s*([0-9.]+),\s*b:\s*([0-9.]+),\s*a:\s*([0-9.]+)\}");
            Assert.IsTrue(colorMatch.Success, "SpatialGlass.mat must define _BaseColor");
            float r = float.Parse(colorMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            float a = float.Parse(colorMatch.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.LessOrEqual(r, 0.25f, "F1.4: SpatialGlass _BaseColor must be dark charcoal, not beige");
            Assert.AreEqual(0.94f, a, 0.03f, "F1.4: SpatialGlass _BaseColor alpha must be ~0.94 (94%)");
        }

        [Test]
        public void F14_Zero3DCubeGlassMonitorInHallAndScene_And_SpatialGlassStyleExists()
        {
            string hallPrefab = E2ETestHelpers.ReadProjectText("Assets/Prefabs/Stages/HallArt.prefab");
            Assert.IsFalse(hallPrefab.Contains("m_Name: GlassMonitor"),
                "F1.4: Legacy 3D cube GlassMonitor must be removed from HallArt.prefab");

            var styleType = E2ETestHelpers.FindTypeByName("SpatialGlassStyle");
            Assert.IsNotNull(styleType, "F1.4: SpatialGlassStyle contract type must exist");
            var applyMethod = typeof(SpatialGlass).GetMethod("Apply",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(RectTransform), styleType }, null);
            Assert.IsNotNull(applyMethod, "F1.4: SpatialGlass.Apply(RectTransform, SpatialGlassStyle) must exist");
        }
    }
}