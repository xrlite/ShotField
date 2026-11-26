using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 虚拟摄影机稳定驱动。此前 <see cref="VirtualCameraStabilizer"/> 只有 Editor 验证器在调用，
    /// 运行时没有任何人每帧喂位姿，等于"有代码没接线"。
    /// 本类把三档稳定接进运行时：Steadicam（平滑跟随）/ TripodLocked（锁地平线）/ HandheldNatural（几乎不过滤）。
    /// 只作用在虚拟摄影机（Director）上，不碰 XR 头显——头显位姿滤波会直接导致晕动。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class SteadicamDriver : MonoBehaviour
    {
        [Tooltip("稳定作用的虚拟摄影机；留空则取本对象上的 Camera")]
        public Camera targetCamera;
        [Tooltip("真正把滤波结果写回相机。默认关闭，避免干扰镜头回看。")]
        public bool applyToCamera;

        private VirtualCameraStabilizer _stabilizer;
        private Vector3 _rawPos;
        private Quaternion _rawRot;
        private bool _hasRaw;

        public CameraStabilizerMode Mode =>
            _stabilizer != null ? _stabilizer.mode : CameraStabilizerMode.Steadicam;

        public string ModeLabel
        {
            get
            {
                switch (Mode)
                {
                    case CameraStabilizerMode.Steadicam: return "斯坦尼康";
                    case CameraStabilizerMode.TripodLocked: return "三脚架";
                    default: return "手持";
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<SteadicamDriver>(FindObjectsInactive.Include) != null) return;

            // Director 在沙盘态是 inactive，GameObject.Find 找不到它；
            // 必须用含 inactive 的遍历，否则稳定器只在 1:1 态才挂得上。
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (cam == null || cam.gameObject.name != "Director") continue;
                if (cam.GetComponent<SteadicamDriver>() == null)
                    cam.gameObject.AddComponent<SteadicamDriver>();
                return;
            }
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            EnsureStabilizer();
        }

        /// <summary>供 UI 调用：手持 → 斯坦尼康 → 三脚架 循环。</summary>
        public string CycleMode()
        {
            EnsureStabilizer();
            if (_stabilizer == null) return "无稳定器";
            switch (_stabilizer.mode)
            {
                case CameraStabilizerMode.HandheldNatural:
                    _stabilizer.mode = CameraStabilizerMode.Steadicam;
                    break;
                case CameraStabilizerMode.Steadicam:
                    _stabilizer.mode = CameraStabilizerMode.TripodLocked;
                    break;
                default:
                    _stabilizer.mode = CameraStabilizerMode.HandheldNatural;
                    break;
            }
            _stabilizer.ResetFilters(_rawPos, _rawRot);
            Debug.Log("[Steadicam] mode=" + _stabilizer.mode);
            return ModeLabel;
        }

        public void SetMode(CameraStabilizerMode mode)
        {
            EnsureStabilizer();
            if (_stabilizer != null) _stabilizer.mode = mode;
        }

        private void EnsureStabilizer()
        {
            if (_stabilizer != null) return;
            _stabilizer = GetComponent<VirtualCameraStabilizer>();
            if (_stabilizer == null) _stabilizer = gameObject.AddComponent<VirtualCameraStabilizer>();
            if (_stabilizer.mode == CameraStabilizerMode.Steadicam && targetCamera != null)
                _rawPos = targetCamera.transform.position;
            if (targetCamera != null) _rawRot = targetCamera.transform.rotation;
            _hasRaw = targetCamera != null;
        }

        private void LateUpdate()
        {
            if (!applyToCamera || targetCamera == null) return;
            EnsureStabilizer();
            if (_stabilizer == null) return;

            var tr = targetCamera.transform;
            if (!_hasRaw) { _rawPos = tr.position; _rawRot = tr.rotation; _hasRaw = true; return; }

            float dt = Mathf.Max(0.001f, Time.deltaTime);
            var filteredPos = _stabilizer.FilterPosition(_rawPos, dt);
            var filteredRot = _stabilizer.FilterRotation(_rawRot, dt);
            if (_stabilizer.lockHorizon) filteredRot = _stabilizer.ApplyHorizonLock(filteredRot);
            tr.SetPositionAndRotation(filteredPos, filteredRot);

            _rawPos = tr.position;
            _rawRot = tr.rotation;
        }
    }
}
