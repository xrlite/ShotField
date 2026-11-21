// 镜场 ShotField · 预演色板唯一真相来源
// 目的：六色演员的颜色只在这里定义一次。构建脚本、验证器、门禁都从这里取值，
// 避免出现「验证器拿自己写的常量去证明自己」的自证断言。
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>六个预演演员的色码。改色只改这里，并同步 Assets/Models/Characters/MAT_Actor_*.mat。</summary>
    public static class PrevisPalette
    {
        /// <summary>色相之间必须拉开的最小角度（度）。低于此值在 1080p 缩略图里会糊成同一色。</summary>
        /// <remarks>
        /// 40° 这条阈值现在是**实测支撑**的：本色板最窄的一对是 品红(318°)-红(0°) = 42°，
        /// 次窄 黄(48°)-红(0°) = 48°、蓝(220°)-紫(268°) = 48°。
        /// 本文件初版曾把这条判据读出成 0.1，原因是 Unity 的 Color.RGBToHSV 返回 0-1 而不是 0-360，
        /// 当时我据此把阈值下调到 30°，那是拿自己的单位错误当成本色板的缺陷——已改回 40°。
        /// 保留 RGB 距离这条，是因为色相本身分不开「同色相不同明度」的情况。
        /// </remarks>
        public const float MinHueSeparationDegrees = 40f;

        /// <summary>两两颜色在归一化 RGB 空间的最小距离，补色相判据的不足。</summary>
        public const float MinRgbDistance = 0.35f;

        private static readonly string[] Order = { "Red", "Blue", "Yellow", "Green", "Violet", "Magenta" };

        private static readonly Dictionary<string, string> HexByKey = new Dictionary<string, string>
        {
            { "Red", "#FF0000" },
            { "Blue", "#0055FF" },
            { "Yellow", "#FFCC00" },
            { "Green", "#00CC44" },
            { "Violet", "#8A3BE2" },
            { "Magenta", "#E23BB0" },
        };

        /// <summary>演员键名，顺序即 A-F。</summary>
        public static IReadOnlyList<string> Keys => Order;

        /// <summary>角色槽位（0-5）对应的键名。</summary>
        public static string KeyAt(int index)
        {
            if (index < 0 || index >= Order.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index), "预演色板只有 6 个槽位。");
            }
            return Order[index];
        }

        /// <summary>键名对应的十六进制，未知键返回 null。</summary>
        public static string HexOf(string key)
        {
            return HexByKey.TryGetValue(key, out var hex) ? hex : null;
        }

        /// <summary>键名对应的颜色。键名写错时直接抛错，不静默回落到白色。</summary>
        public static Color ColorOf(string key)
        {
            var hex = HexOf(key);
            if (hex == null)
            {
                throw new System.Collections.Generic.KeyNotFoundException("预演色板没有键：" + key);
            }
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                throw new System.InvalidOperationException("色板十六进制无法解析：" + hex);
            }
            return color;
        }

        /// <summary>槽位对应的材质资产路径，用于构建脚本按名取材质。</summary>
        public static string MaterialPathAt(int index)
        {
            return "Assets/Models/Characters/MAT_Actor_" + KeyAt(index) + ".mat";
        }

        /// <summary>槽位对应的 prefab 路径。</summary>
        public static string PrefabPathAt(int index)
        {
            return "Assets/Prefabs/SC01/Actor" + System.Convert.ToChar('A' + index) + "_" + KeyAt(index) + ".prefab";
        }

        /// <summary>导出层的中性灰：墙面。与演员色分开定义，免得灰模层污染色板。</summary>
        public static Color StageWallGray => ColorUtility.TryParseHtmlString("#D8D8D8", out var c) ? c : Color.white;

        /// <summary>导出层的中性灰：地面。</summary>
        public static Color StageFloorGray => ColorUtility.TryParseHtmlString("#9A9A9A", out var c) ? c : Color.white;

        /// <summary>
        /// 两两色相角差的最小值，单位「度」。
        /// Unity 的 Color.RGBToHSV 返回的 h 是 0-1 而不是 0-360，必须乘 360，
        /// 否则整函数算出 0.x，被任何阈值判成不合格（本文件初版就踩了这条）。
        /// </summary>
        public static float NarrowestHueSeparation()
        {
            var hues = new float[Order.Length];
            for (var i = 0; i < Order.Length; i++)
            {
                Color.RGBToHSV(ColorOf(Order[i]), out var h, out _, out _);
                hues[i] = h * 360f;
            }

            var narrowest = 360f;
            for (var i = 0; i < hues.Length; i++)
            {
                for (var j = i + 1; j < hues.Length; j++)
                {
                    var delta = Mathf.Abs(hues[i] - hues[j]);
                    if (delta > 180f)
                    {
                        delta = 360f - delta;
                    }
                    if (delta < narrowest)
                    {
                        narrowest = delta;
                    }
                }
            }
            return narrowest >= 360f ? 0f : narrowest;
        }

        /// <summary>
        /// 两两颜色在归一化 RGB 空间的最小欧氏距离。
        /// 单看色相会把红/品红这类同色系判成不可分，所以再加这条。
        /// </summary>
        public static float NarrowestRgbDistance()
        {
            var narrowest = float.MaxValue;
            for (var i = 0; i < Order.Length; i++)
            {
                for (var j = i + 1; j < Order.Length; j++)
                {
                    var a = ColorOf(Order[i]);
                    var b = ColorOf(Order[j]);
                    var d = Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));
                    if (d < narrowest)
                    {
                        narrowest = d;
                    }
                }
            }
            return narrowest;
        }
    }
}
