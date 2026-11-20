using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间片场灯光控制台（对齐参考图）：
    /// 实时控制槽位灯光、内光束角(10°-90°)、模式(主/辅/环境)、范围、HSV色相/饱和度与焦点追踪。
    /// </summary>
    public class SpatialLightingConsole : MonoBehaviour
    {
        [Serializable]
        public class LightSlot
        {
            public string slotName = "槽位1";
            public Light lightSource;
            public SpatialStudioLightFixture fixture;
            public Transform focusTarget;
            public float innerSpotAngle = 10f;
            public int modeIndex = 2; // 0:主光 1/3, 1:辅光 2/3, 2:环境 3/3
            public float rangePercent = 0.60f;
            public float hue = 0f;
            public float saturation = 0.11f;
            public float maxRange = 6.0f;
            public float maxIntensity = 5.0f;
        }

        public LightSlot[] slots = new LightSlot[3];
        public int currentSlotIndex = 0;

        [Header("UI 绑定组件 (可选)")]
        public Text slotTitleText;
        public Text innerAngleText;
        public Text modeText;
        public Text rangeText;
        public Text hueText;
        public Text saturationText;
        public Image colorSwatchImage;
        public Text focusTargetText;

        public event Action<int, LightSlot> OnSlotUpdated;

        public static readonly string[] ModeNames = { "主光", "辅光", "环境" };

        void Awake()
        {
            if (slots == null || slots.Length == 0) InitializeDefaultSlots();
        }

        public void InitializeDefaultSlots()
        {
            slots = new LightSlot[3];
            slots[0] = new LightSlot { slotName = "槽位1", innerSpotAngle = 10f, modeIndex = 2, rangePercent = 0.60f, hue = 0f, saturation = 0.11f };
            slots[1] = new LightSlot { slotName = "槽位2", innerSpotAngle = 35f, modeIndex = 0, rangePercent = 0.85f, hue = 38f, saturation = 0.65f };
            slots[2] = new LightSlot { slotName = "槽位3", innerSpotAngle = 65f, modeIndex = 1, rangePercent = 0.45f, hue = 215f, saturation = 0.40f };
        }

        public LightSlot CurrentSlot => (slots != null && currentSlotIndex >= 0 && currentSlotIndex < slots.Length) ? slots[currentSlotIndex] : null;

        public void SelectSlot(int index)
        {
            if (slots == null || slots.Length == 0) return;
            currentSlotIndex = Mathf.Clamp(index, 0, slots.Length - 1);
            ApplySlot(CurrentSlot);
            RefreshUI();
        }

        public void CycleSlot() => SelectSlot((currentSlotIndex + 1) % slots.Length);

        public void SetInnerSpotAngle(float angle)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.innerSpotAngle = Mathf.Clamp(angle, 5f, 90f);
            ApplySlot(s);
            RefreshUI();
        }

        public void CycleMode(int direction = 1)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.modeIndex = (s.modeIndex + direction + ModeNames.Length) % ModeNames.Length;
            ApplySlot(s);
            RefreshUI();
        }

        public void SetRangePercent(float pct)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.rangePercent = Mathf.Clamp01(pct);
            ApplySlot(s);
            RefreshUI();
        }

        public void SetHue(float h)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.hue = Mathf.Repeat(h, 360f);
            ApplySlot(s);
            RefreshUI();
        }

        public void SetSaturation(float sat)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.saturation = Mathf.Clamp01(sat);
            ApplySlot(s);
            RefreshUI();
        }

        public void SetFocusTarget(Transform target)
        {
            var s = CurrentSlot;
            if (s == null) return;
            s.focusTarget = target;
            ApplySlot(s);
            RefreshUI();
        }

        public void ApplySlot(LightSlot slot)
        {
            if (slot == null || slot.lightSource == null) return;
            var c = Color.HSVToRGB(slot.hue / 360f, slot.saturation, 1f);
            slot.lightSource.color = c;
            slot.lightSource.range = Mathf.Max(0.5f, slot.maxRange * slot.rangePercent);
            slot.lightSource.intensity = Mathf.Max(0.1f, slot.maxIntensity * slot.rangePercent);
            if (slot.lightSource.type == LightType.Spot)
            {
                slot.lightSource.spotAngle = slot.innerSpotAngle;
#if UNITY_2019_3_OR_NEWER
                slot.lightSource.innerSpotAngle = Mathf.Min(slot.innerSpotAngle * 0.75f, slot.innerSpotAngle - 1f);
#endif
            }
            if (slot.focusTarget != null)
                slot.lightSource.transform.LookAt(slot.focusTarget.position);

            if (slot.fixture != null)
                slot.fixture.UpdateFixture(slot.innerSpotAngle, slot.lightSource.range, c, slot.focusTarget);

            OnSlotUpdated?.Invoke(currentSlotIndex, slot);
        }

        public void RefreshUI()
        {
            var s = CurrentSlot;
            if (s == null) return;
            if (slotTitleText != null) slotTitleText.text = s.slotName;
            if (innerAngleText != null) innerAngleText.text = $"{Mathf.RoundToInt(s.innerSpotAngle)}°";
            if (modeText != null) modeText.text = $"{ModeNames[s.modeIndex]}  {s.modeIndex + 1} / {ModeNames.Length}  < >";
            if (rangeText != null) rangeText.text = $"{Mathf.RoundToInt(s.rangePercent * 100f)}%";
            if (hueText != null) hueText.text = $"{Mathf.RoundToInt(s.hue)}°";
            if (saturationText != null) saturationText.text = $"{Mathf.RoundToInt(s.saturation * 100f)}%";
            var rgb = Color.HSVToRGB(s.hue / 360f, s.saturation, 1f);
            if (colorSwatchImage != null) colorSwatchImage.color = rgb;
            if (focusTargetText != null)
                focusTargetText.text = s.focusTarget != null ? $"灯光聚焦的对象: {s.focusTarget.name}" : "灯光聚焦的对象。";
        }
    }
}
