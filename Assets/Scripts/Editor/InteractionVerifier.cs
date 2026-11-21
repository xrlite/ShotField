#if UNITY_EDITOR
using System.Text;
using ShotField.RuntimeUI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M3 交互骨架验证：XROrigin / InteractionManager / 每手双 Interactor / 抓取组件配置 / 走位 Provider / Canvas 射线。
    /// 结果标记 INTERACTION-RESULT=。注明：这些只能证明「组件装配正确」，
    /// 手柄射线与 3D 立体窗口在模拟器不可靠，运行时交互一律 pending-真机。
    /// </summary>
    internal static class InteractionVerifier
    {
        private const int ExpectedAssertions = 13;
        private static int _passed;
        private static int _total;
        private static readonly StringBuilder Report = new StringBuilder();

        public static void Run()
        {
            _passed = 0;
            _total = 0;
            Report.Length = 0;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = null;
            try
            {
                root = Sc01Blockout.BuildIntoScene();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[InteractionVerifier] 场景构建失败: " + e.Message);
                Finish(false);
                return;
            }

            // 先建工作台 Canvas，再装配 XRI，否则 UpgradeWorkbenchCanvas 找不到 Canvas
            SpatialWorkbenchBuilder.BuildWorkbench();
            SceneXriRigBuilder.BuildRig(root);

            Assert("场景存在 XROrigin", Object.FindAnyObjectByType<XROrigin>() != null);
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Assert("XROrigin 绑定了主相机（未新增第二个主相机）",
                origin != null && origin.Camera != null
                && Object.FindObjectsByType<Camera>().Length <= 2);

            Assert("存在 XRInteractionManager", Object.FindAnyObjectByType<XRInteractionManager>() != null);
            Assert("存在 LocomotionMediator", Object.FindAnyObjectByType<LocomotionMediator>() != null);

            var directs = Object.FindObjectsByType<XRDirectInteractor>();
            var rays = Object.FindObjectsByType<XRRayInteractor>();
            Assert($"每只手一个近距 Interactor（实际 {directs.Length}）", directs.Length >= 2);
            Assert($"每只手一个射线 Interactor（实际 {rays.Length}）", rays.Length >= 2);

            Assert("近距 Interactor 的 Collider 为 trigger",
                AllTriggers(directs));
            Assert("射线 Interactor 不带 Collider",
                NoColliders(rays));

            Assert("存在 SnapTurnProvider 且角度 45°",
                TurnAngleIs(45f));
            Assert("存在 ContinuousMoveProvider 且速度 2.0",
                MoveSpeedIs(2f));

            Assert("可摆道具已挂 XRGrabInteractable + Rigidbody（至少 3 件）",
                CountGrabbables(root) >= 3);
            Assert("抓取道具的 Collider 均非 trigger 且带 ScenePropGrab",
                GrabConfigValid(root));

            Assert("WorldSpace Canvas 有 TrackedDeviceGraphicRaycaster",
                CanvasHasRaycaster());

            Finish(_passed == ExpectedAssertions);
        }

        private static bool AllTriggers(XRDirectInteractor[] list)
        {
            if (list.Length == 0) return false;
            for (int i = 0; i < list.Length; i++)
            {
                var c = list[i].GetComponent<Collider>();
                if (c == null || !c.isTrigger) return false;
            }
            return true;
        }

        private static bool NoColliders(XRRayInteractor[] list)
        {
            if (list.Length == 0) return false;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].GetComponent<Collider>() != null) return false;
            }
            return true;
        }

        private static bool TurnAngleIs(float expected)
        {
            var snap = Object.FindAnyObjectByType<SnapTurnProvider>();
            return snap != null && Mathf.Abs(snap.turnAmount - expected) < 0.5f;
        }

        private static bool MoveSpeedIs(float expected)
        {
            var move = Object.FindAnyObjectByType<ContinuousMoveProvider>();
            return move != null && Mathf.Abs(move.moveSpeed - expected) < 0.05f;
        }

        private static int CountGrabbables(GameObject root)
        {
            int n = 0;
            for (int i = 0; i < SceneXriRigBuilder.GrabbableProps.Length; i++)
            {
                var t = root.transform.Find(SceneXriRigBuilder.GrabbableProps[i]);
                if (t == null) continue;
                if (t.GetComponent<XRGrabInteractable>() != null && t.GetComponent<Rigidbody>() != null) n++;
            }
            return n;
        }

        private static bool GrabConfigValid(GameObject root)
        {
            for (int i = 0; i < SceneXriRigBuilder.GrabbableProps.Length; i++)
            {
                var t = root.transform.Find(SceneXriRigBuilder.GrabbableProps[i]);
                if (t == null) continue;
                var collider = t.GetComponent<Collider>();
                if (collider == null || collider.isTrigger) return false;
                if (t.GetComponent<ScenePropGrab>() == null) return false;
            }
            return true;
        }

        private static bool CanvasHasRaycaster()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return false;
            return canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null;
        }

        private static void Assert(string label, bool ok)
        {
            _total++;
            if (ok) _passed++;
            var line = (ok ? "PASS: " : "FAIL: ") + label;
            Report.AppendLine("[InteractionVerifier] " + line);
            Debug.Log("[InteractionVerifier] " + line);
        }

        private static void Finish(bool allPassed)
        {
            if (_total != ExpectedAssertions)
            {
                allPassed = false;
                var mismatch = $"FAIL: 断言计数不符 ({_total}/{ExpectedAssertions})";
                Report.AppendLine("[InteractionVerifier] " + mismatch);
                Debug.Log("[InteractionVerifier] " + mismatch);
            }
            var verdict = allPassed ? "PASS" : "FAIL";
            var result = $"INTERACTION-RESULT={verdict} ({_passed}/{_total})";
            Report.AppendLine(result);
            Debug.Log("[InteractionVerifier] " + result);
            Debug.Log("[InteractionVerifier] 口径：组件装配已验证；手柄射线与立体窗口 pending-真机。");

            var path = "05_验证记录/2026-11/interaction_verify_last.log";
            System.IO.File.WriteAllText(path, Report.ToString());
        }
    }
}
#endif
