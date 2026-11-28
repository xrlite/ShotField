#if UNITY_EDITOR
// 目标位置 Assets/Scripts/Editor/WarmHallVisualChamfer.cs
// 纪律：不改 WarmHallVisualGeometry.cs（181/200 行）；新方法名与现有 21 个方法名无重名。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Dais 三件倒角（lathe 剖面）+ 原状恢复。tris = 剖面段数*sides*2 + 2*sides。</summary>
    internal static partial class WarmHallVisualBuilder
    {
        private static Mesh LatheMesh(string name, Vector2[] profile, int sides, bool caps)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var uv = new List<Vector2>(); var t = new List<int>();
            float y0 = profile[0].y, h = Mathf.Max(1e-4f, profile[profile.Length - 1].y - y0);

            for (int s = 0; s < profile.Length - 1; s++)
            {
                Vector2 a = profile[s], b = profile[s + 1];
                Vector2 dir = (b - a).normalized;
                Vector2 on = new Vector2(dir.y, -dir.x);        // 剖面外法线（x=径向, y=向上）
                int ring = v.Count;
                for (int r = 0; r < 2; r++)
                {
                    Vector2 p = r == 0 ? a : b;
                    for (int i = 0; i < sides; i++)
                    {
                        float ang = Mathf.PI * 2f * i / sides, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        v.Add(new Vector3(cs * p.x, p.y, sn * p.x));
                        n.Add(new Vector3(cs * on.x, on.y, sn * on.x));
                        uv.Add(new Vector2(i / (float)sides, (p.y - y0) / h));
                    }
                }
                for (int i = 0; i < sides; i++)
                {
                    int a0 = ring + i, a1 = ring + (i + 1) % sides, b0 = a0 + sides, b1 = a1 + sides;
                    AddQuadAway(v, t, a0, a1, b0, b1, (n[a0] + n[a1] + n[b0] + n[b1]) * 0.25f);
                }
            }
            if (caps)
            {
                AddFan(v, n, uv, t, profile[0], Vector3.down, sides);
                AddFan(v, n, uv, t, profile[profile.Length - 1], Vector3.up, sides);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 叉积自判绕序：本轮无法实测渲染，用几何自检杜绝"端盖看不见"
        private static void AddQuadAway(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) >= 0f) t.AddRange(new[] { a, b, c, b, d, c });
            else t.AddRange(new[] { a, c, b, b, c, d });
        }

        private static void AddFan(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
            Vector2 ring0, Vector3 faceNormal, int sides)
        {
            int c = v.Count, ring = c + 1;
            v.Add(new Vector3(0f, ring0.y, 0f)); n.Add(faceNormal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                float ang = Mathf.PI * 2f * i / sides, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                v.Add(new Vector3(cs * ring0.x, ring0.y, sn * ring0.x)); n.Add(faceNormal);
                uv.Add(new Vector2(0.5f + 0.5f * cs, 0.5f + 0.5f * sn));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = ring + i, b = ring + (i + 1) % sides;
                if (Vector3.Dot(Vector3.Cross(v[a] - v[c], v[b] - v[c]), faceNormal) >= 0f) t.AddRange(new[] { c, a, b });
                else t.AddRange(new[] { c, b, a });
            }
        }

        /// <summary>替换 sharedMesh：不碰 transform、不碰 m_StaticEditorFlags（在 GameObject 上）、不碰 materials。</summary>
        private static void AssignMesh(GameObject go, Mesh mesh)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) { Debug.LogWarning("[WarmHall] MeshFilter missing on " + go.name); return; }
            // guard 必须保留：内置 primitive(fileID 10206) 是 persistent，删它会废掉全工程 13 个圆柱
            if (filter.sharedMesh != null && !EditorUtility.IsPersistent(filter.sharedMesh))
                Object.DestroyImmediate(filter.sharedMesh);
            filter.sharedMesh = mesh;
        }

        private static void ApplyLathe(Transform tf, string meshName, Vector2[] profile, int sides, bool caps)
        {
            AssignMesh(tf.gameObject, LatheMesh(meshName, profile, sides, caps));
            // 尺寸烘进顶点 → scale 必须回 1；(1.55,0.04,1.55) 的逆转置会把 45° 倒角法线扭到 88.5°
            tf.localScale = Vector3.one;
        }

        [MenuItem("ShotField/Warm Hall/Apply dais chamfer")]
        private static void ApplyDaisChamfer()
        {
            var env = FindHallEnvironment();
            if (env == null) return;
            var baseGo = env.Find("Dais_Base");
            var topGo = env.Find("Dais_Top");
            var lineGo = env.Find("Dais_GoldLine");

            // r=0.775 / 高 0.08，按 primitive 中心建 → 局部 y ∈ [-0.04,+0.04]，倒角 5mm×5mm
            if (baseGo != null)
                ApplyLathe(baseGo, "Dais_Base_Chamfer", new[]
                {
                    new Vector2(0.775f, -0.040f), new Vector2(0.775f, 0.035f), new Vector2(0.770f, 0.040f)
                }, 48, true);   // 2 段*48*2 + 2*48 = 288

            // r=0.74 / 高 0.025，底缘埋在 Dais_Base 顶面下 2.5mm → 只倒上缘 3mm
            if (topGo != null)
                ApplyLathe(topGo, "Dais_Top_Chamfer", new[]
                {
                    new Vector2(0.740f, -0.0125f), new Vector2(0.740f, 0.0095f), new Vector2(0.737f, 0.0125f)
                }, 48, true);   // 2 段*48*2 + 2*48 = 288

            // 金口贴在 Dais_Base 顶面那圈 35mm 台阶上（y=0.055，倒角后平段 r=0.740..0.770）
            // 两侧各留 1mm：inner 0.741 / outer 0.769 = 28mm 宽；凸 1mm(0.056) / 埋 1mm(0.054)
            if (lineGo != null)
                ApplyLathe(lineGo, "Dais_GoldLine_Trim", new[]
                {
                    new Vector2(0.769f, 0.0540f), new Vector2(0.768f, 0.0560f),
                    new Vector2(0.742f, 0.0560f), new Vector2(0.741f, 0.0540f)
                }, 48, false);   // 环形带不加盖：3 段*48*2 = 288

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            EditorSceneManager.SaveScene(env.gameObject.scene);
            LogDaisStats(baseGo, topGo, lineGo);
        }

        /// <summary>回到未倒角原状（含 2× 错尺寸的 1.500/1.450 原环）：Base/Top 换回内置 Cylinder + 原 scale；GoldLine 走原 Ring() 代码路径。</summary>
        [MenuItem("ShotField/Warm Hall/Refresh dais (original)")]
        private static void RefreshDais()
        {
            var env = FindHallEnvironment();
            if (env == null) return;
            var baseGo = env.Find("Dais_Base");
            var topGo = env.Find("Dais_Top");
            var lineGo = env.Find("Dais_GoldLine");
            var gold = lineGo != null ? lineGo.GetComponent<Renderer>().sharedMaterial : null;

            RestoreCylinder(baseGo, new Vector3(1.55f, 0.04f, 1.55f));
            RestoreCylinder(topGo, new Vector3(1.48f, 0.0125f, 1.48f));
            // 原 Ring(): 96 段 / 1.500-1.450 / y=0.080，与原始逐位一致（复用 geometry 文件里的 Ring）
            if (lineGo != null && gold != null) Ring(env, "Dais_GoldLine", 1.500f, 1.450f, 0.080f, gold);

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            EditorSceneManager.SaveScene(env.gameObject.scene);
            Debug.Log("[WarmHall] REFRESH-DAIS=ORIGINAL | builtinCylinder scale(1.55,0.04,1.55)/(1.48,0.0125,1.48) | " +
                      "GoldLine 恢复为 2x 错尺寸原环 Ring96(1.500/1.450/y0.080) —— 这不是修正尺寸，是施工前原状");
        }

        private static void RestoreCylinder(Transform tf, Vector3 scale)
        {
            if (tf == null) return;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // 唯一可靠取到内置 Cylinder 的方式
            AssignMesh(tf.gameObject, temp.GetComponent<MeshFilter>().sharedMesh);   // guard 会跳过 Destroy
            tf.localScale = scale;
            Object.DestroyImmediate(temp);
        }

        private static Transform FindHallEnvironment()
        {
            var hall = GameObject.Find(RootName);
            var env = hall != null ? hall.transform.Find("HallEnvironment") : null;
            if (env == null) Debug.LogWarning("[WarmHall] HallEnvironment not found.");
            return env;
        }

        private static void LogDaisStats(Transform a, Transform b, Transform c)
        {
            var sb = new System.Text.StringBuilder("[WarmHall] DAIS-CHAMFER");
            foreach (var tf in new[] { a, b, c })
            {
                if (tf == null) { sb.Append(" | null"); continue; }
                var m = tf.GetComponent<MeshFilter>().sharedMesh;
                sb.Append(string.Format(" | {0} tri={1} vtx={2} sub={3} scale={4} pos={5} flags={6}",
                    tf.name, m.triangles.Length / 3, m.vertexCount, m.subMeshCount,
                    tf.localScale, tf.localPosition, GameObjectUtility.GetStaticEditorFlags(tf.gameObject)));
            }
            Debug.Log(sb.ToString());
        }
    }
}
#endif
