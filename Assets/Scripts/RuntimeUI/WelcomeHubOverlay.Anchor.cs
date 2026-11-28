using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 面板锁位（C-1）：hub 淡入结束后锁世界坐标，此后只有两种情况平滑重贴——
    /// 水平视角偏离 &gt; 45° 持续 0.8s，或头部相对锁定时刻锚点位移 &gt; 1.5m。
    /// 单独成文件：主文件守 200 行预算（照抄 ScaleToggleCapsule.Suppress 的 partial 拆法）。
    /// 判定全是静态纯函数，不依赖协程状态，可脱离运行时单测。
    /// </summary>
    public sealed partial class WelcomeHubOverlay
    {
        /// <summary>面板距头部的前向距离（米），沿用 A 批既有值。</summary>
        public const float AnchorDistance = 1.25f;
        /// <summary>面板相对视平线的抬高（米）。</summary>
        public const float AnchorEyeRise = 0.08f;
        /// <summary>视角偏离阈值（度）。杨亮 2026-11-28 13:33 拍板，勿改。</summary>
        public const float ReangleDegrees = 45f;
        /// <summary>偏离需连续持续的时长（秒），不到不重贴——甩一下头不该把面板闪走。</summary>
        public const float ReangleHoldSeconds = 0.8f;
        /// <summary>位移阈值（米）：走开这么远就够不到了。</summary>
        public const float ReplaceDistance = 1.5f;
        /// <summary>重贴过渡时长（秒），落在 0.3–0.4 区间；瞬移会让人失去空间定位。</summary>
        public const float ReposeSeconds = 0.35f;
        /// <summary>开场黑幕保持时长（秒），与 PlayIntro 共用同一份来源，别再写字面量。</summary>
        public const float IntroHoldSeconds = 1.2f;
        /// <summary>
        /// 追踪位姿有效的最小头高（米）。Build 期常读到 y=0（XR 位姿还没生效），低于此值一律当「未就绪」：
        /// 不跟头、不锁位、不做判定。
        /// </summary>
        public const float MinValidHeadY = 0.8f;
        /// <summary>
        /// 锁定后允许的垂直错位（米），超过就重贴——第三条重贴条件（与 45°/0.8s、1.5m 并列）。
        /// 2026-11-28 主理人裁定新增：yaw 与水平位移都看不见垂直方向的 1.75 m 错位。
        /// </summary>
        public const float MaxVerticalDrift = 0.5f;
        /// <summary>未锁位前的跟头阻尼，沿用旧实现，别改。</summary>
        private const float FollowLerp = 6f;

        private bool _anchorLocked;
        private bool _lockPending;
        private Vector3 _anchorHeadPosition;
        private float _offAngleSeconds;
        private bool _reposing;
        private float _reposeElapsed;
        private Vector3 _reposeFromPosition;
        private Quaternion _reposeFromRotation;
        private Vector3 _reposeToPosition;
        private Quaternion _reposeToRotation;

        // ── 纯判定（可单测，不碰 transform / Time） ──────────────────────

        /// <summary>
        /// 水平面内「头部朝向」与「头→面板」方向的夹角（度）。
        /// 先拍平 y 的理由：面板是按水平前向摆的，若用含俯仰的三维夹角，用户低头看沙盘
        /// 超过 45° 就会触发重贴，而重贴后夹角依旧超阈值 → 每 0.8s 抖一次，形成自激循环。
        /// 拍平后「重贴」必然把夹角拉回 0，条件自洽。
        /// </summary>
        public static float YawAngleTo(Vector3 headForward, Vector3 headToPanel)
        {
            var f = new Vector3(headForward.x, 0f, headForward.z);
            var d = new Vector3(headToPanel.x, 0f, headToPanel.z);
            if (f.sqrMagnitude < 1e-4f || d.sqrMagnitude < 1e-4f) return 0f;
            return Vector3.Angle(f, d);
        }

        /// <summary>头部是否已离开锁定时刻的锚点超过 <see cref="ReplaceDistance"/>。</summary>
        public static bool IsOutOfRange(Vector3 headPosition, Vector3 anchorHeadPosition)
            => (headPosition - anchorHeadPosition).sqrMagnitude > ReplaceDistance * ReplaceDistance;

        /// <summary>偏离计时与位移两项任一命中即重贴；位移是硬条件（够不到就等于没面板）。</summary>
        public static bool ShouldRepose(float secondsOffAngle, bool outOfRange)
            => outOfRange || secondsOffAngle >= ReangleHoldSeconds;

        /// <summary>0–1 平滑权重（smoothstep），保证过渡起止都不突。</summary>
        public static float ReposeSmooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>头部位姿是否已就绪（XR 追踪生效后才可信）。</summary>
        public static bool IsHeadPoseValid(Vector3 headPosition) => headPosition.y >= MinValidHeadY;

        /// <summary>面板与「眼平线 + 固定抬高」的垂直错位是否超阈值。</summary>
        public static bool IsVerticalDrift(float panelY, float headY)
            => Mathf.Abs(panelY - (headY + AnchorEyeRise)) > MaxVerticalDrift;

        /// <summary>面板摆在头部正前方的世界坐标（水平前向 + 固定抬高）。</summary>
        public static Vector3 FrontOfHead(Vector3 headPosition, Vector3 headForward)
        {
            var fwd = new Vector3(headForward.x, 0f, headForward.z);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            return headPosition + fwd.normalized * AnchorDistance + Vector3.up * AnchorEyeRise;
        }

        // ── 运行时 ──────────────────────────────────────────────────────

        /// <summary>hub 淡入结束（射线刚打开）那一刻锁世界坐标：此时面板已在人眼前，之后不再跟脸动。</summary>
        private void LockAnchorHere()
        {
            // 位姿未就绪（Build 期常读到 y=0）就挂起锁位：锁在「面板还在爬升」的半路上，
            // 正是 2026-11-28 首屏 UI 跑到视野外那次事故的直接成因（HUBDIAG f30 vp.y=-0.752）。
            if (_head == null || !IsHeadPoseValid(_head.position)) { _lockPending = true; return; }
            LockNow();
        }

        private void LockNow()
        {
            _lockPending = false;
            _anchorLocked = true;
            _anchorHeadPosition = _head.position;
            _offAngleSeconds = 0f;
        }

        /// <summary>淡入 + 锁位：WelcomeHubReveal 的时序不动，只在它收尾时挂回调。</summary>
        private void PlayRevealThenLock()
        {
            var reveal = gameObject.AddComponent<WelcomeHubReveal>();
            reveal.OnRevealed = LockAnchorHere;
            reveal.Play();
        }

        /// <summary>[重置] 按钮：把面板召回正前方并重新锁定（走同一条 0.35s 平滑过渡，不瞬移）。</summary>
        public void RecallToFront()
        {
            if (_head == null || !IsHeadPoseValid(_head.position)) return;
            BeginRepose();
        }

        private void TickPanelPose(bool snap)
        {
            _diagFrame++;
            if (_diagFrame == 30 || _diagFrame == 150 || _diagFrame == 600) DiagPose(_diagFrame);
            if (snap) { FollowHead(true); return; }
            if (_reposing) { StepRepose(); return; }
            bool poseReady = _head != null && IsHeadPoseValid(_head.position);
            if (_lockPending)
            {
                if (!poseReady) return;
                FollowHead(true);   // 位姿刚就绪：先摆到位再锁，别锁在半路
                LockNow();
                return;
            }
            if (!_anchorLocked) { FollowHead(false); return; }
            if (!poseReady) return;   // 位姿丢了就不做判定，免得拿未就绪坐标去比
            float yaw = YawAngleTo(_head.forward, transform.position - _head.position);
            _offAngleSeconds = yaw > ReangleDegrees ? _offAngleSeconds + Time.deltaTime : 0f;
            if (ShouldRepose(_offAngleSeconds, IsOutOfRange(_head.position, _anchorHeadPosition))
                || IsVerticalDrift(transform.position.y, _head.position.y)) BeginRepose();
        }

        /// <summary>未锁位前的跟头；位姿未就绪时面板原地不动，等追踪位姿到位。</summary>
        private void FollowHead(bool immediate)
        {
            if (_head == null || !IsHeadPoseValid(_head.position)) return;
            Vector3 target = FrontOfHead(_head.position, _head.forward);
            transform.position = immediate ? target : Vector3.Lerp(transform.position, target, Time.deltaTime * FollowLerp);
            Vector3 look = transform.position - _head.position;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        /// <summary>起一次重贴：记住起点与终点，锚点即刻更新到当前头部位置。</summary>
        private void BeginRepose()
        {
            if (_head == null || !IsHeadPoseValid(_head.position)) return;
            _reposeFromPosition = transform.position;
            _reposeFromRotation = transform.rotation;
            _reposeToPosition = FrontOfHead(_head.position, _head.forward);
            Vector3 look = _reposeToPosition - _head.position;
            _reposeToRotation = look.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(look.normalized, Vector3.up)
                : _reposeFromRotation;
            _reposeElapsed = 0f;
            _reposing = true;
            _anchorLocked = true;
            _anchorHeadPosition = _head.position;
            _offAngleSeconds = 0f;
        }

        private void StepRepose()
        {
            _reposeElapsed += Time.deltaTime;
            float k = ReposeSmooth(_reposeElapsed / ReposeSeconds);
            transform.position = Vector3.Lerp(_reposeFromPosition, _reposeToPosition, k);
            transform.rotation = Quaternion.Slerp(_reposeFromRotation, _reposeToRotation, k);
            if (_reposeElapsed >= ReposeSeconds)
            {
                transform.position = _reposeToPosition;
                transform.rotation = _reposeToRotation;
                _reposing = false;
            }
        }
    }
}
