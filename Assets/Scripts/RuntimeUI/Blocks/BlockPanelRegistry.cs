using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 一块可拖拽的"积木"面板。挂在板的根节点上，只存身份与尺寸，不含任何交互逻辑。
    /// 位姿由 <see cref="BlockPanelRegistry"/> 统一计算，板自己不记住"该在哪"。
    /// </summary>
    public sealed class BlockPanelSlot : MonoBehaviour
    {
        [Tooltip("板标识，用于排序与存档；留空时用 GameObject 名字。")]
        public string panelId = "";

        [Tooltip("板的世界尺寸（宽×高，米）。默认大板 0.62×0.42。")]
        public Vector2 sizeMeters = new Vector2(0.62f, 0.42f);

        [Tooltip("排序权重，小的排左边。同权重时按注册顺序。")]
        public int order;

        public string Id => string.IsNullOrEmpty(panelId) ? name : panelId;
    }

    /// <summary>
    /// 积木平铺的布局中枢：把注册进来的板按**弧面**排到用户面前。
    ///
    /// 为什么是弧面而不是并排（这条是规格里的硬结论，改之前先读）：
    /// @0.70 m 处一块 0.62 m 宽的板占 47.8°，已经吃满 ±25° 水平舒适区；
    /// 两块并排要 85.8°，超出 ±35° 可接受上限。所以多板只能沿弧面布，
    /// 板心角间距取 <see cref="panelSpacingDeg"/>=55°（>47.8°，保证不互相重叠），
    /// 靠转头切换焦点，而不是靠"一眼看全"。
    ///
    /// 本类只算位姿与吸附，不做拖拽抓取。抓取属 XRI 交互层，需真机手感标定，
    /// 留作后续接入；当前提供 <see cref="SnapToSteps"/> 供拖拽结束时调用。
    /// </summary>
    public sealed class BlockPanelRegistry : MonoBehaviour
    {
        [Header("绑定")]
        [Tooltip("头部锚点，弧面以它为圆心。留空时 Awake 取主相机。")]
        public Transform headAnchor;

        [Header("弧面布局")]
        [Tooltip("相邻板中心的角度间距（度）。必须大于单块板的占角，否则会重叠。")]
        [Range(20f, 90f)] public float panelSpacingDeg = 55f;

        [Tooltip("板中心相对眼睛的高度下沉量，米。正数表示低于视线。")]
        [Range(0f, 0.4f)] public float verticalDrop = 0.10f;

        [Header("距离档")]
        [Tooltip("拖拽松手后吸附到的距离档，米。必须递增。")]
        public float[] distanceSteps = { 0.60f, 0.70f, 0.80f };

        [Tooltip("当前使用的距离档索引。")]
        public int currentStepIndex = 1;

        private readonly List<BlockPanelSlot> _slots = new List<BlockPanelSlot>();

        public IReadOnlyList<BlockPanelSlot> Slots => _slots;
        public int Count => _slots.Count;

        /// <summary>当前档距离（米）。数组异常时回退到 0.70。</summary>
        public float CurrentDistance =>
            distanceSteps != null && distanceSteps.Length > 0
                ? distanceSteps[Mathf.Clamp(currentStepIndex, 0, distanceSteps.Length - 1)]
                : 0.70f;

        private void Awake()
        {
            if (headAnchor == null)
            {
                var cam = Camera.main;
                headAnchor = cam != null ? cam.transform : null;
            }
        }

        public void Register(BlockPanelSlot slot)
        {
            if (slot == null || _slots.Contains(slot)) return;
            _slots.Add(slot);
            _slots.Sort(Compare);
        }

        public void Unregister(BlockPanelSlot slot)
        {
            if (slot != null) _slots.Remove(slot);
        }

        /// <summary>按当前头朝向把全部板重排到弧面上。</summary>
        public void Layout()
        {
            if (headAnchor == null || _slots.Count == 0) return;

            _slots.Sort(Compare);

            Vector3 headPos = headAnchor.position;
            float baseYaw = headAnchor.eulerAngles.y;
            float distance = CurrentDistance;
            float center = (_slots.Count - 1) * 0.5f;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                float yaw = baseYaw + (i - center) * panelSpacingDeg;
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

                Vector3 pos = headPos + yawRot * Vector3.forward * distance;
                pos.y = headPos.y - verticalDrop;

                // uGUI 的正面朝 -Z，所以 +Z 必须背离观察者
                Vector3 away = pos - headPos;
                away.y = 0f;
                Quaternion rot = away.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(away.normalized, Vector3.up)
                    : yawRot;

                slot.transform.SetPositionAndRotation(pos, rot);
            }
        }

        /// <summary>把某块板的当前位置吸附到最近的距离档，返回吸附后的位置。</summary>
        public Vector3 SnapToSteps(Vector3 worldPos)
        {
            if (headAnchor == null || distanceSteps == null || distanceSteps.Length == 0)
                return worldPos;

            float horizontal = HorizontalDistance(headAnchor.position, worldPos);
            int best = 0;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < distanceSteps.Length; i++)
            {
                float delta = Mathf.Abs(distanceSteps[i] - horizontal);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }

            currentStepIndex = best;
            float target = distanceSteps[best];
            Vector3 dir = worldPos - headAnchor.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return worldPos;

            Vector3 snapped = headAnchor.position + dir.normalized * target;
            snapped.y = Mathf.Max(headAnchor.position.y - verticalDrop, worldPos.y);
            return snapped;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static int Compare(BlockPanelSlot x, BlockPanelSlot y)
        {
            int byOrder = x.order.CompareTo(y.order);
            if (byOrder != 0) return byOrder;
            return string.CompareOrdinal(x.Id, y.Id);
        }
    }
}
