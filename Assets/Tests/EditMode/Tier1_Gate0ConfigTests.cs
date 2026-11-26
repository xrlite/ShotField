using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShotField.RuntimeUI;
using UnityEngine;

namespace ShotField.Tests.EditMode
{
    [TestFixture]
    public class Tier1_Gate0ConfigTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown() => E2ETestHelpers.DestroyAllTracked(_created);

        [Test]
        public void F01_OpenXRPackageSettings_DisablesEyeTrackingFoveationAndEnablesHands()
        {
            string yaml = E2ETestHelpers.ReadProjectText("Assets/XR/Settings/OpenXRPackageSettings.asset");
            string foveated = E2ETestHelpers.ExtractYamlSection(yaml, "m_Name: FoveatedRenderingFeature Android");
            Assert.IsNotEmpty(foveated, "FoveatedRenderingFeature Android section must exist");
            Assert.AreEqual("0", E2ETestHelpers.ExtractYamlValue(foveated, "useEyeTracking"),
                "F0.1: EyeTracking foveation must be disabled so eye-tracking is not required");

            string handTracking = E2ETestHelpers.ExtractYamlSection(yaml, "m_Name: HandTracking Android");
            string handProfile = E2ETestHelpers.ExtractYamlSection(yaml, "m_Name: HandInteractionProfile Android");
            Assert.AreEqual("1", E2ETestHelpers.ExtractYamlValue(handTracking, "m_enabled"),
                "F0.1: OpenXR HandTracking Android must be enabled");
            Assert.AreEqual("1", E2ETestHelpers.ExtractYamlValue(handProfile, "m_enabled"),
                "F0.1: HandInteractionProfile Android must be enabled");
        }

        [Test]
        public void F01_HandTrackingModeRouter_DoesNotDisableControllersWhenHandsTracked()
        {
            string src = E2ETestHelpers.ReadProjectText("Assets/Scripts/RuntimeUI/HandTrackingModeRouter.cs");
            bool disablesLeft = src.Contains("SetActive(leftController, !left)");
            bool disablesRight = src.Contains("SetActive(rightController, !right)");
            Assert.IsFalse(disablesLeft || disablesRight,
                "F0.1: HandTrackingModeRouter must not disable controller GameObjects when hands track");
        }

        [Test]
        public void F01_SpatialTrackingCoordinator_DefaultsAllExtensionsFalseAndConfigures()
        {
            var go = new GameObject("SpatialTrackingCoordinator_Test");
            _created.Add(go);
            var coord = go.AddComponent<SpatialTrackingCoordinator>();

            Assert.IsFalse(coord.EnableMotionTracker, "Default motion tracker must be false");
            Assert.IsFalse(coord.EnableHandTracking, "Default hand tracking must be false");
            Assert.IsFalse(coord.EnableEyeTracking, "Default eye tracking must be false");

            coord.Configure(false, true, false);
            coord.InitializeTracking();
            Assert.IsTrue(coord.EnableHandTracking);
            Assert.IsFalse(coord.EnableEyeTracking);
        }

        [Test]
        public void F02_SC01Main_HasSingleXRUIInputModuleWithInputSystemModeAndZeroTriggerDocRaycast()
        {
            string scene = E2ETestHelpers.ReadProjectText("Assets/Scenes/SC01_Main.unity");
            int xrUiModules = E2ETestHelpers.CountMatches(scene, @"m_EnableXRInput:\s*1");
            Assert.AreEqual(1, xrUiModules, "F0.2: SC01_Main must have exactly one enabled XRUIInputModule");

            int inputModes = E2ETestHelpers.CountMatches(scene, @"m_ActiveInputMode:\s*1");
            Assert.AreEqual(1, inputModes, "F0.2: XRUIInputModule must use m_ActiveInputMode: 1 (InputSystemActions)");

            int badUiDocTrigger = E2ETestHelpers.CountMatches(scene, @"m_RaycastUIDocumentTriggerInteraction:\s*1");
            Assert.AreEqual(0, badUiDocTrigger,
                "F0.2: m_RaycastUIDocumentTriggerInteraction must be 0 across all rays");
        }

        [Test]
        public void F03_ShotFieldXRIActions_BindsUiPressForBothControllerTriggerAndHandPointer()
        {
            string json = E2ETestHelpers.ReadProjectText("Assets/Settings/Input/ShotFieldXRIActions.inputactions");
            StringAssert.Contains("\"name\": \"UI Press\"", json);
            StringAssert.Contains("<XRController>{LeftHand}/triggerPressed", json);
            StringAssert.Contains("<XRController>{RightHand}/triggerPressed", json);
            StringAssert.Contains("<HandInteraction>{LeftHand}/pointerActivated", json);
            StringAssert.Contains("<HandInteraction>{RightHand}/pointerActivated", json);
        }

        [Test]
        public void F04_TmpSettings_RegistersShotFieldSansScSdfInDefaultAndFallbackFonts()
        {
            string fontMeta = E2ETestHelpers.ReadProjectText("Assets/Resources/Fonts/ShotFieldSansSC SDF.asset.meta");
            string fontGuid = E2ETestHelpers.ExtractYamlValue(fontMeta, "guid");
            Assert.IsNotEmpty(fontGuid, "ShotFieldSansSC SDF.asset.meta must have a valid guid");

            string tmp = E2ETestHelpers.ReadProjectText("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            StringAssert.Contains(fontGuid, E2ETestHelpers.ExtractYamlValue(tmp, "m_defaultFontAsset"),
                "F0.4: TMP default font must reference ShotFieldSansSC SDF");

            string fallbackSection = E2ETestHelpers.ExtractYamlSection(tmp, "m_fallbackFontAssets:");
            StringAssert.Contains(fontGuid, fallbackSection,
                "F0.4: m_fallbackFontAssets must register ShotFieldSansSC SDF to prevent CJK tofu glyphs");
        }

        [Test]
        public void F05_EmulatorRuntimeDetector_ExistsAndExposesBoolIsEmulatorContract()
        {
            var detectorType = E2ETestHelpers.FindTypeByName("EmulatorRuntimeDetector");
            Assert.IsNotNull(detectorType, "F0.5: EmulatorRuntimeDetector type must exist");
            var method = detectorType.GetMethod("IsEmulator",
                BindingFlags.Public | BindingFlags.Static, null, System.Type.EmptyTypes, null);
            Assert.IsNotNull(method, "F0.5: EmulatorRuntimeDetector.IsEmulator() must be public static");
            Assert.AreEqual(typeof(bool), method.ReturnType, "IsEmulator() must return bool");
            bool result = (bool)method.Invoke(null, null);
            Assert.IsFalse(result, "Editor host must not falsely report ro.boot.qemu == 1");
        }

        [Test]
        public void F05_WarmHallVisualController_PreservesVstPassthroughAndEditorStage()
        {
            var go = new GameObject("WarmHallController_Test");
            _created.Add(go);
            var ctrl = go.AddComponent<WarmHallVisualController>();
            Assert.IsTrue(ctrl.usePassthroughState, "F0.5: VST passthrough state flag must default true");
            Assert.IsTrue(ctrl.followTabletopAnchor);
        }
    }
}