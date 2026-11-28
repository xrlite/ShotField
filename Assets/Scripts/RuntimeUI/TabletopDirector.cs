using UnityEngine;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public partial class TabletopDirector : MonoBehaviour
    {
        public enum ScaleMode { Immersive1to1, TabletopMini }

        [Header("沙盘配置")]
        public ScaleMode currentMode = ScaleMode.TabletopMini;
        public float miniScale = 0.05f; // 1:20 缩放比
        public Transform tabletopAnchor; // 沙盘桌面中心锚点

        [Header("片场与沙盘根节点")]
        public Transform sceneRoot1to1;
        public Transform tabletopProxyRoot;

        [Header("相机与观察者")]
        public Transform observerCamera;

        private bool hasSavedPose;
        private Vector3 savedImmersivePos;
        private Quaternion savedImmersiveRot;

        /// <summary>最近一次模式切换的可读说明，便于排查是否误动了头显相机。</summary>
        public string LastModeNote { get; private set; } = "";

        public Transform TabletopAnchor
        {
            get
            {
                if (tabletopAnchor == null)
                {
                    var anchorGo = new GameObject("TabletopAnchor");
                    anchorGo.transform.SetParent(transform);
                    anchorGo.transform.SetPositionAndRotation(new Vector3(0, 0.8f, 1.2f), Quaternion.identity);
                    tabletopAnchor = anchorGo.transform;
                }
                return tabletopAnchor;
            }
        }

        void Awake()
        {
            _ = TabletopAnchor;
            SetTabletopActive(currentMode == ScaleMode.TabletopMini);
        }

        public void SwitchMode(ScaleMode targetMode)
        {
            if (currentMode == targetMode) return;
            var anchor = TabletopAnchor;
            bool headCamera = IsHeadCamera(observerCamera);

            if (targetMode == ScaleMode.TabletopMini)
            {
                EnterTabletopMini(anchor, headCamera);
                currentMode = ScaleMode.TabletopMini;
                SetTabletopActive(true);
            }
            else
            {
                ExitTabletopMini(headCamera);
                currentMode = ScaleMode.Immersive1to1;
                SetTabletopActive(false);
            }
            Debug.Log($"[TabletopDirector] SwitchMode -> {currentMode} ({LastModeNote})");
        }

        // 进入沙盘模式：仅独立观察相机走保存/移机位姿分支；头显或未配置相机一律不动相机
        private void EnterTabletopMini(Transform anchor, bool headCamera)
        {
            if (observerCamera != null && !headCamera)
            {
                hasSavedPose = true;
                savedImmersivePos = observerCamera.position;
                savedImmersiveRot = observerCamera.rotation;
                observerCamera.position = anchor.position + new Vector3(0, 0.6f, -0.8f);
                observerCamera.LookAt(anchor.position);
                LastModeNote = "独立观察相机已切到沙盘俯视角";
                return;
            }
            LastModeNote = headCamera
                ? "观察者绑定的是 XR 头显，未移动相机，仅切换沙盘代理显隐"
                : "未配置独立观察相机，仅切换沙盘代理显隐";
            Debug.Log($"[TabletopDirector] {LastModeNote}");
        }

        // 退出沙盘模式：只恢复本组件自己存过的独立观察相机位姿
        private void ExitTabletopMini(bool headCamera)
        {
            if (observerCamera != null && !headCamera && hasSavedPose)
            {
                observerCamera.SetPositionAndRotation(savedImmersivePos, savedImmersiveRot);
                hasSavedPose = false;
                LastModeNote = "独立观察相机已恢复沉浸式位姿";
                Debug.Log($"[TabletopDirector] {LastModeNote}");
                return;
            }
            LastModeNote = "头显相机不受沙盘模式接管，未恢复任何位姿";
            Debug.Log($"[TabletopDirector] {LastModeNote}");
        }

        /// <summary>
        /// 判断目标是不是 XR 头显/主相机：位于 XROrigin/XR_Headset 层级下，
        /// 或本身带 Camera 且为 MainCamera 标签。这类相机一律不允许被搬动。
        /// </summary>
        private static bool IsHeadCamera(Transform target)
        {
            if (target == null) return false;
            var cam = target.GetComponent<Camera>();
            if (cam != null && target.CompareTag("MainCamera")) return true;

            var node = target;
            while (node != null)
            {
                if (node.name == "XR_Origin" || node.name == "XROrigin" || node.name == "XR_Headset") return true;
                node = node.parent;
            }
            return false;
        }

        public Vector3 WorldToTabletop(Vector3 worldPos)
        {
            Vector3 anchor = TabletopAnchor.position;
            Quaternion anchorRot = TabletopAnchor.rotation;
            Vector3 offset = worldPos - (sceneRoot1to1 != null ? sceneRoot1to1.position : Vector3.zero);
            return anchor + (anchorRot * (offset * miniScale));
        }

        public Vector3 TabletopToWorld(Vector3 tabletopPos)
        {
            Vector3 anchor = TabletopAnchor.position;
            Quaternion invAnchorRot = Quaternion.Inverse(TabletopAnchor.rotation);
            Vector3 offset = invAnchorRot * (tabletopPos - anchor);
            return (sceneRoot1to1 != null ? sceneRoot1to1.position : Vector3.zero) + (offset / (miniScale > 0.0001f ? miniScale : 0.05f));
        }

        public Quaternion WorldToTabletopRot(Quaternion worldRot) =>
            TabletopAnchor.rotation * worldRot;

        public Quaternion TabletopToWorldRot(Quaternion tabletopRot) =>
            Quaternion.Inverse(TabletopAnchor.rotation) * tabletopRot;

        public void SyncProxyToWorld(Transform proxy, Transform worldTarget)
        {
            if (proxy == null || worldTarget == null) return;
            worldTarget.position = TabletopToWorld(proxy.position);
            worldTarget.rotation = TabletopToWorldRot(proxy.rotation);
        }

        public void SyncWorldToProxy(Transform worldTarget, Transform proxy)
        {
            if (proxy == null || worldTarget == null) return;
            proxy.position = WorldToTabletop(worldTarget.position);
            proxy.rotation = WorldToTabletopRot(worldTarget.rotation);
        }

        private void SetTabletopActive(bool active)
        {
            if (tabletopProxyRoot != null)
                tabletopProxyRoot.gameObject.SetActive(active);
            // 仅当真实存在非空沙盘代理节点时才隐藏 1:1 建筑，避免无代理时出现全灰屏
            bool hasProxy = tabletopProxyRoot != null && tabletopProxyRoot.childCount > 0;
            if (hasProxy && sceneRoot1to1 != null)
                sceneRoot1to1.gameObject.SetActive(!active);
            SetStageRootsActive(!active);
        }
    }
}
