#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>同一真实机位的约束 FAIL/PASS 成对取证；复用 ConstraintPairVerifier 的探针。</summary>
    public static class ConstraintEvidence
    {
        public static string OutputDirectory;

        [Serializable]
        internal sealed class Report
        {
            public string status, scene, markerId, cameraPosition, failStatus, failDetail;
            public string passStatus, passDetail, restoredFailStatus, failImage, passImage;
            public string failLog, passLog, failSha256, passSha256;
        }

        [MenuItem("ShotField/Evidence/Capture Constraint FAIL PASS")]
        public static void Run()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string directory = OutputDirectory;
            if (string.IsNullOrEmpty(directory))
                directory = Environment.GetEnvironmentVariable("SHOTFIELD_CONSTRAINT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                string month = Path.Combine(B5EvidenceSupport.FindEvidenceRoot(), DateTime.Now.ToString("yyyy-MM"));
                Directory.CreateDirectory(month);
                directory = Path.Combine(month, "constraint_evidence_" + stamp);
            }
            Directory.CreateDirectory(directory);
            string failImage = Path.Combine(directory, stamp + "_约束_FAIL.png");
            string passImage = Path.Combine(directory, stamp + "_约束_PASS.png");
            string failLog = Path.Combine(directory, stamp + "_约束_FAIL.log");
            string passLog = Path.Combine(directory, stamp + "_约束_PASS.log");
            string reportPath = Path.Combine(directory, "constraint_evidence.json");
            if (File.Exists(failImage) || File.Exists(passImage) || File.Exists(reportPath))
                throw new InvalidOperationException("Constraint evidence already exists; no files were overwritten.");

            string failHash = "", passHash = "";
            var result = ConstraintPairVerifier.Capture((camera, stage, constraint) =>
            {
                bool expected = stage == "FAIL" ? constraint.status == CheckStatus.Violation : constraint.status == CheckStatus.Pass;
                if (!expected) throw new InvalidDataException($"{stage} 状态不符合预期: {constraint.status}");
                string image = stage == "FAIL" ? failImage : passImage;
                string label = stage == "FAIL" ? "FAIL | front visible" : "PASS | front blocked";
                B5EvidenceRenderer.RenderPng(camera, image, 1280, 720, label);
                byte[] bytes = File.ReadAllBytes(image);
                if (bytes.Length == 0) throw new InvalidDataException(stage + " 截图为空");
                string hash;
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (stage == "FAIL") failHash = hash; else passHash = hash;
                string log = stage == "FAIL" ? failLog : passLog;
                string text = $"stage={stage}\nscene={BuildAndroid.ScenePath}\nmarker=envelope\n" +
                    $"camera={camera.transform.position}\nstatus={constraint.status}\ndetail={constraint.detail}\n" +
                    $"image={image}\nbytes={bytes.Length}\nsha256={hash}\n";
                File.WriteAllText(log, text, new UTF8Encoding(false));
            });
            if (result.fail.status != CheckStatus.Violation || result.pass.status != CheckStatus.Pass ||
                result.restoredFail.status != CheckStatus.Violation)
                throw new InvalidDataException("约束可逆探针未满足 FAIL→PASS→FAIL");

            var report = new Report
            {
                status = "PASS", scene = result.scenePath, markerId = result.markerId,
                cameraPosition = result.cameraPosition.ToString("F4"),
                failStatus = result.fail.status.ToString(), failDetail = result.fail.detail,
                passStatus = result.pass.status.ToString(), passDetail = result.pass.detail,
                restoredFailStatus = result.restoredFail.status.ToString(),
                failImage = failImage, passImage = passImage, failLog = failLog, passLog = passLog,
                failSha256 = failHash, passSha256 = passHash
            };
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log($"CONSTRAINT-EVIDENCE=PASS fail={result.fail.status} pass={result.pass.status} " +
                $"restored={result.restoredFail.status} path={directory}");
        }
    }
}
#endif
