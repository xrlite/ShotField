using UnityEngine;
using ShotField.Core;

namespace ShotField.RuntimeUI
{
    public class TabletopDirector : MonoBehaviour
    {
        public enum ScaleMode { Immersive1to1, TabletopMini }

        [Header("沙盘配置")]
        public ScaleMode currentMode = ScaleMode.Immersive1to1;
        public float miniScale = 0.05f; // 1:20 缩放比
        public Transform tabletopAnchor; // 沙盘桌面中心锚点

        [Header("片场与沙盘根节点")]
        public Transform sceneRoot1to1;
        public Transform tabletopProxyRoot;

        [Header("相机与观察者")]
        public Transform observerCamera;

        private Vector3 savedImmersivePos;
        private Quaternion savedImmersiveRot;

        public Transform TabletopAnchor
        {
            get
            {
                if (tabletopAnchor == null)
                {
                    var anchorGo = new GameObject("TabletopAnchor");
                    anchorGo.transform.SetPositionAndRotation(new Vector3(0, 0.8f, 1.2f), Quaternion.identity);
                    tabletopAnchor = anchorGo.transform;
                }
                return tabletopAnchor;
            }
        }

        void Awake() => _ = TabletopAnchor;

        public void SwitchMode(ScaleMode targetMode)
        {
            if (currentMode == targetMode) return;
            var anchor = TabletopAnchor;
            if (targetMode == ScaleMode.TabletopMini)
            {
                if (observerCamera != null)
                {
                    savedImmersivePos = observerCamera.position;
                    savedImmersiveRot = observerCamera.rotation;
                    observerCamera.position = anchor.position + new Vector3(0, 0.6f, -0.8f);
                    observerCamera.LookAt(anchor.position);
                }
                currentMode = ScaleMode.TabletopMini;
                SetTabletopActive(true);
            }
            else
            {
                if (observerCamera != null && savedImmersivePos != Vector3.zero)
                {
                    observerCamera.SetPositionAndRotation(savedImmersivePos, savedImmersiveRot);
                }
                currentMode = ScaleMode.Immersive1to1;
                SetTabletopActive(false);
            }
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
        }
    }
}
