// 镜场 ShotField · 预演色板唯一真相来源
// 目的：六色演员、道具、布光与空间界面的颜色只在这里定义一次。构建脚本、验证器、门禁都从这里取值。
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>预演色板。改色只改这里，并同步 Assets/Models/Characters/MAT_Actor_*.mat。</summary>
    public static class PrevisPalette
    {
        public const float MinHueSeparationDegrees = 40f;
        public const float MinRgbDistance = 0.35f;

        private static readonly string[] Order = { "Red", "Blue", "Yellow", "Green", "Violet", "Magenta" };

        private static readonly Dictionary<string, string> HexByKey = new Dictionary<string, string>
        {
            // 六色演员
            { "Red", "#FF0000" },
            { "Blue", "#0055FF" },
            { "Yellow", "#FFCC00" },
            { "Green", "#00CC44" },
            { "Violet", "#8A3BE2" },
            { "Magenta", "#E23BB0" },
            // 关键道具
            { "Envelope", "#F5F5F0" },
            { "Seal", "#D92525" },
            // 两级中性灰模
            { "StageWallGray", "#D8D8D8" },
            { "StageFloorGray", "#9A9A9A" },
            // 预演导向层
            { "AmberTrajectory", "#F2A33C" },
            { "SlateBlueGuideline", "#4A90E2" },
            // 空间布光矩阵
            { "AmbientSky", "#B8B3A8" },
            { "AmbientEquator", "#8F877A" },
            { "AmbientGround", "#423D36" },
            { "WindowMoonlight", "#AED1FF" },
            { "TableWarmLight", "#FFB866" },
            { "FilmBounce", "#FFE0C2" },
            // 空间界面与卡片色
            { "PicoBg", "#1A1C21" },
            { "PicoCard", "#262930" },
            { "PicoAccent", "#2B66FF" },
            { "PicoButton", "#3D424D" },
            // 顶面与背景灰调
            { "CeilingGray", "#75757A" },
            { "TrussGray", "#414446" },
            { "BackdropDark", "#34373C" },
            { "ClothDark", "#373739" }
        };

        public static IReadOnlyList<string> Keys => Order;

        public static string KeyAt(int index) => (index >= 0 && index < Order.Length)
            ? Order[index] : throw new System.ArgumentOutOfRangeException(nameof(index), "预演色板只有 6 个槽位。");

        public static string HexOf(string key) => HexByKey.TryGetValue(key, out var hex) ? hex : null;

        public static Color ColorOf(string key)
        {
            if (key == "AmberTrajectory") return AmberTrajectory;
            var hex = HexOf(key) ?? throw new KeyNotFoundException("预演色板没有键：" + key);
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                throw new System.InvalidOperationException("色板十六进制无法解析：" + hex);
            return color;
        }

        public static string MaterialPathAt(int index) =>
            "Assets/Models/Characters/MAT_Actor_" + KeyAt(index) + ".mat";

        public static string PrefabPathAt(int index) =>
            "Assets/Prefabs/SC01/Actor" + System.Convert.ToChar('A' + index) + "_" + KeyAt(index) + ".prefab";

        public static Color StageWallGray => ColorOf("StageWallGray");
        public static Color StageFloorGray => ColorOf("StageFloorGray");
        public static Color Envelope => ColorOf("Envelope");
        public static Color Seal => ColorOf("Seal");
        public static Color AmberTrajectory => new Color(0.949f, 0.639f, 0.235f, 0.850f);
        public static Color SlateBlueGuideline => ColorOf("SlateBlueGuideline");
        public static Color AmbientSky => ColorOf("AmbientSky");
        public static Color AmbientEquator => ColorOf("AmbientEquator");
        public static Color AmbientGround => ColorOf("AmbientGround");
        public static Color PicoBg => new Color(0.10f, 0.11f, 0.13f, 0.92f);
        public static Color PicoCard => new Color(0.15f, 0.16f, 0.19f, 0.95f);
        public static Color PicoAccent => new Color(0.17f, 0.40f, 1.00f, 1.0f);
        public static Color PicoButton => new Color(0.24f, 0.26f, 0.30f, 0.95f);
        public static Color CeilingGray => new Color(0.460f, 0.460f, 0.480f);
        public static Color TrussGray => new Color(0.255f, 0.265f, 0.275f);
        public static Color BackdropDark => new Color(0.205f, 0.215f, 0.235f);
        public static Color ClothDark => new Color(0.215f, 0.215f, 0.225f);

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
            for (var j = i + 1; j < hues.Length; j++)
            {
                var delta = Mathf.Abs(hues[i] - hues[j]);
                if (delta > 180f) delta = 360f - delta;
                if (delta < narrowest) narrowest = delta;
            }
            return narrowest >= 360f ? 0f : narrowest;
        }

        public static float NarrowestRgbDistance()
        {
            var narrowest = float.MaxValue;
            for (var i = 0; i < Order.Length; i++)
            for (var j = i + 1; j < Order.Length; j++)
            {
                var a = ColorOf(Order[i]);
                var b = ColorOf(Order[j]);
                var d = Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b));
                if (d < narrowest) narrowest = d;
            }
            return narrowest;
        }
    }
}
