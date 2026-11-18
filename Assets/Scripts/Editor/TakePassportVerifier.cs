using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class TakePassportVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 4;
            var sceneRoot = new GameObject("TakeVerifyRoot");

            try
            {
                var actorMarker = sceneRoot.AddComponent<SemanticMarker>();
                actorMarker.markerId = "actorA";
                actorMarker.kind = SemanticMarker.Kind.ActorPrimary;

                // Test 1: 保存带 ActorTake 的 ShotData 到 JSON，读回验证一致性
                var originalShot = new ShotData
                {
                    shotId = "SC01-01",
                    revision = 1,
                    intent = "表演测试",
                    updatedAt = "2026-11-18 15:00",
                    updatedBy = "Antigravity",
                    approval = ApprovalStatus.Draft,
                    camera = new ShotCameraState { position = new Vector3(0, 1.6f, -3f) }
                };
                originalShot.snapshot.actors.Add(new ActorState
                {
                    actorId = "actorA",
                    position = new Vector3(0, 0, 1f),
                    rotation = Quaternion.identity
                });

                var take = new ActorTake { takeId = "Take_01", actorId = "actorA" };
                take.AddKeyframe(0f, new Vector3(0, 0, 0), Quaternion.identity, "walk");
                take.AddKeyframe(1.0f, new Vector3(0, 0, 1f), Quaternion.Euler(0, 30f, 0), "pause");
                take.AddKeyframe(2.0f, new Vector3(0, 0, 2f), Quaternion.Euler(0, 60f, 0), "speak");
                originalShot.take = take;

                var specs = new[] { new ConstraintSpec { type = "Visibility", ruleName = "视线无遮挡" } };
                var dto = ShotPassportDto.FromShot(originalShot, "SC01", specs);
                string json = JsonUtility.ToJson(dto, true);

                var loadedDto = JsonUtility.FromJson<ShotPassportDto>(json);
                var loadedShot = loadedDto.ToShot();

                bool t1Ok = loadedShot.take != null &&
                            loadedShot.take.takeId == "Take_01" &&
                            loadedShot.take.actorId == "actorA" &&
                            loadedShot.take.Count == 3 &&
                            Mathf.Approximately(loadedShot.take.duration, 2.0f) &&
                            loadedShot.take.keyframes[0].actionTag == "walk" &&
                            loadedShot.take.keyframes[1].actionTag == "pause" &&
                            loadedShot.take.keyframes[2].actionTag == "speak" &&
                            Vector3.Distance(loadedShot.take.keyframes[1].position, new Vector3(0, 0, 1f)) < 0.001f;

                if (t1Ok)
                {
                    passCount++;
                    Debug.Log("[TakePassportVerifier] Test 1 PASS: ActorTake 序列化/反序列化完整往返一致");
                }
                else
                {
                    Debug.LogError("[TakePassportVerifier] Test 1 FAIL: ActorTake 反序列化数据不匹配");
                }

                // Test 2: 读回不含 actorTake 的旧版 JSON 护照，验证 null 安全回退无异常
                string legacyJson = @"
                {
                    ""shot_id"": ""SC01-02"",
                    ""revision"": 1,
                    ""approval_status"": ""draft"",
                    ""updated_at"": ""2026-11-18 12:00"",
                    ""updated_by"": ""LegacyUser"",
                    ""camera"": { ""pos"": [0, 1.5, -2], ""rot"": [0, 0, 0], ""fov"": 45, ""height"": 1.5 },
                    ""cameraExtras"": { ""nearClip"": 0.05, ""farClip"": 100, ""aspect"": 1.7777778 },
                    ""snapshotExtras"": [ { ""actorId"": ""actorA"", ""position"": { ""x"": 0, ""y"": 0, ""z"": 0 }, ""rotation"": { ""x"": 0, ""y"": 0, ""z"": 0, ""w"": 1 } } ],
                    ""constraints"": [ { ""type"": ""Visibility"", ""ruleName"": ""rule1"" } ]
                }";

                var legacyDto = JsonUtility.FromJson<ShotPassportDto>(legacyJson);
                var legacyShot = legacyDto.ToShot();
                if (legacyShot != null && legacyShot.take == null)
                {
                    passCount++;
                    Debug.Log("[TakePassportVerifier] Test 2 PASS: 旧版 JSON 无 actorTake 字段平滑回退 null，无异常");
                }
                else
                {
                    Debug.LogError("[TakePassportVerifier] Test 2 FAIL: 旧版 JSON 兼容性失败");
                }

                // Test 3: Take 关键帧时序插值评估
                var sampleEarly = loadedShot.take.Evaluate(0.4f);
                var sampleLate = loadedShot.take.Evaluate(1.6f);
                bool t3Ok = Vector3.Distance(sampleEarly.position, new Vector3(0, 0, 0.4f)) < 0.001f &&
                            sampleEarly.actionTag == "walk" &&
                            Vector3.Distance(sampleLate.position, new Vector3(0, 0, 1.6f)) < 0.001f &&
                            sampleLate.actionTag == "speak";
                if (t3Ok)
                {
                    passCount++;
                    Debug.Log($"[TakePassportVerifier] Test 3 PASS: 关键帧线性时序插值与动作标签正确 (t=0.4 pos={sampleEarly.position}, t=1.6 pos={sampleLate.position})");
                }
                else
                {
                    Debug.LogError($"[TakePassportVerifier] Test 3 FAIL: 插值偏差 earlyPos={sampleEarly.position}, latePos={sampleLate.position}");
                }

                // Test 4: ShotData 深拷贝中 Take 独立性验证
                var cloneShot = originalShot.Clone();
                cloneShot.take.keyframes[0] = new ActorKeyframe(0f, Vector3.one, Quaternion.identity, "modifiedAction");
                bool t4Ok = originalShot.take.keyframes[0].actionTag == "walk" &&
                            cloneShot.take.keyframes[0].actionTag == "modifiedAction";
                if (t4Ok)
                {
                    passCount++;
                    Debug.Log("[TakePassportVerifier] Test 4 PASS: ShotData.Clone() 对 take 保持深拷贝独立性");
                }
                else
                {
                    Debug.LogError("[TakePassportVerifier] Test 4 FAIL: 深拷贝引用污染");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TakePassportVerifier] EXCEPTION: {ex}");
            }
            finally
            {
                if (sceneRoot != null) UnityEngine.Object.DestroyImmediate(sceneRoot);
            }

            if (passCount == totalTests)
            {
                Debug.Log($"TAKE-PASSPORT-RESULT=PASS ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"TAKE-PASSPORT-RESULT=FAIL ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
