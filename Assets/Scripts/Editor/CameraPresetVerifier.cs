#if UNITY_EDITOR
using System;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M4 机位库验收：6 预设齐全、几何合理、纯函数可复现、朝向确实指向锚点。
    /// </summary>
    public static class CameraPresetVerifier
    {
        // 6 条结构 + 4 条几何 + 1 条纯函数 = 11
        private const int ExpectedAssertions = 11;

        [MenuItem("ShotField/Verifiers/Run Camera Preset Verifier")]
        [MenuItem("ShotField/验证/验证机位预设 CameraPreset")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[CameraPresetVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[CameraPresetVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            // 锚点用制作包 §2 的信封位置，与 M1 同一基准
            var anchor = new Vector3(1.5f, 0.46f, 2.15f);

            Assert("预设总数为 6", CameraPreset.Count == 6);
            Assert("预设枚举值数等于 Count",
                Enum.GetValues(typeof(CameraPresetId)).Length == CameraPreset.Count);

            var names = new System.Collections.Generic.HashSet<string>();
            bool nameUnique = true;
            for (int i = 0; i < CameraPreset.Count; i++)
            {
                if (!names.Add(CameraPreset.DisplayName((CameraPresetId)i))) nameUnique = false;
            }
            Assert("6 个预设显示名互不相同", nameUnique);

            var all = CameraPreset.BuildAll(anchor);
            Assert("BuildAll 返回 6 条非空状态", all != null && all.Length == 6);
            int nullCount = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] == null) nullCount++;
            Assert("无 null 预设", nullCount == 0);

            int plausible = 0;
            for (int i = 0; i < all.Length; i++) if (CameraPreset.IsPlausible(all[i])) plausible++;
            Assert($"6 个预设机位全部落在房间内（{plausible}/6）", plausible == 6);

            bool offsetOk = true;
            for (int i = 1; i < CameraPreset.Count; i++)
            {
                var expected = anchor + CameraPreset.Offsets[i];
                if ((all[i].position - expected).sqrMagnitude > 1e-6f) offsetOk = false;
            }
            Assert("非 Current 预设位置等于锚点加偏移", offsetOk);

            bool facing = true;
            for (int i = 1; i < CameraPreset.Count; i++)
            {
                float dot = Vector3.Dot(all[i].CameraRotation * Vector3.forward, (anchor - all[i].position).normalized);
                if (dot < 0.99f) facing = false;
            }
            Assert("5 个预设朝向均指向主体锚点（dot>=0.99）", facing);

            Assert("俯拍机位标记为 High、仰拍标记为 Low",
                all[3].angle == ShotAngle.High && all[4].angle == ShotAngle.Low);

            bool aspectOk = true;
            for (int i = 0; i < all.Length; i++)
                if (Mathf.Abs(all[i].aspect - ShotCameraState.DefaultAspect) > 1e-4f) aspectOk = false;
            Assert("全部预设默认 16:9 画幅", aspectOk);

            var repeat = CameraPreset.Build(CameraPresetId.Front, anchor);
            Assert("同一输入两次构建结果一致（纯函数）",
                (repeat.position - all[1].position).sqrMagnitude < 1e-8f &&
                Mathf.Abs(repeat.fieldOfView - all[1].fieldOfView) < 1e-6f);

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"CAMERA-PRESET-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"CAMERA-PRESET-RESULT=PASS ({passCount}/{ExpectedAssertions})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
