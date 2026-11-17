using UnityEngine;
using ShotField.Core;

namespace ShotField.Constraints
{
    /// <summary>
    /// 关键面可见性约束。
    ///
    /// 例：「信封正面绝对不能露」。
    /// 做法：对关键面取 5 个采样点（中心+四角），逐点判断
    ///   ① 是否落在摄影机视锥内（视口坐标 0..1 且 z 正）
    ///   ② 若在视锥内，从机位向该点发射线，是否被别的物体挡住
    /// 只要有任意一个采样点"在画面内且没被挡"，即判违规。
    /// </summary>
    public class VisibilityConstraint : ShotConstraint
    {
        [Tooltip("留空则检查场景里所有勾了 criticalFaceMustStayHidden 的对象")]
        public string targetMarkerId = "";

        [Tooltip("遮挡检测用的层。留空表示所有层")]
        public LayerMask occluderMask = ~0;

        [Tooltip("射线起点距机位的最小距离，避免打到自己")]
        public float epsilon = 0.02f;

        public override ConstraintSpec ToSpec()
        {
            var spec = base.ToSpec();
            spec.type = "prop_hidden";
            spec.propId = targetMarkerId;
            if (string.IsNullOrEmpty(spec.propId))
            {
                var targets = ResolveTargets();
                var ids = System.Array.ConvertAll(targets, marker => marker.markerId);
                System.Array.Sort(ids, System.StringComparer.Ordinal);
                spec.propId = string.Join(",", ids);
            }
            spec.face = "front";
            spec.epsilon = epsilon;
            spec.occluderMask = occluderMask.value;
            return spec;
        }

        public override ConstraintResult Evaluate(ShotData shot, ShotData previous)
        {
            if (shot == null) return ConstraintResult.Pending(ruleName, "无镜头数据", targetMarkerId);

            var cam = ShotCameraStateFor(shot);
            if (cam == null) return ConstraintResult.Pending(ruleName, "机位数据缺失", targetMarkerId);

            var targets = ResolveTargets();
            if (targets.Length == 0)
            {
                return ConstraintResult.Pending(ruleName, "场景中没有标记为「关键面不可见」的对象", targetMarkerId);
            }

            string worstId = "";
            string worstDetail = "";
            int visibleCount = 0;

            for (int t = 0; t < targets.Length; t++)
            {
                var marker = targets[t];

                // 朝向判断：关键面法线必须"朝向摄影机"，才算正面露出。
                // 少了这一步，信封背面朝着机位时，采样点依然落在视锥内，会一直误报。
                var faceNormal = marker.CriticalFaceNormalWorld;
                var toCamera = cam.position - marker.CriticalFaceCenter;
                if (toCamera.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(faceNormal, toCamera.normalized) <= 0f)
                {
                    continue;   // 正面背对摄影机，本镜不可能露出
                }

                var samples = marker.SampleCriticalFace();
                int visiblePoints = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    var p = samples[i];
                    if (!IsInsideViewport(cam, p)) continue;
                    if (IsOccluded(cam, p, marker.transform)) continue;
                    visiblePoints++;
                }

                if (visiblePoints > 0)
                {
                    visibleCount++;
                    if (worstId == "")
                    {
                        worstId = marker.markerId;
                        worstDetail = string.Format("{0} 的关键面有 {1}/{2} 个采样点出现在画面中",
                            string.IsNullOrEmpty(marker.displayName) ? marker.markerId : marker.displayName,
                            visiblePoints, samples.Length);
                    }
                }
            }

            if (visibleCount > 0)
            {
                return ConstraintResult.Violation(ruleName, worstDetail, worstId);
            }

            return ConstraintResult.Pass(ruleName,
                string.Format("{0} 个关键面均未入画", targets.Length), "");
        }

        private SemanticMarker[] ResolveTargets()
        {
            if (!string.IsNullOrEmpty(targetMarkerId))
            {
                var one = SemanticMarker.Find(targetMarkerId);
                return one != null ? new SemanticMarker[] { one } : new SemanticMarker[0];
            }

            var all = SemanticMarker.All();
            var list = new System.Collections.Generic.List<SemanticMarker>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].criticalFaceMustStayHidden) list.Add(all[i]);
            }
            return list.ToArray();
        }

        /// <summary>世界点在机位的视口内吗（0..1，z 为正）。</summary>
        private bool IsInsideViewport(ShotCameraState cam, Vector3 worldPoint)
        {
            var vp = cam.ViewMatrix;
            var local = vp.MultiplyPoint3x4(worldPoint);   // 相机空间，Unity 相机看向 -Z

            if (-local.z <= cam.nearClip) return false;    // 在机位后面或太近

            var proj = cam.ProjectionMatrix();
            // 注意：传入的 z 必须是相机空间的 local.z（前方为负）。
            // 之前写成 -local.z 会让 clip.w 变成负数，所有采样点都被判"不在视锥内"，
            // 结果是"信封正面不可见"永远判通过——这个 bug 是冒烟测试抓到的。
            var clip = proj * new Vector4(local.x, local.y, local.z, 1f);
            if (clip.w <= 0f) return false;

            float ndcX = clip.x / clip.w;
            float ndcY = clip.y / clip.w;

            return ndcX >= -1f && ndcX <= 1f && ndcY >= -1f && ndcY <= 1f;
        }

        /// <summary>
        /// 从机位到该点之间有无遮挡。
        /// 必须排除目标对象自身——否则射线终点就在信封上，会命中信封的碰撞体，
        /// 把自己的体积当成"遮挡物"，导致永远判"被挡住"。
        /// </summary>
        private bool IsOccluded(ShotCameraState cam, Vector3 worldPoint, Transform excludeRoot)
        {
            var origin = cam.position;
            var dir = worldPoint - origin;
            float dist = dir.magnitude;
            if (dist <= epsilon) return false;

            float castDist = dist - epsilon;
            if (castDist <= 0f) return false;

            var hits = Physics.RaycastAll(origin, dir.normalized, castDist, occluderMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null) continue;
                var t = c.transform;
                if (excludeRoot != null && (t == excludeRoot || t.IsChildOf(excludeRoot))) continue;
                return true;
            }
            return false;
        }

        /// <summary>shot 里没有 Camera 对象，用相机状态自己算。</summary>
        private ShotCameraState ShotCameraStateFor(ShotData shot)
        {
            return shot.camera;
        }
    }
}
