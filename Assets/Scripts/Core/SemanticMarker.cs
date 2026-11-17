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
            var center = CriticalFaceCenter;
            var n = CriticalFaceNormalWorld;

            // 在关键面所在平面上构造两条正交切线
            var up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(n, up)) > 0.9f) up = Vector3.right;
            var right = Vector3.Cross(up, n).normalized;
            var realUp = Vector3.Cross(n, right).normalized;

            float hw = criticalFaceSize.x * 0.5f;
            float hh = criticalFaceSize.y * 0.5f;

            return new Vector3[]
            {
                center,
                center + right * hw + realUp * hh,
                center - right * hw + realUp * hh,
                center + right * hw - realUp * hh,
                center - right * hw - realUp * hh
            };
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

        /// <summary>场景里所有带标记的对象。</summary>
        public static SemanticMarker[] All()
        {
            return Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Exclude);
        }

        public static SemanticMarker Find(string id)
        {
            var all = All();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].markerId == id) return all[i];
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
