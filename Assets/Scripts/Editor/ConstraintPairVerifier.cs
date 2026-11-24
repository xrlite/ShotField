#if UNITY_EDITOR
using System;
using ShotField.Constraints;
using ShotField.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal sealed class ConstraintPairResult
    {
        public ConstraintResult fail, pass, restoredFail;
        public Vector3 cameraPosition;
        public string scenePath, markerId;
        public bool cameraStateStable;
    }

    /// <summary>同一真实机位加/撤挡板的可逆约束探针；FAIL 是预期结果，不记 Console 错误。</summary>
    public static class ConstraintPairVerifier
    {
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run Constraint Pair Verifier")]
        public static void Run()
        {
            int passed = 0;
            try
            {
                var result = Capture(null);
                Check("真实主场景与信封关键面", result.scenePath == BuildAndroid.ScenePath &&
                    result.markerId == "envelope", ref passed);
                Check("无挡板同一机位判 Violation", result.fail.status == CheckStatus.Violation, ref passed);
                Check("加挡板后同一镜头判 Pass", result.pass.status == CheckStatus.Pass, ref passed);
                Check("撤挡板后同一机位恢复 Violation", result.restoredFail.status == CheckStatus.Violation, ref passed);
                Check("三次求值前后 ShotCameraState 位置/旋转未变", result.cameraStateStable, ref passed);
                Check("违规与通过原因均由真实规则返回", !string.IsNullOrEmpty(result.fail.detail) &&
                    !string.IsNullOrEmpty(result.pass.detail), ref passed);
            }
            catch (Exception ex) { Debug.LogError("[CONSTRAINT-PAIR] " + ex); }
            VerifyAssert.Finish("CONSTRAINT-PAIR-RESULT", passed, Expected);
        }

        internal static ConstraintPairResult Capture(Action<Camera, string, ConstraintResult> onFrame)
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) throw new InvalidOperationException("主场景加载失败");
            var director = GameObject.Find("Director");
            var camera = director != null ? director.GetComponent<Camera>() : null;
            var library = director != null ? director.GetComponent<ShotLibrary>() : null;
            var rule = director != null ? director.GetComponent<VisibilityConstraint>() : null;
            var marker = SemanticMarker.Find("envelope", FindObjectsInactive.Include);
            if (camera == null || library == null || rule == null || marker == null || !marker.criticalFaceMustStayHidden)
                throw new InvalidOperationException("Director/ShotLibrary/VisibilityConstraint/envelope 关键面未完整接线");

            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            float fov = camera.fieldOfView, aspect = camera.aspect;
            var target = camera.targetTexture;
            var clearFlags = camera.clearFlags;
            var background = camera.backgroundColor;
            GameObject blocker = null;
            try
            {
                Vector3 center = marker.CriticalFaceCenter, normal = marker.CriticalFaceNormalWorld;
                camera.transform.position = center + normal;
                Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
                camera.transform.LookAt(center, up);
                camera.fieldOfView = 45f; camera.aspect = ShotCameraState.DefaultAspect;
                Physics.SyncTransforms();
                var cameraState = ShotCameraState.FromCamera(camera);
                var expectedPosition = cameraState.position;
                var expectedRotation = cameraState.rotation;
                var shot = new ShotData
                {
                    shotId = "SC01-CONSTRAINT-EVIDENCE", camera = cameraState,
                    snapshot = library.CaptureSnapshot()
                };
                var fail = rule.Evaluate(shot, null);
                Debug.Log($"[CONSTRAINT-PAIR] expected FAIL camera={camera.transform.position} status={fail.status} detail={fail.detail}");
                onFrame?.Invoke(camera, "FAIL", fail);

                blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name = "[ConstraintEvidenceBlocker]";
                blocker.transform.position = center + normal * 0.35f;
                blocker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                blocker.transform.localScale = new Vector3(0.4f, 0.05f, 0.4f);
                Physics.SyncTransforms();
                var pass = rule.Evaluate(shot, null);
                Debug.Log($"[CONSTRAINT-PAIR] expected PASS blocker=true status={pass.status} detail={pass.detail}");
                onFrame?.Invoke(camera, "PASS", pass);

                UnityEngine.Object.DestroyImmediate(blocker); blocker = null;
                Physics.SyncTransforms();
                var restoredFail = rule.Evaluate(shot, null);
                Debug.Log($"[CONSTRAINT-PAIR] expected FAIL blocker=false status={restoredFail.status} detail={restoredFail.detail}");
                return new ConstraintPairResult
                {
                    fail = fail, pass = pass, restoredFail = restoredFail,
                    cameraPosition = shot.camera.position, scenePath = scene.path, markerId = marker.markerId,
                    cameraStateStable = Vector3.Distance(cameraState.position, expectedPosition) < 0.00001f &&
                        Quaternion.Angle(cameraState.rotation, expectedRotation) < 0.001f
                };
            }
            finally
            {
                if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker);
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov; camera.aspect = aspect; camera.targetTexture = target;
                camera.clearFlags = clearFlags; camera.backgroundColor = background;
            }
        }

        private static void Check(string label, bool ok, ref int passed)
        {
            if (!ok) throw new InvalidOperationException(label);
            passed++; Debug.Log("[CONSTRAINT-PAIR] PASS: " + label);
        }
    }
}
#endif
