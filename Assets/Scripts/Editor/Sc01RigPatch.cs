#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B4 交互骨架：修两处会让玩家第一眼就出戏的硬伤。
    /// 1) 工作台画布 localPosition 被写成 y=0，1.9 米宽的面板半埋进地板（门口看就是一块"半透明玻璃板"）；
    /// 2) XROrigin 在原点，而原点正好落在入口墙体内 —— 玩家出生在墙里。
    /// </summary>
    internal static class Sc01RigPatch
    {
        private const string CanvasName = "Spatial_Workbench_Canvas";
        private const string OriginName = "XR_Origin";
        private const float WallInnerX = 2.95f;
        private const float StandingEyeHeight = 1.65f;

        private static readonly Vector3 CanvasPos = new Vector3(2.15f, 1.35f, 1.50f);
        private static readonly Vector3 OriginPos = new Vector3(-2.20f, 0f, 0.55f);
        private const float OriginYaw = 54f;

        [MenuItem("ShotField/SC01/Patch B4 工作台与出生点")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("Rig");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("RIG");
        }

        internal static bool Apply()
        {
            var canvas = Sc01PatchCore.FindDeep(CanvasName) as RectTransform;
            if (canvas == null) Sc01PatchCore.Check(false, "B4.1 工作台画布", "缺失或不是 RectTransform");
            else
            {
                canvas.position = CanvasPos;
                // 面板实际宽度受 sizeDelta 影响，硬编码 X 必然算不准：实测四角，超出墙内面就整体内推。
                var maxX = MaxX(canvas);
                if (maxX > WallInnerX - 0.01f)
                {
                    var shift = maxX - (WallInnerX - 0.01f);
                    var p = canvas.position;
                    canvas.position = new Vector3(p.x - shift, p.y, p.z);
                    Debug.Log("[SC01-PATCH] 画布右角 " + maxX.ToString("F3") + " 越墙，内推 " +
                              shift.ToString("F3") + " 米");
                }
                var lowest = float.MaxValue;
                foreach (var corner in Corners(canvas)) lowest = Mathf.Min(lowest, corner.y);
                Sc01PatchCore.Check(lowest > 0.05f, "B4.1 画布已离开地面",
                    "最低角 y=" + lowest.ToString("F3"));
            }

            var origin = Sc01PatchCore.FindDeep(OriginName);
            if (origin == null) Sc01PatchCore.Check(false, "B4.2 出生点", "缺失");
            else
            {
                origin.position = OriginPos;
                origin.rotation = Quaternion.Euler(0f, OriginYaw, 0f);
                var yaw = Mathf.DeltaAngle(OriginYaw, origin.eulerAngles.y);
                Sc01PatchCore.Check(Sc01PatchCore.Near(origin.position, OriginPos, 0.005f) &&
                                    Mathf.Abs(yaw) < 0.5f, "B4.2 出生点位于门洞并朝向棚内",
                    "pos=" + origin.position + " yaw=" + origin.eulerAngles.y.ToString("F1"));
            }

            Sc01PatchCore.Check(SetCameraYOffset(StandingEyeHeight), "B4.3 站立眼高 1.65 米");

            if (canvas != null)
            {
                var maxX = MaxX(canvas);
                Sc01PatchCore.Check(maxX < WallInnerX, "B4.4 画布不穿右墙",
                    "最右角 x=" + maxX.ToString("F3") + " 墙内面=" + WallInnerX);
            }
            return Sc01PatchCore.AllPassed;
        }

        private static float MaxX(RectTransform rt)
        {
            var maxX = float.MinValue;
            foreach (var corner in Corners(rt)) maxX = Mathf.Max(maxX, corner.x);
            return maxX;
        }

        private static Vector3[] Corners(RectTransform rt)
        {
            var rect = rt.rect;
            return new[]
            {
                rt.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f)),
                rt.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f)),
                rt.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f)),
                rt.TransformPoint(new Vector3(rect.xMax, rect.yMax, 0f))
            };
        }

        private static bool SetCameraYOffset(float value)
        {
            var origin = Sc01PatchCore.FindDeep(OriginName);
            var component = origin == null ? null : origin.GetComponent<XROrigin>();
            if (component == null)
            {
                Debug.LogError("[SC01-PATCH] " + OriginName + " 上没有 XROrigin 组件");
                return false;
            }
            var so = new SerializedObject(component);
            var prop = so.FindProperty("m_CameraYOffset");
            if (prop == null)
            {
                Debug.LogError("[SC01-PATCH] XROrigin 找不到 m_CameraYOffset");
                return false;
            }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Mathf.Abs(component.CameraYOffset - value) < 0.001f;
        }
    }
}
#endif
