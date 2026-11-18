using System;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 对标小云雀「机位库」的六种预设。纯几何计算，不读场景、不碰 UI。
    /// 只产出/改写 ShotCameraState，不另建相机状态结构（复用 ShotData.cs 的同一份定义）。
    /// </summary>
    public enum CameraPresetId
    {
        Current = 0,      // 当前视角
        Front = 1,        // 正面（从门口方向看向主体）
        Side = 2,         // 侧面
        Top = 3,          // 俯拍
        Low = 4,          // 仰拍
        OverShoulder = 5  // 过肩
    }

    public static class CameraPreset
    {
        public const int Count = 6;

        public static readonly Vector3[] Offsets =
        {
            Vector3.zero,                  // Current：不偏移
            new Vector3(0f, 0.74f, -1.5f),  // Front
            new Vector3(-1.8f, 0.64f, 0f),  // Side
            new Vector3(0f, 2.14f, -0.6f),  // Top
            new Vector3(0f, -0.11f, -1.2f), // Low
            new Vector3(1f, 1.09f, -1.7f)   // OverShoulder
        };

        public static readonly float[] FieldOfViews = { 45f, 45f, 50f, 60f, 55f, 40f };

        public static string DisplayName(CameraPresetId id)
        {
            switch (id)
            {
                case CameraPresetId.Current: return "当前视角";
                case CameraPresetId.Front: return "正面";
                case CameraPresetId.Side: return "侧面";
                case CameraPresetId.Top: return "俯拍";
                case CameraPresetId.Low: return "仰拍";
                case CameraPresetId.OverShoulder: return "过肩";
                default: return id.ToString();
            }
        }

        /// <summary>
        /// 以主体锚点算一个预设机位。解析式给出，不依赖场景里已有相机，
        /// 便于机位库在没有渲染环境的 batchmode 里逐条验证。
        /// </summary>
        public static ShotCameraState Build(CameraPresetId id, Vector3 anchor)
        {
            var state = new ShotCameraState();
            if (id == CameraPresetId.Current)
            {
                state.position = anchor;
                state.rotation = Quaternion.identity;
                state.fieldOfView = 45f;
                state.aspect = ShotCameraState.DefaultAspect;
                state.usesUnityCameraRotation = true;
                state.heightAboveFloor = anchor.y;
                return state;
            }

            int index = (int)id;
            Vector3 pos = anchor + Offsets[index];
            Vector3 forward = anchor - pos;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;

            state.position = pos;
            state.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            state.usesUnityCameraRotation = true;
            state.fieldOfView = FieldOfViews[index];
            state.aspect = ShotCameraState.DefaultAspect;
            state.heightAboveFloor = pos.y;
            state.shotSize = ShotSize.Medium;
            state.angle = index == 3 ? ShotAngle.High : (index == 4 ? ShotAngle.Low : ShotAngle.EyeLevel);
            return state;
        }

        /// <summary>把预设直接应用到场景相机；Current 保留相机现状。</summary>
        public static ShotCameraState Apply(CameraPresetId id, Camera cam, Vector3 anchor)
        {
            if (cam == null) return Build(id, anchor);
            if (id == CameraPresetId.Current)
            {
                var current = ShotCameraState.FromCamera(cam);
                return current;
            }
            var state = Build(id, anchor);
            state.ApplyTo(cam);
            return state;
        }

        public static ShotCameraState[] BuildAll(Vector3 anchor)
        {
            var all = new ShotCameraState[Count];
            for (int i = 0; i < Count; i++) all[i] = Build((CameraPresetId)i, anchor);
            return all;
        }

        /// <summary>
        /// 机位必须能在房间内立得住：位置落在 6×4m 地面范围、高度不穿地。
        /// 用于验收断言，避免预设把相机推到墙外。
        /// </summary>
        public static bool IsPlausible(ShotCameraState state)
        {
            if (state == null) return false;
            var p = state.position;
            return p.x >= -3f && p.x <= 3f
                && p.z >= 0f && p.z <= 4f
                && p.y >= 0.2f && p.y <= 2.8f
                && state.fieldOfView > 10f && state.fieldOfView < 120f;
        }
    }
}
