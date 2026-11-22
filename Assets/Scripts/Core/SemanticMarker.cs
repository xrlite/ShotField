using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 语义标记：告诉系统"这个物体是什么、哪个面是关键面"。
    /// 这是约束检查能成立的前提——系统不可能凭空理解三维语义，必须由人指定。
    /// </summary>
    public class SemanticMarker : MonoBehaviour
    {
        public enum Kind
        {
            ActorPrimary = 0,     // 主角 A
            ActorSecondary = 1,   // 主角 B
            KeyProp = 2,          // 关键道具（如信封）
            Prop = 3,             // 普通道具
            Environment = 4       // 环境结构
        }

        [Header("身份")]
        public Kind kind = Kind.Prop;
        [Tooltip("稳定 ID，写入快照与护照，改名不影响")]
        public string markerId = "";
        [Tooltip("给人看的名字，仅用于显示")]
        public string displayName = "";

        [Header("关键面（用于可见性约束）")]
        [Tooltip("关键面在局部空间的朝向。信封正面朝 +Z 时填 (0,0,1)")]
        public Vector3 criticalFaceNormal = Vector3.forward;

        [Tooltip("勾选后：该关键面出现在画面里即判违规")]
        public bool criticalFaceMustStayHidden = false;

        [Tooltip("关键面尺寸（宽×高，米），用于采样四角")]
        public Vector2 criticalFaceSize = new Vector2(0.16f, 0.11f);

        /// <summary>关键面的世界中心。</summary>
        public Vector3 CriticalFaceCenter
        {
            get { return transform.position; }
        }

        /// <summary>关键面的世界法线。</summary>
        public Vector3 CriticalFaceNormalWorld
        {
            get
            {
                var n = criticalFaceNormal;
                if (n.sqrMagnitude < 0.0001f) n = Vector3.forward;
                return transform.TransformDirection(n.normalized);
            }
        }

        /// <summary>关键面的 5 个采样点：中心 + 四角。用于判断"是否有一角露出"。</summary>
        public Vector3[] SampleCriticalFace()
        {
            var points = new Vector3[5];
            SampleCriticalFace(points);
            return points;
        }

        public void SampleCriticalFace(System.Span<Vector3> points)
        {
            if (points.Length < 5) throw new System.ArgumentException("关键面采样需要 5 个位置。", nameof(points));
            var center = CriticalFaceCenter;
            var n = CriticalFaceNormalWorld;

            // 在关键面所在平面上构造两条正交切线
            var up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(n, up)) > 0.9f) up = Vector3.right;
            var right = Vector3.Cross(up, n).normalized;
            var realUp = Vector3.Cross(n, right).normalized;

            float hw = criticalFaceSize.x * 0.5f;
            float hh = criticalFaceSize.y * 0.5f;

            points[0] = center;
            points[1] = center + right * hw + realUp * hh;
            points[2] = center - right * hw + realUp * hh;
            points[3] = center + right * hw - realUp * hh;
            points[4] = center - right * hw - realUp * hh;
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(markerId))
            {
                markerId = gameObject.name;
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = gameObject.name;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, SemanticMarker> Registry =
            new System.Collections.Generic.Dictionary<string, SemanticMarker>();

        void OnEnable()
        {
            if (string.IsNullOrEmpty(markerId)) return;
            if (Registry.TryGetValue(markerId, out var existing) && existing != null && existing != this)
                Debug.LogWarning($"[SemanticMarker] markerId 重复：\"{markerId}\" 被 \"{name}\" 覆盖，规则将指向后者。请保证场景内标记 ID 唯一。", this);
            Registry[markerId] = this;
        }

        void OnDisable()
        {
            if (!string.IsNullOrEmpty(markerId) && Registry.TryGetValue(markerId, out var m) && m == this)
                Registry.Remove(markerId);
        }

        /// <summary>运行时改 ID 并同步注册表，避免旧 ID 残留指向本物体。</summary>
        public void RebindId(string newId)
        {
            if (!string.IsNullOrEmpty(markerId) && Registry.TryGetValue(markerId, out var old) && old == this)
                Registry.Remove(markerId);
            markerId = string.IsNullOrEmpty(newId) ? gameObject.name : newId;
            if (gameObject.activeInHierarchy) Registry[markerId] = this;
        }

        /// <summary>场景里所有带标记的对象。</summary>
        public static SemanticMarker[] All(FindObjectsInactive inactive = FindObjectsInactive.Exclude)
        {
            return Object.FindObjectsByType<SemanticMarker>(inactive);
        }

        public static SemanticMarker Find(string id, FindObjectsInactive inactive = FindObjectsInactive.Exclude)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (inactive == FindObjectsInactive.Exclude && Registry.TryGetValue(id, out var m) && m != null && m.gameObject.activeInHierarchy)
                return m;
            var all = All(inactive);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].markerId == id)
                {
                    Registry[id] = all[i];
                    return all[i];
                }
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            if (!criticalFaceMustStayHidden) return;
            var n = CriticalFaceNormalWorld;
            Gizmos.color = new Color(0.85f, 0.19f, 0.15f, 0.9f);
            Gizmos.DrawLine(CriticalFaceCenter, CriticalFaceCenter + n * 0.25f);

            var pts = SampleCriticalFace();
            Gizmos.DrawLine(pts[1], pts[2]);
            Gizmos.DrawLine(pts[2], pts[4]);
            Gizmos.DrawLine(pts[4], pts[3]);
            Gizmos.DrawLine(pts[3], pts[1]);
        }
    }
}
