#if UNITY_EDITOR
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    internal static class SmokeScene
    {
        internal static void Build(System.Action<string> line)
        {
            var existing = Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Exclude);
            if (existing.Length > 0)
            {
                line("场景里已有标记对象，直接复用");
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.localScale = new Vector3(1.6f, 0.05f, 0.8f);
            table.transform.position = new Vector3(0f, 0.55f, 0f);

            var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            a.name = "ActorA";
            a.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            a.transform.position = new Vector3(-1.1f, 0.9f, 0.6f);
            var mA = a.AddComponent<SemanticMarker>();
            mA.kind = SemanticMarker.Kind.ActorPrimary; mA.markerId = "actorA"; mA.displayName = "沈默";

            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            b.name = "ActorB";
            b.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            b.transform.position = new Vector3(1.1f, 0.9f, 0.6f);
            var mB = b.AddComponent<SemanticMarker>();
            mB.kind = SemanticMarker.Kind.ActorSecondary; mB.markerId = "actorB"; mB.displayName = "苏明";

            var env = GameObject.CreatePrimitive(PrimitiveType.Cube);
            env.name = "Envelope";
            env.transform.localScale = new Vector3(0.16f, 0.011f, 0.11f);
            env.transform.position = new Vector3(0f, 0.583f, 0f);
            env.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 局部 +Z → 世界 -Z
            var mE = env.AddComponent<SemanticMarker>();
            mE.kind = SemanticMarker.Kind.KeyProp; mE.markerId = "envelope"; mE.displayName = "信封";
            mE.criticalFaceNormal = Vector3.forward;
            mE.criticalFaceMustStayHidden = true;
            mE.criticalFaceSize = new Vector2(0.16f, 0.11f);

            var host = new GameObject("Constraints");
            var vis = host.AddComponent<VisibilityConstraint>(); vis.ruleName = "信封正面不可见"; vis.targetMarkerId = "";
            var ax = host.AddComponent<AxisConstraint>(); ax.ruleName = "摄影机不得越轴"; ax.actorAId = "actorA"; ax.actorBId = "actorB";
            var fr = host.AddComponent<FramingConstraint>(); fr.ruleName = "主角A在画面左侧"; fr.subjectId = "actorA"; fr.minViewportX = 0f; fr.maxViewportX = 0.5f;

            // 运行时创建的 Collider 必须同步一次，否则 Raycast 打不到
            Physics.SyncTransforms();

            line("已构建测试场景：地板 / 桌子 / 沈默 / 苏明 / 信封（正面朝世界 -Z）");
        }

        internal static void PlaceCamera(System.Action<string> line, Camera cam, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt, Vector3.up);
            Physics.SyncTransforms();
            line(string.Format("  [CAM] pos={0} lookAt={1} euler={2} forward={3}",
                pos.ToString("F2"), lookAt.ToString("F2"),
                cam.transform.rotation.eulerAngles.ToString("F1"),
                cam.transform.forward.ToString("F3")));
        }

        /// <summary>把「信封正面不可见」这条规则的中间量全部打印出来，定位卡在哪一步。</summary>
        internal static void DiagnoseEnvelope(ShotData shot, System.Action<string> line)
        {
            var marker = SemanticMarker.Find("envelope");
            if (marker == null) { line("  诊断：找不到信封 marker"); return; }

            var cam = shot.camera;
            line(string.Format("  诊断 cam.pos={0} euler={1} fov={2}",
                cam.position.ToString("F2"), cam.rotation.eulerAngles.ToString("F1"), cam.fieldOfView));
            line(string.Format("  诊断 faceCenter={0} normalWorld={1}",
                marker.CriticalFaceCenter.ToString("F2"), marker.CriticalFaceNormalWorld.ToString("F2")));

            var toCam = cam.position - marker.CriticalFaceCenter;
            float dot = Vector3.Dot(marker.CriticalFaceNormalWorld, toCam.normalized);
            line(string.Format("  诊断 朝向dot={0:F3}（>0 才算正面朝镜头）", dot));

            var samples = marker.SampleCriticalFace();
            for (int i = 0; i < samples.Length; i++)
            {
                var local = cam.ViewMatrix.MultiplyPoint3x4(samples[i]);
                var proj = cam.ProjectionMatrix();
                var clip = proj * new Vector4(local.x, local.y, local.z, 1f);
                float ndcX = clip.w > 0f ? clip.x / clip.w : -999f;
                float ndcY = clip.w > 0f ? clip.y / clip.w : -999f;
                bool inside = clip.w > 0f && ndcX >= -1f && ndcX <= 1f && ndcY >= -1f && ndcY <= 1f;

                bool occluded = false;
                string occluder = "";
                var dir = samples[i] - cam.position;
                float dist = dir.magnitude;
                var hits = Physics.RaycastAll(cam.position, dir.normalized, Mathf.Max(0.01f, dist - 0.02f), ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits.Length; h++)
                {
                    var t = hits[h].collider != null ? hits[h].collider.transform : null;
                    if (t == null) continue;
                    if (t == marker.transform || t.IsChildOf(marker.transform)) { occluder = "(自身已忽略)"; continue; }
                    occluded = true;
                    occluder = t.name;
                    break;
                }

                line(string.Format("    点{0} world={1} localZ={2:F2} w={3:F2} ndc=({4:F2},{5:F2}) 视锥内={6} 遮挡={7} {8}",
                    i, samples[i].ToString("F2"), local.z, clip.w, ndcX, ndcY, inside, occluded, occluder));
            }
        }


    }
}
#endif
