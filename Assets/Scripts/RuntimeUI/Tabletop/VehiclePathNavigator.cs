using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI.Tabletop
{
    public enum VehicleDomainMode { Ground, Air }

    /// <summary>
    /// 地空双模载具寻路器：统一调度沙盘中的马车/汽车（地面避障）与太空飞船/无人机（3D样条与侧倾）。
    /// </summary>
    [ExecuteAlways]
    public class VehiclePathNavigator : MonoBehaviour
    {
        [Header("运行域与路径")]
        public VehicleDomainMode domainMode = VehicleDomainMode.Ground;
        public Vector3[] waypoints = new Vector3[0];
        [Range(0.1f, 3.0f)] public float bankFactor = 1.2f;

        [Header("探针前向大灯挂载")]
        public Transform headlightMount;
        public float headlightRange = 6.0f;
        public float headlightSpotAngle = 40.0f;

        [Header("地面避障雕刻")]
        public NavMeshObstacle obstacle;

        private static readonly Vector3[] DefaultGroundPoints = new[]
        {
            new Vector3(-2.0f, 0.0f, -1.5f),
            new Vector3(-0.5f, 0.0f,  0.5f),
            new Vector3( 1.0f, 0.0f,  1.5f),
            new Vector3( 2.5f, 0.0f, -0.5f)
        };

        private static readonly Vector3[] DefaultAirPoints = new[]
        {
            new Vector3(-2.5f, 1.2f, -2.0f),
            new Vector3(-1.0f, 2.4f,  0.0f),
            new Vector3( 1.2f, 1.8f,  1.8f),
            new Vector3( 2.8f, 3.0f, -1.0f)
        };

        void Awake() => EnsureComponents();
        void OnValidate() => EnsureComponents();

        public void EnsureComponents()
        {
            if (headlightMount == null)
            {
                var child = transform.Find("HeadlightMount");
                if (child != null) headlightMount = child;
                else
                {
                    var mountGo = new GameObject("HeadlightMount");
                    mountGo.transform.SetParent(transform, false);
                    mountGo.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);
                    headlightMount = mountGo.transform;
                }
            }

            if (obstacle == null) obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null && domainMode == VehicleDomainMode.Ground)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.size = new Vector3(1.2f, 1.0f, 2.0f);
            }
            if (obstacle != null)
            {
                obstacle.enabled = (domainMode == VehicleDomainMode.Ground);
            }
        }

        public Vector3[] GetActiveWaypoints()
        {
            if (waypoints != null && waypoints.Length >= 2) return waypoints;
            return domainMode == VehicleDomainMode.Ground ? DefaultGroundPoints : DefaultAirPoints;
        }

        public Vector3 EvaluateSpline(float t, Vector3[] pts)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            if (pts.Length == 1) return pts[0];
            t = Mathf.Clamp01(t);
            float fIdx = t * (pts.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(fIdx), pts.Length - 2);
            float u = fIdx - i;

            Vector3 p0 = i > 0 ? pts[i - 1] : pts[i];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < pts.Length) ? pts[i + 2] : p2;

            return 0.5f * ((2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) +
                (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u));
        }

        public Vector3 EvaluateTangent(float t, Vector3[] pts)
        {
            float dt = 0.01f;
            Vector3 a = EvaluateSpline(Mathf.Max(0f, t - dt), pts);
            Vector3 b = EvaluateSpline(Mathf.Min(1f, t + dt), pts);
            Vector3 tan = b - a;
            return tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector3.forward;
        }

        public void EvaluateTrajectory(float normT)
        {
            var pts = GetActiveWaypoints();
            Vector3 pos = EvaluateSpline(normT, pts);
            Vector3 tangent = EvaluateTangent(normT, pts);

            if (obstacle != null && obstacle.enabled != (domainMode == VehicleDomainMode.Ground))
                obstacle.enabled = (domainMode == VehicleDomainMode.Ground);

            Quaternion rot;
            if (domainMode == VehicleDomainMode.Air)
            {
                // 空中模式：3D航向与转弯侧倾横滚 (Banking)
                Vector3 prevTan = EvaluateTangent(Mathf.Max(0f, normT - 0.03f), pts);
                Vector3 nextTan = EvaluateTangent(Mathf.Min(1f, normT + 0.03f), pts);
                float turnAngle = Vector3.SignedAngle(prevTan, nextTan, Vector3.up);
                float bank = Mathf.Clamp(-turnAngle * bankFactor * 15f, -50f, 50f);
                Vector3 upRef = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                rot = Quaternion.LookRotation(tangent, upRef) * Quaternion.Euler(0f, 0f, bank);
            }
            else
            {
                // 地面模式：沿水平面平滑偏航转向，锁定Roll与倾覆
                Vector3 flatTan = Vector3.ProjectOnPlane(tangent, Vector3.up);
                if (flatTan.sqrMagnitude < 1e-4f) flatTan = Vector3.forward;
                rot = Quaternion.LookRotation(flatTan.normalized, Vector3.up);
            }

            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
