using UnityEngine;
using UnityEngine.XR;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 首屏 UI 不可见的定位埋点（临时诊断文件，定位完整文件移入 05 暂存目录，不进正式版）。
    /// 帧号按主理人 14:2x 实测对齐：Build 发生在 Displayed 之后 +7.07~+8.71 s，
    /// 故取 Build 后第 30 / 120 / 300 / 600 帧（约 0.5 / 2 / 5 / 10 s），**以 600 帧那次为结论依据**；
    /// FORCED ALPHA 也推到 600 帧，避免把「还在淡入」误判成「卡住」。
    /// 每条都带 Time.realtimeSinceStartup，便于与 Displayed / HubStatus 两条时间戳直接对齐。
    /// 单独成文件：WelcomeHubOverlay.Anchor.cs 已 158 行，埋点不能挤进去破 200 行预算。
    /// </summary>
    public sealed partial class WelcomeHubOverlay
    {
        private const string Diag = "[HUBDIAG]";
        private int _diagFrame;

        private static string A(CanvasGroup g) => g != null ? g.alpha.ToString("F2") : "NULL";

        private static string V(Vector3 v, string fmt) => v.ToString(fmt);

        /// <summary>Build 末尾打一次：相机身份、面板初值、世界空间 Canvas 的装配情况。</summary>
        private void DiagBuild()
        {
            var cam = Camera.main;
            var canvas = GetComponent<Canvas>();
            var root = GetComponent<CanvasGroup>();
            Debug.Log(Diag + " t=" + Time.realtimeSinceStartup.ToString("F2") + " build"
                + " camName=" + (cam != null ? cam.name : "NULL")
                + " camPos=" + (cam != null ? V(cam.transform.position, "F3") : "NULL")
                + " camEuler=" + (cam != null ? V(cam.transform.eulerAngles, "F1") : "NULL")
                + " headIsCam=" + (cam != null && _head == cam.transform)
                + " panel=" + V(transform.position, "F3")
                + " lossyScale=" + transform.lossyScale.x.ToString("F4")
                + " canvasEnabled=" + (canvas != null && canvas.enabled)
                + " worldCamIsCam=" + (canvas != null && cam != null && canvas.worldCamera == cam)
                + " rootA=" + A(root)
                + " | " + XrEnv());
        }

        /// <summary>第 30/150/600 帧快照：两级 alpha 分开打 + 世界缩放实测 + 是否被几何体挡住。</summary>
        private void DiagPose(int frame)
        {
            var cam = Camera.main;
            var root = GetComponent<CanvasGroup>();
            var panelGroup = _panel != null ? _panel.GetComponent<CanvasGroup>() : null;
            Vector3 panel = transform.position;
            Vector3 eye = _head != null ? _head.position : Vector3.zero;
            Vector3 vp = cam != null ? cam.WorldToViewportPoint(panel) : Vector3.zero;
            RaycastHit hit = default(RaycastHit);
            bool blocked = cam != null && Physics.Linecast(eye, panel, out hit);
            Debug.Log(Diag + " t=" + Time.realtimeSinceStartup.ToString("F2") + " f" + frame
                + " rootA=" + A(root)
                + " panelA=" + A(panelGroup)
                + " lossyScale=" + transform.lossyScale.x.ToString("F4")
                + " panelLossy=" + (_panel != null ? _panel.transform.lossyScale.x.ToString("F4") : "NULL")
                + " panelLocalScale=" + (_panel != null ? V(_panel.transform.localScale, "F2") : "NULL")
                + " headPos=" + V(eye, "F3")
                + " panelPos=" + V(panel, "F3")
                + " dist=" + Vector3.Distance(eye, panel).ToString("F2")
                + " headEuler=" + (_head != null ? V(_head.eulerAngles, "F1") : "NULL")
                + " vp=" + V(vp, "F3")
                + " locked=" + _anchorLocked
                + " pending=" + _lockPending
                + " blockedBy=" + (blocked ? hit.collider.name + "@" + hit.distance.ToString("F2") : "none")
                + (frame == 30 ? " | " + XrEnv() : ""));
        }

        /// <summary>
        /// 归档埋点（只读，把「谁加的 1.65 m」钉进 logcat）：XR 参考空间类型 / 头在 tracking 空间的局部 y /
        /// 相机世界 y / XR_Origin 世界位。rigWorld.y≈0 而 camWorldY≈3.25 → 位姿解析问题；rigWorld.y≈1.6 → rig 被搬过。
        /// </summary>
        private static string XrEnv()
        {
            var sb = new System.Text.StringBuilder();
            // 参考空间类型本版拿不到：Unity 6000.6 的 UnityEngine.XRModule 里只有 TrackingOriginModeFlags 这个类型，
            // 没有 trackingOriginMode 属性也没有 TryGetTrackingOriginMode 扩展（grep 二进制的结论）。
            // 该字段改由设备侧 logcat 提供：XR_REFERENCE_SPACE_TYPE_LOCAL_FLOOR + floor_height 1.650000。
            sb.Append("headLocalY=").Append(InputTracking.GetLocalPosition(XRNode.Head).y.ToString("F3"));
            var cam = Camera.main;
            sb.Append(" camWorldY=").Append(cam != null ? cam.transform.position.y.ToString("F3") : "null");
            var rig = GameObject.Find("XR_Origin");
            sb.Append(" rigWorld=").Append(rig != null ? rig.transform.position.ToString("F3") : "null");
            return sb.ToString();
        }
    }
}
