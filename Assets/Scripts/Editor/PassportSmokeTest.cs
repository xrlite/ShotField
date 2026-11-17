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
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../05_验证记录/2026-11", "PassportSmoke-" + Guid.NewGuid().ToString("N")));
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
        }

        private static string Write(string folder, ShotLibrary library, ShotData shot, ShotConstraint[] rules)
        {
            return ShotPassportStore.Save(ShotPassportDto.FromShot(shot, library.SceneId,
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
            if (!File.Exists(path) || shot.revision != revision || shot.shotId != source.shotId ||
                Vector3.Distance(actual.transform.position, source.camera.position) > 0.001f ||
                Quaternion.Angle(actual.transform.rotation, source.camera.rotation) > 0.1f ||
                Mathf.Abs(actual.fieldOfView - source.camera.fieldOfView) > 0.01f ||
                Mathf.Abs(shot.camera.heightAboveFloor - source.camera.heightAboveFloor) > 0.001f ||
                !dto.constraints.Any(rule => rule.type == "prop_hidden" && rule.propId == "envelope" && rule.face == "front") ||
                !dto.props.Any(prop => prop.id == "envelope") || !dto.actors.Any(actor => actor.id == "actorA") ||
                string.IsNullOrEmpty(dto.check_summary.checked_at) ||
                (expected == CheckStatus.Pass ? dto.check_summary.pass : dto.check_summary.violation) != 1)
                throw new InvalidOperationException("护照验收失败：" + path);
        }
    }
}
