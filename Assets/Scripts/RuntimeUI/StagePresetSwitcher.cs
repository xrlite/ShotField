using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 白模 / 天工宏大叙事舞台的一键切换。
    /// 天工二进制只存在本地 Resources/Stages，场景不序列化未入库资产引用。
    /// 当前只切换 1:1 舞台；进入沙盘时仍使用白模代理，避免把 17m 场景错误缩放进桌面。
    /// </summary>
    public class StagePresetSwitcher : MonoBehaviour
    {
        private const string TianGongResource = "Stages/TianGong_Android";

        public TabletopDirector tabletop;
        public GameObject grandStageRoot;

        public bool GrandStageActive { get; private set; }
        public string LastStageNote { get; private set; } = "";

        private void Awake()
        {
            if (tabletop == null) tabletop = GetComponentInChildren<TabletopDirector>(true);
            if (grandStageRoot == null) EnsureGrandStage();
            SetGrandStage(false);
        }

        public void SetGrandStage(bool useGrandStage)
        {
            if (tabletop != null && tabletop.currentMode != TabletopDirector.ScaleMode.Immersive1to1)
                tabletop.SwitchMode(TabletopDirector.ScaleMode.Immersive1to1);

            if (useGrandStage && !EnsureGrandStage())
            {
                LastStageNote = "天工预设未安装，保留白模舞台";
                return;
            }

            if (tabletop != null && tabletop.sceneRoot1to1 != null)
                tabletop.sceneRoot1to1.gameObject.SetActive(!useGrandStage);
            if (grandStageRoot != null) grandStageRoot.SetActive(useGrandStage);

            GrandStageActive = useGrandStage;
            LastStageNote = useGrandStage ? "天工宏大叙事舞台（1:1）" : "白模预演舞台（1:1）";
            Debug.Log($"[StagePresetSwitcher] {LastStageNote}");
        }

        public void ToggleStage() => SetGrandStage(!GrandStageActive);

        private bool EnsureGrandStage()
        {
            if (grandStageRoot != null) return true;
            var prefab = Resources.Load<GameObject>(TianGongResource);
            if (prefab == null) return false;

            grandStageRoot = Instantiate(prefab, transform);
            grandStageRoot.name = "TianGong_Android_Runtime";
            grandStageRoot.transform.localPosition = Vector3.zero;
            grandStageRoot.transform.localRotation = Quaternion.identity;
            grandStageRoot.transform.localScale = Vector3.one;
            return true;
        }
    }
}
