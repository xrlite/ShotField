using System;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 三视角落地校准：站姿 / 坐姿 / 蹲姿三档使用姿态下，把 UI 与沙盘的高度基准
    /// 校准到"当前眼高减去一个合适的下沉量"，避免蹲下来做沙盘时面板飘在头顶、
    /// 或者站着时面板卡在胸口。
    ///
    /// 安全边界（这条是硬约束，不要为了"看起来对"去动它）：
    /// 本类**只读取头显相机的世界高度，绝不写入相机或 XROrigin 的位姿/高度偏移**。
    /// 头显相机位置由用户真实身体与硬件追踪决定，任何由软件"抬高/压低眼睛"的做法
    /// 都会导致世界尺度失真和眩晕。本项目已在 TabletopDirector.IsHeadCamera 里
    /// 立过同样的规矩。校准只作用于 UI 层（板的下沉量、沙盘锚点高度）。
    ///
    /// 三档怎么判：按眼高自动分档，也允许外部显式锁定（<see cref="LockPreset"/>）。
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class ViewHeightCalibration : MonoBehaviour
    {
        public enum ViewPreset { Standing, Seated, Crouched }

        [Header("绑定")]
        public Transform headAnchor;
        public BlockPanelRegistry blockRegistry;
        public TabletopDirector tabletop;

        [Header("分档阈值（眼高，米）")]
        [Tooltip("眼高不低于此值判为站姿。")]
        public float standingMinEyeHeight = 1.45f;

        [Tooltip("眼高不低于此值判为坐姿；低于此值判为蹲姿。")]
        public float seatedMinEyeHeight = 1.05f;

        [Header("各姿态的板下沉量（米）")]
        public float standingDrop = 0.10f;
        public float seatedDrop = 0.07f;
        public float crouchedDrop = 0.05f;

        [Header("沙盘锚点相对眼高的下降量（米）")]
        public float tabletopDrop = 0.60f;

        [Header("平滑")]
        [Range(1f, 30f)] public float followLerp = 6f;

        private ViewPreset _preset = ViewPreset.Standing;
        private float _eyeHeight = 1.65f;
        private float _targetDrop = 0.10f;
        private bool _locked;

        public ViewPreset CurrentPreset => _preset;
        public float EyeHeight => _eyeHeight;

        /// <summary>姿态跨档时触发（不会每帧触发）。</summary>
        public event Action<ViewPreset> PresetChanged;

        private void Awake()
        {
            if (headAnchor == null)
            {
                var cam = Camera.main;
                headAnchor = cam != null ? cam.transform : null;
            }
            if (blockRegistry == null) blockRegistry = FindAnyObjectByType<BlockPanelRegistry>();
            if (tabletop == null) tabletop = FindAnyObjectByType<TabletopDirector>();
        }

        private void Update()
        {
            if ((headAnchor == null || headAnchor.position.y < 0.30f) && Camera.main != null)
                headAnchor = Camera.main.transform;
            if (headAnchor == null) return;

            _eyeHeight = headAnchor.position.y;
            var detected = Detect(_eyeHeight);
            if (detected != _preset)
            {
                _preset = detected;
                _targetDrop = DropFor(_preset);
                PresetChanged?.Invoke(_preset);
            }

            // 板的下沉量平滑逼近目标，避免跨档时面板跳一下
            if (blockRegistry != null)
            {
                blockRegistry.verticalDrop = Mathf.Lerp(
                    blockRegistry.verticalDrop, _targetDrop, Time.deltaTime * followLerp);
            }

            ApplyTabletopHeight();
        }

        /// <summary>手动锁定到某一档，忽略自动判定。传 null 解除锁定。</summary>
        public void LockPreset(ViewPreset? preset)
        {
            _locked = preset.HasValue;
            if (!preset.HasValue) return;
            _preset = preset.Value;
            _targetDrop = DropFor(_preset);
            PresetChanged?.Invoke(_preset);
        }

        /// <summary>按眼高判定姿态档。</summary>
        public ViewPreset Detect(float eyeHeight)
        {
            if (_locked) return _preset;
            if (eyeHeight >= standingMinEyeHeight) return ViewPreset.Standing;
            if (eyeHeight >= seatedMinEyeHeight) return ViewPreset.Seated;
            return ViewPreset.Crouched;
        }

        public float DropFor(ViewPreset preset)
        {
            switch (preset)
            {
                case ViewPreset.Seated: return seatedDrop;
                case ViewPreset.Crouched: return crouchedDrop;
                default: return standingDrop;
            }
        }

        /// <summary>沙盘锚点高度跟随眼高下沉；只写 anchor，不碰任何相机。</summary>
        private void ApplyTabletopHeight()
        {
            if (tabletop == null) return;
            var anchor = tabletop.TabletopAnchor;
            if (anchor == null) return;

            Vector3 pos = anchor.position;
            float targetY = _eyeHeight - tabletopDrop;
            pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * followLerp);
            anchor.position = pos;
        }
    }
}
