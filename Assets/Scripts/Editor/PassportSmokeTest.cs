using System;
using System.IO;
using System.Linq;
using ShotField.Constraints;
using ShotField.Core;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class PassportSmokeTest
    {
        internal static void Verify(ShotLibrary library, ShotData first, ShotData second,
            ConstraintResult pass, ConstraintResult violation, ShotConstraint[] rules)
        {
            var smokeRun = System.Environment.GetEnvironmentVariable("SHOTFIELD_VERIFY_RUN");
            var folderTag = string.IsNullOrEmpty(smokeRun)
                ? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyyMMdd_HHmmss")
                : smokeRun;
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../05_验证记录", DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM"), "PassportSmoke-" + folderTag));
            first.lastCheck.Add(pass);
            first.checkedAt = DateTimeOffset.UtcNow.ToString("O");
            second.lastCheck.Add(violation);
            second.checkedAt = first.checkedAt;
            var firstPath = Write(folder, library, first, rules);
            var secondPath = Write(folder, library, second, rules);
            second.revision++;
            Write(folder, library, second, rules);

            var fresh = new GameObject("Reloaded passport library").AddComponent<ShotLibrary>();
            fresh.ShotCamera = library.ShotCamera;
            VerifyLoaded(fresh, firstPath, first, first.revision, CheckStatus.Pass);
            VerifyLoaded(fresh, secondPath, second, second.revision - 1, CheckStatus.Violation);
            VerifyInactiveRoundTrip(library);
        }

        private static void VerifyInactiveRoundTrip(ShotLibrary library)
        {
            var marker = SemanticMarker.Find("actorA");
            if (marker == null) throw new InvalidOperationException("找不到冒烟场景的 actorA。");
            var original = library.CaptureSnapshot();
            marker.gameObject.SetActive(false);
            try
            {
                var hidden = library.CaptureSnapshot();
                if (hidden.Find(marker.markerId)?.active != false)
                    throw new InvalidOperationException("快照遗漏了隐藏人物。");
                library.ApplySnapshot(original);
                if (!marker.gameObject.activeSelf)
                    throw new InvalidOperationException("应用快照后没有恢复隐藏人物。");
            }
            finally
            {
                library.ApplySnapshot(original);
            }
        }

        private static string Write(string folder, ShotLibrary library, ShotData shot, ShotConstraint[] rules)
        {
            return ShotPassportStore.Save(ShotPassportMapper.FromShot(shot, library.SceneId,
                ShotPassportPanel.Specs(rules)), folder);
        }

        private static void VerifyLoaded(ShotLibrary library, string path, ShotData source,
            int revision, CheckStatus expected)
        {
            var dto = ShotPassportStore.Load(path);
            var shot = dto.ToShot();
            library.AddLoaded(shot);
            library.ApplyShot(shot);
            var actual = library.ShotCamera;
            VerifyProjection(actual, shot.camera);
            if (!File.Exists(path) || shot.revision != revision || shot.shotId != source.shotId ||
                Vector3.Distance(actual.transform.position, source.camera.position) > 0.001f ||
                Quaternion.Angle(actual.transform.rotation, source.camera.CameraRotation) > 0.1f ||
                Mathf.Abs(actual.fieldOfView - source.camera.fieldOfView) > 0.01f ||
                Mathf.Abs(actual.aspect - source.camera.aspect) > 0.001f ||
                dto.cameraExtras.usesUnityCameraRotation != source.camera.usesUnityCameraRotation ||
                Mathf.Abs(shot.camera.heightAboveFloor - source.camera.heightAboveFloor) > 0.001f ||
                !dto.constraints.Any(rule => rule.type == "prop_hidden" && rule.propId == "envelope" && rule.face == "front") ||
                !dto.props.Any(prop => prop.id == "envelope") || !dto.actors.Any(actor => actor.id == "actorA") ||
                string.IsNullOrEmpty(dto.check_summary.checked_at) ||
                (expected == CheckStatus.Pass ? dto.check_summary.pass : dto.check_summary.violation) != 1)
                throw new InvalidOperationException("护照验收失败：" + path);

            // Old passports omit both cameraExtras fields.
            dto.cameraExtras.aspect = 0f;
            dto.cameraExtras.usesUnityCameraRotation = false;
            var euler = (source.camera.CameraRotation * Quaternion.Euler(0f, 180f, 0f)).eulerAngles;
            dto.camera.rot = new[] { euler.x, euler.y, euler.z };
            var legacy = dto.ToShot().camera;
            if (legacy.aspect != ShotCameraState.DefaultAspect ||
                Quaternion.Angle(legacy.CameraRotation, source.camera.CameraRotation) > 0.1f)
                throw new InvalidOperationException("旧版护照画幅或相机朝向兼容失败：" + path);
        }

        private static void VerifyProjection(Camera actual, ShotCameraState state)
        {
            var point = actual.transform.position + actual.transform.forward * 2f +
                actual.transform.right * 0.2f + actual.transform.up * 0.1f;
            var viewport = actual.WorldToViewportPoint(point);
            if (!state.TryProjectToViewport(point, out var projected) ||
                Vector2.Distance(projected, viewport) > 0.001f)
                throw new InvalidOperationException("约束投影与实际相机画面不一致。");
        }
    }
}
