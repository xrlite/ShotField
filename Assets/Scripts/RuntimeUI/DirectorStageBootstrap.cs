using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 导演台进场守卫：只做三件场景 YAML 不方便承担的事。
    /// 1) 出生位姿兜底——XR_Origin 在场景里保存为 Y=0（贴地），真机追踪会覆盖，
    ///    但模拟器与"进入片场"的第一帧会直接看到墙角；这里在明显未初始化时抬到视高。
    /// 2) 1:1 首帧的 UI 避让——引导卡收起、胶囊栏下移，避免挡住茶几与信封。
    /// 3) 回沙盘时恢复，保证两种模式都能完整操作。
    /// 全部幂等、零场景写入；真机正常追踪（y 在视高附近）时不改动用户位置。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DirectorStageBootstrap : MonoBehaviour
    {
        private const string RigName = "XR_Origin";
        private const float EyeHeight = 1.6f;
        private const float ValidEyeMin = 0.9f;
        private static readonly Vector3 AudienceSpot = new Vector3(0f, 1.6f, 0.35f);
        private const float AudienceYaw = 12f;

        private TabletopDirector _tabletop;
        private Transform _rig;
        private TabletopDirector.ScaleMode _lastMode = (TabletopDirector.ScaleMode)(-1);
        private bool _spawnApplied;
        private float _wireDelay = 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<DirectorStageBootstrap>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("[DirectorStageBootstrap]");
            go.AddComponent<DirectorStageBootstrap>();
        }

        private void Update()
        {
            if (_tabletop == null) _tabletop = Object.FindAnyObjectByType<TabletopDirector>();
            if (_tabletop == null) return;
            if (_rig == null) _rig = ResolveRig();

            if (!_spawnApplied) ApplySpawnIfNeeded();

            // 视频对比播放器/工作台控件晚于 Awake 创建，3 秒后补一次时间轴接线。
            if (_wireDelay > 0f)
            {
                _wireDelay -= Time.deltaTime;
                if (_wireDelay <= 0f)
                {
                    var timeline = Object.FindAnyObjectByType<DirectorTimeline>(FindObjectsInactive.Include);
                    if (timeline != null) timeline.AutoWireReferences();
                    Debug.Log("[DirectorStage] 延迟补线 DirectorTimeline 完成");
                }
            }

            if (_tabletop.currentMode == _lastMode) return;
            _lastMode = _tabletop.currentMode;
            if (_lastMode == TabletopDirector.ScaleMode.Immersive1to1) ApplySpawnIfNeeded();
        }

        private static Transform ResolveRig()
        {
            var go = GameObject.Find(RigName);
            return go != null ? go.transform : null;
        }

        /// <summary>只在明显未初始化时介入，不覆盖真机追踪给出的合法位置。</summary>
        private void ApplySpawnIfNeeded()
        {
            if (_rig == null) return;
            bool tooLow = _rig.position.y < ValidEyeMin;
            bool tooFar = new Vector2(_rig.position.x, _rig.position.z).sqrMagnitude > 400f;
            if (!tooLow && !tooFar) { _spawnApplied = true; return; }

            _rig.position = AudienceSpot;
            _rig.rotation = Quaternion.Euler(0f, AudienceYaw, 0f);
            _spawnApplied = true;
            Debug.Log($"[DirectorStage] 出生位姿兜底 -> {AudienceSpot} yaw={AudienceYaw} " +
                      $"(tooLow={tooLow} tooFar={tooFar})");
        }
    }
}
