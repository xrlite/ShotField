using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class ShotPackageExporterVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 6;
            if (WorkbenchExportBinding.SavedBindingExists()) passCount++;
            else Debug.LogError("Missing SC01 export button binding");
            string tempDir = Path.Combine(Application.temporaryCachePath, "ExportTest_" + Guid.NewGuid().ToString("N"));
            string zipPath = Path.Combine(tempDir, "SC01_01_reference_pack.zip");
            var markerGo = new GameObject("TestEnvelopeMarker");

            try
            {
                Directory.CreateDirectory(tempDir);
                var marker = markerGo.AddComponent<SemanticMarker>();
                marker.markerId = "envelope";
                marker.kind = SemanticMarker.Kind.KeyProp;

                // Test 1: 构建含相机、快照、轨迹、Take 的完整 ShotData
                var shot = new ShotData
                {
                    shotId = "SC01-01",
                    revision = 2,
                    intent = "俯视特写信封，演员从右侧伸手拿走",
                    updatedAt = "2026-11-18 16:00",
                    updatedBy = "Antigravity",
                    approval = ApprovalStatus.Reviewed,
                    camera = new ShotCameraState { position = new Vector3(0, 1.2f, 1.2f), aspect = 1.7778f }
                };
                shot.snapshot.actors.Add(new ActorState { actorId = "envelope", position = new Vector3(0, 0.81f, 1.2f) });

                var traj = new CameraTrajectory();
                traj.AddPoint(0f, new Vector3(0, 1.5f, 0), Quaternion.identity, 45f);
                traj.AddPoint(2.5f, new Vector3(0, 1.2f, 1.2f), Quaternion.Euler(30f, 0, 0), 50f);
                shot.trajectory = traj;

                var take = new ActorTake { takeId = "Take_01", actorId = "actorA" };
                take.AddKeyframe(0f, new Vector3(0.5f, 0, 1.2f), Quaternion.identity, "reach_hand");
                take.AddKeyframe(2.0f, new Vector3(0, 0.81f, 1.2f), Quaternion.identity, "take_letter");
                shot.take = take;

                passCount++;
                Debug.Log("[ShotPackageExporterVerifier] Test 1 PASS: 测试分镜数据模型构建就绪");

                // Test 2: 内存生成标准 Zip 二进制流
                byte[] dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };
                byte[] zipBytes = ShotPackageExporter.CreateZipPackage(shot, dummyPng, "sc01_clay.png");
                if (zipBytes != null && zipBytes.Length > 200)
                {
                    passCount++;
                    Debug.Log($"[ShotPackageExporterVerifier] Test 2 PASS: 内存 Zip 打包成功 (字节数={zipBytes.Length})");
                }
                else
                {
                    Debug.LogError("[ShotPackageExporterVerifier] Test 2 FAIL: Zip 二进制流生成失败");
                }

                // Test 3: 落盘导出为 .zip 文件
                ShotPackageExporter.ExportPackageToFile(shot, dummyPng, zipPath, "sc01_clay.png");
                if (File.Exists(zipPath) && new FileInfo(zipPath).Length > 0)
                {
                    passCount++;
                    Debug.Log($"[ShotPackageExporterVerifier] Test 3 PASS: 参考包成功写入磁盘文件 (size={new FileInfo(zipPath).Length} bytes)");
                }
                else
                {
                    Debug.LogError("[ShotPackageExporterVerifier] Test 3 FAIL: 磁盘写入失败");
                }

                // Test 4: 校验压缩包内部目录结构与清单
                bool verified = ShotPackageExporter.VerifyZipPackage(zipPath, out List<string> entries);
                bool hasPassport = entries.Contains("passport.json");
                bool hasManifest = entries.Contains("manifest.json");
                bool hasPrompt = entries.Contains("generation_prompt.txt");
                bool hasImage = entries.Contains("sc01_clay.png");

                if (verified && hasPassport && hasManifest && hasPrompt && hasImage)
                {
                    passCount++;
                    Debug.Log($"[ShotPackageExporterVerifier] Test 4 PASS: 压缩包解压校验通过，包含完整 4 项条目 ({string.Join(", ", entries)})");
                }
                else
                {
                    Debug.LogError($"[ShotPackageExporterVerifier] Test 4 FAIL: 缺少条目 pass={hasPassport}, manifest={hasManifest}, prompt={hasPrompt}, img={hasImage}");
                }

                // Test 5: 解包读取 passport.json 并逆向反序列化验证数据无损
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var passportEntry = archive.GetEntry("passport.json");
                    using var reader = new StreamReader(passportEntry.Open());
                    string json = reader.ReadToEnd();
                    var loadedDto = JsonUtility.FromJson<ShotPassportDto>(json);
                    var loadedShot = loadedDto.ToShot();

                    bool t5Ok = loadedShot != null &&
                                loadedShot.shotId == "SC01-01" &&
                                loadedShot.revision == 2 &&
                                loadedShot.intent.Contains("俯视特写") &&
                                loadedShot.trajectory != null && loadedShot.trajectory.Count == 2 &&
                                loadedShot.take != null && loadedShot.take.Count == 2;

                    if (t5Ok)
                    {
                        passCount++;
                        Debug.Log("[ShotPackageExporterVerifier] Test 5 PASS: Zip 包内 passport.json 提取反序列化验证数据完全无损");
                    }
                    else
                    {
                        Debug.LogError("[ShotPackageExporterVerifier] Test 5 FAIL: 反序列化数据不匹配");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShotPackageExporterVerifier] EXCEPTION: {ex}");
            }
            finally
            {
                if (markerGo != null) UnityEngine.Object.DestroyImmediate(markerGo);
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }

            if (passCount == totalTests)
            {
                Debug.Log($"PACKAGE-EXPORTER-RESULT=PASS ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"PACKAGE-EXPORTER-RESULT=FAIL ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
