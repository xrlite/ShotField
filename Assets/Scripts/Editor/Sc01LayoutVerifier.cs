#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M1 布局验收：坐标是否回到制作包 §2 基准、道具是否悬空、两两 AABB 是否重叠。
    /// 只读场景，不改任何资产；断言失败会打印每一项的具体数值，便于按数据修正。
    /// </summary>
    public static class Sc01LayoutVerifier
    {
        private const int ExpectedAssertions = 16;
        private const float FloorY = 0f;
        private const float ContactTolerance = 0.012f;
        // 高度断言的边界容差：FitExact 压到 0.45 时会留下 0.4500001 这类浮点尾巴，
        // 不加容差会偶发把达标尺寸判成 FAIL。
        private const float SizeTolerance = 0.005f;

        // 参与重叠判定的具名道具（排除房间壳体与灯）。
        // 注意：Sc01ArtSceneBuilder.Place() 把实例改名为 sceneName，
        // 所以这里必须用场景名（ShoeCabinet/Sofa/CoffeeTable…），不是 prefab 资产名。
        private static readonly string[] PropNames =
        {
            "ShoeCabinet", "Sofa", "CoffeeTable", "Chair",
            "Plant", "ActorA", "Envelope",
            "Fridge", "Tea", "Slipper"
        };

        [MenuItem("ShotField/Verifiers/Run SC01 Layout Verifier")]
        [MenuItem("镜场 ShotField/验证/验证 SC01 布局与重叠")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition) { passCount++; Debug.Log($"[Sc01LayoutVerifier] PASS: {name}"); }
                else { Debug.LogError($"[Sc01LayoutVerifier] FAIL: {name}"); throw new Exception("Assertion failed: " + name); }
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Sc01Blockout.BuildIntoScene();
            Assert("SC01 白模可无 GUI 构建", root != null);

            var bounds = new Dictionary<string, Bounds>();
            for (int i = 0; i < PropNames.Length; i++)
            {
                var b = Collect(root.transform, PropNames[i]);
                if (b.HasValue) bounds[PropNames[i]] = b.Value;
            }
            Report(bounds);
            Assert($"已采集到全部 {PropNames.Length} 件道具的包围盒（实际 {bounds.Count}）",
                bounds.Count == PropNames.Length);

            // 坐标基准：Blender 场景换算口径（UnityX = BlenderX - 3）
            var table = bounds.ContainsKey("CoffeeTable") ? bounds["CoffeeTable"] : new Bounds();
            var envelope = bounds.ContainsKey("Envelope") ? bounds["Envelope"] : new Bounds();
            var tea = bounds.ContainsKey("Tea") ? bounds["Tea"] : new Bounds();

            Assert("茶几 X 对齐 -1.6（Blender 口径）", Mathf.Abs(table.center.x + 1.6f) < 0.10f);
            Assert("茶几 Z 对齐 2.2（Blender 口径）", Mathf.Abs(table.center.z - 2.2f) < 0.10f);
            Assert("信封水平位置在茶几上（Blender -1.5/2.15）",
                Mathf.Abs(envelope.center.x + 1.5f) < 0.14f && Mathf.Abs(envelope.center.z - 2.15f) < 0.14f);

            // 桌面高度以茶几实测顶面为准（写实资产当前只有 0.19m 高，不是制作包的 0.45）
            float tableTop = table.max.y;
            Assert($"信封底面贴茶几实际顶面 {tableTop:F3}（±1.2cm）",
                Mathf.Abs(envelope.min.y - tableTop) < ContactTolerance);

            var envT = FindDescendant(root.transform, "Envelope");
            Vector3 envNormal = envT != null ? envT.rotation * Vector3.forward : Vector3.up;
            Vector3 envLong = envT != null ? envT.rotation * Vector3.right : Vector3.right;
            Assert("信封平放（关键面法线朝 +Y，不立着）",
                Vector3.Dot(envNormal, Vector3.up) > 0.95f);
            Assert("信封长边指向 -X（制作包「正面朝 -X」的实现口径）",
                Vector3.Dot(envLong, new Vector3(-1f, 0f, 0f)) > 0.95f);

            Assert($"茶杯底面贴茶几实际顶面 {tableTop:F3}（±1.2cm）",
                Mathf.Abs(tea.min.y - tableTop) < ContactTolerance);

            var actor = bounds.ContainsKey("ActorA") ? bounds["ActorA"] : new Bounds();
            var cabinet = bounds.ContainsKey("ShoeCabinet") ? bounds["ShoeCabinet"] : new Bounds();
            var plant = bounds.ContainsKey("Plant") ? bounds["Plant"] : new Bounds();
            Assert($"coffee table height is 0.42-0.45m (actual {table.size.y:F3})",
                table.size.y >= 0.42f - SizeTolerance && table.size.y <= 0.45f + SizeTolerance);
            Assert($"cabinet height is 0.80-0.90m (actual {cabinet.size.y:F3})",
                cabinet.size.y >= 0.80f - SizeTolerance && cabinet.size.y <= 0.90f + SizeTolerance);
            Assert($"plant pot rests on floor (minY {plant.min.y:F3})", Mathf.Abs(plant.min.y - FloorY) < ContactTolerance);
            Assert($"角色身高接近 1.72m（实际 {actor.size.y:F2}）",
                Mathf.Abs(actor.size.y - 1.72f) < 0.08f);

            // 重叠判定
            var overlaps = new List<string>();
            for (int a = 0; a < PropNames.Length; a++)
            {
                for (int b2 = a + 1; b2 < PropNames.Length; b2++)
                {
                    var ka = PropNames[a];
                    var kb = PropNames[b2];
                    if (!bounds.ContainsKey(ka) || !bounds.ContainsKey(kb)) continue;
                    if (!bounds[ka].Intersects(bounds[kb])) continue;
                    var inter = Intersection(bounds[ka], bounds[kb]);
                    // 贴放（如杯在桌面上）允许接触，只有三轴都超出容差才算真重叠
                    bool realOverlap = inter.size.y > ContactTolerance &&
                                       inter.size.x > ContactTolerance &&
                                       inter.size.z > ContactTolerance;
                    if (realOverlap) overlaps.Add($"{ka}×{kb} d={inter.size}");
                }
            }
            if (overlaps.Count > 0) Debug.LogError("[Sc01LayoutVerifier] 重叠项: " + string.Join(" | ", overlaps));
            Assert("道具两两三轴重叠数为 0", overlaps.Count == 0);

            // 悬空与越界（信封、茶杯按设计就应在桌面上，不参与"落地"判定）
            var onSurface = new HashSet<string> { "Envelope", "Tea" };
            int floating = 0;
            for (int i = 0; i < PropNames.Length; i++)
            {
                var k = PropNames[i];
                if (!bounds.ContainsKey(k) || onSurface.Contains(k)) continue;
                if (bounds[k].min.y > FloorY + 0.06f && bounds[k].min.y < 3.0f) floating++;
            }
            Assert("应落地的道具无悬空（底面高于地面 6cm 以上）", floating == 0);

            bool inRoom = true;
            for (int i = 0; i < PropNames.Length; i++)
            {
                var k = PropNames[i];
                if (!bounds.ContainsKey(k)) continue;
                var bb = bounds[k];
                if (bb.min.x < -3.1f || bb.max.x > 3.1f || bb.min.z < -0.1f || bb.max.z > 4.1f) inRoom = false;
            }
            Assert("全部道具在 6×4m 房间范围内", inRoom);

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"SC01-LAYOUT-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"SC01-LAYOUT-RESULT=PASS ({passCount}/{ExpectedAssertions})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static Bounds? Collect(Transform root, string name)
        {
            var t = FindDescendant(root, name);
            if (t == null) return null;
            var col = name == "ActorA" ? t.GetComponent<Collider>() : null;
            if (col != null) return col.bounds;
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var deeper = FindDescendant(c, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static Bounds Intersection(Bounds a, Bounds b)
        {
            var min = Vector3.Max(a.min, b.min);
            var max = Vector3.Min(a.max, b.max);
            var c = new Bounds();
            c.SetMinMax(min, max);
            return c;
        }

        private static void Report(Dictionary<string, Bounds> bounds)
        {
            foreach (var kv in bounds)
            {
                var b = kv.Value;
                Debug.Log($"[Sc01LayoutVerifier] DATA {kv.Key}: center={b.center} size={b.size} min.y={b.min.y:F3}");
            }
        }
    }
}
#endif
