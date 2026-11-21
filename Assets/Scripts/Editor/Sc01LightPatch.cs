#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B3b 灯光：立起"专业影棚"的三点光。
    /// 关键约束：主光必须低于桁架底（4.02m），否则一开就是"被挡死的灯"；
    /// 顶板之上的两盏假灯必须关掉，否则穿顶打脏光斑。
    /// </summary>
    internal static class Sc01LightPatch
    {
        private const string BounceName = "LGT_STU_Bounce";

        private struct Spec
        {
            public string Name;
            public int Enabled;
            public LightType Type;
            public float Intensity, Range, Spot, Inner;
            public LightShadows Shadows;
            public Vector3 Position, Aim;
            public Color Color;
        }

        /// <summary>参数顺序：名 / 启用(-1不改) / 强度 / 距离 / 锥角 / 内锥 / 阴影(-1不改) / 位置 / 朝向(0=不改) / 颜色(黑=不改)。NaN 表示不改。</summary>
        private static readonly Spec[] Specs =
        {
            S("LGT_STU_Key", 1, 26f, 16f, 100f, 62f, 2, new Vector3(0.85f, 3.78f, 1.05f), new Vector3(0.20f, 0f, 2.45f), new Color(1f, 0.96f, 0.90f), LightType.Spot),
            S("LGT_STU_Rim", 1, 14f, 12f, 38f, 22f, 2, new Vector3(1f, 3.50f, 6.55f), new Vector3(-0.73f, 1.56f, 4.00f), new Color(0.78f, 0.86f, 1f), LightType.Spot),
            S("TableWarmLight", -1, 9f, float.NaN, 78f, float.NaN, -1, Vector3.zero, Vector3.zero, Color.black),
            S("FillLight_A", -1, 7f, 11f, 96f, float.NaN, 0, new Vector3(-4.30f, 2.20f, 1.60f), new Vector3(0.50f, 0.90f, 2.60f), new Color(0.80f, 0.87f, 1f)),
            S("FillLight_B", -1, 2.6f, float.NaN, 85f, float.NaN, -1, Vector3.zero, Vector3.zero, Color.black),
            S("WindowMoonlight", -1, 0.55f, float.NaN, float.NaN, float.NaN, 0, Vector3.zero, Vector3.zero, Color.black),
            S("LGT_STU_Corridor", 0, float.NaN, float.NaN, float.NaN, float.NaN, -1, Vector3.zero, Vector3.zero, Color.black),
            S("LGT_STU_PanelDown", 0, float.NaN, float.NaN, float.NaN, float.NaN, -1, Vector3.zero, Vector3.zero, Color.black)
        };

        [MenuItem("ShotField/SC01/Patch B3b 三点光")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("Light");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("LIGHT");
        }

        internal static bool Apply()
        {
            foreach (var spec in Specs) ApplyOne(spec);
            EnsureBounce();
            Sc01PatchCore.Check(CountShadowLights() == 3, "B3.7 生效投影灯恰为 3 盏",
                "实际=" + CountShadowLights());
            var key = Sc01PatchCore.FindDeep("LGT_STU_Key");
            Sc01PatchCore.Check(key != null && key.position.y < 3.96f, "B3.8 主光低于桁架底",
                key == null ? "缺失" : key.position.y.ToString("F2"));
            return Sc01PatchCore.AllPassed;
        }

        /// <summary>
        /// 本工程的环境光探针在离屏渲染时不生效（实测 Flat 0.5 下顶面仍为 8/255），
        /// 因此"没有直射光的面必然是纯黑"。补一盏朝上的宽角灯，等效于地面反弹，
        /// 把吊顶与场景上缘从"黑洞"拉回可读的暗灰。不投影，避免在主光造型上引入第二个影子。
        /// </summary>
        private static void EnsureBounce()
        {
            var t = Sc01PatchCore.FindDeep(BounceName);
            if (t == null)
            {
                var go = new GameObject(BounceName);
                go.transform.SetParent(null);
                t = go.transform;
                t.gameObject.AddComponent<Light>();
            }
            t.position = new Vector3(0f, 1.05f, 2.00f);
            t.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var light = t.GetComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.95f, 0.93f, 0.90f);
            light.intensity = 26f;
            light.range = 10f;
            light.spotAngle = 125f;
            light.innerSpotAngle = 80f;
            light.shadows = LightShadows.None;
            light.enabled = true;
            Sc01PatchCore.Check(Vector3.Dot(t.forward, Vector3.up) > 0.99f,
                "B3.9 反弹补光朝上", "forward=" + t.forward + " int=" + light.intensity);
        }

        private static void ApplyOne(Spec spec)
        {
            var t = Sc01PatchCore.FindDeep(spec.Name);
            var light = t == null ? null : t.GetComponent<Light>();
            if (light == null) { Sc01PatchCore.Check(false, "B3 灯 " + spec.Name, "缺失或无 Light"); return; }

            if (spec.Enabled >= 0) light.enabled = spec.Enabled == 1;
            if ((int)spec.Type >= 0) light.type = spec.Type;
            if (!float.IsNaN(spec.Intensity)) light.intensity = spec.Intensity;
            if (!float.IsNaN(spec.Range)) light.range = spec.Range;
            if (!float.IsNaN(spec.Spot)) light.spotAngle = spec.Spot;
            if (!float.IsNaN(spec.Inner)) light.innerSpotAngle = spec.Inner;
            if ((int)spec.Shadows >= 0) light.shadows = spec.Shadows;
            if (spec.Color != Color.black) light.color = spec.Color;

            var aimed = true;
            if (spec.Position != Vector3.zero) t.position = spec.Position;
            if (spec.Aim != Vector3.zero)
            {
                var dir = spec.Aim - t.position;
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                aimed = Vector3.Dot(t.forward, dir.normalized) > 0.99f;
            }

            var ok = aimed && (spec.Enabled != 1 || light.enabled) &&
                     (float.IsNaN(spec.Intensity) || Mathf.Abs(light.intensity - spec.Intensity) < 0.01f);
            Sc01PatchCore.Check(ok, "B3 灯 " + spec.Name,
                "type=" + light.type + " int=" + light.intensity.ToString("F2") +
                " range=" + light.range.ToString("F2") + " spot=" + light.spotAngle.ToString("F1") +
                " shadow=" + light.shadows + " pos=" + t.position);
        }

        private static int CountShadowLights()
        {
            var count = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.isActiveAndEnabled && light.shadows != LightShadows.None && light.type != LightType.Directional)
                    count++;
            return count;
        }

        private static Spec S(string name, int enabled, float intensity, float range, float spot,
            float inner, int shadow, Vector3 position, Vector3 aim, Color color,
            LightType type = (LightType)(-1)) => new Spec
        {
            Name = name, Enabled = enabled, Intensity = intensity, Range = range, Spot = spot,
            Inner = inner, Shadows = (LightShadows)shadow, Position = position, Aim = aim,
            Color = color, Type = type
        };
    }
}
#endif
