using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把三层 PICO 图标合成 Android 启动器图标：输出 1024 合成图 + 5 个参考密度图。
/// 全部 CPU 计算（alpha-over 合层 + alpha 加权 box 缩放），不依赖 GPU，可在 -nographics 下执行。
/// </summary>
public static partial class ShotFieldLauncherIconBuilder
{
    const string BgPath = "Assets/Art/LauncherIcon/icon_bg.png";
    const string Fg2Path = "Assets/Art/LauncherIcon/icon_fg2.png";
    const string Fg3Path = "Assets/Art/LauncherIcon/icon_fg3.png";
    const string CompositePath = "Assets/Art/LauncherIcon/icon_composite_1024.png";
    const string ResRoot = "F:/pico/06_素材/Logo_v02_提案/android_mipmap_20261125";

    static readonly string[] DensityDirs =
        { "mipmap-mdpi", "mipmap-hdpi", "mipmap-xhdpi", "mipmap-xxhdpi", "mipmap-xxxhdpi" };
    static readonly int[] DensitySizes = { 48, 72, 96, 144, 192 };

    const int Canvas = 1024;

    [MenuItem("ShotField/生成 Android 启动器图标")]
    public static void Build()
    {
        var bg = ReadPng(BgPath, Canvas);
        if (bg == null) throw new Exception("[IconBuilder] 读不到底板层 " + BgPath);

        var fg2 = ReadPng(Fg2Path, Canvas);
        var fg3 = ReadPng(Fg3Path, Canvas);

        var canvas = new Color32[Canvas * Canvas];
        for (int i = 0; i < canvas.Length; i++) canvas[i] = new Color32(0, 0, 0, 0);

        Over(canvas, bg);
        if (fg2 != null) Over(canvas, fg2);
        if (fg3 != null) Over(canvas, fg3);

        WriteTexture(canvas, Canvas, CompositePath);

        for (int d = 0; d < DensityDirs.Length; d++)
        {
            var small = Downscale(canvas, Canvas, DensitySizes[d]);
            WriteTexture(small, DensitySizes[d], ResRoot + "/" + DensityDirs[d] + "/ic_launcher.png");
        }

        ValidateBackground(bg);
        ValidateForeground(fg2, fg3);
        ApplyPlayerSettingsIcon(canvas);
        AssetDatabase.Refresh();
        Debug.Log("[IconBuilder] 完成：1024 合成图 + " + DensityDirs.Length + " 个密度。");
    }

    static Color32[] ReadPng(string path, int expected)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("[IconBuilder] 缺文件 " + path);
            return null;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(path)))
        {
            Debug.LogError("[IconBuilder] PNG 解码失败 " + path);
            UnityEngine.Object.DestroyImmediate(tex);
            return null;
        }
        if (tex.width != expected || tex.height != expected)
            Debug.LogWarning("[IconBuilder] 尺寸 " + tex.width + "x" + tex.height + "，期望 " + expected);
        var px = tex.GetPixels32();
        UnityEngine.Object.DestroyImmediate(tex);
        return px;
    }

    static void Over(Color32[] dst, Color32[] src)
    {
        for (int i = 0; i < dst.Length && i < src.Length; i++)
        {
            float sa = src[i].a / 255f;
            if (sa <= 0.0001f) continue;
            float da = dst[i].a / 255f;
            float oa = sa + da * (1f - sa);
            float inv = oa <= 0.0001f ? 0f : 1f / oa;
            dst[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((src[i].r * sa + dst[i].r * da * (1f - sa)) * inv), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src[i].g * sa + dst[i].g * da * (1f - sa)) * inv), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((src[i].b * sa + dst[i].b * da * (1f - sa)) * inv), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255f), 0, 255));
        }
    }

    static Color32[] Downscale(Color32[] src, int srcSize, int dstSize)
    {
        var dst = new Color32[dstSize * dstSize];
        float scale = (float)srcSize / dstSize;

        for (int y = 0; y < dstSize; y++)
        {
            for (int x = 0; x < dstSize; x++)
            {
                int x0 = Mathf.FloorToInt(x * scale);
                int x1 = Mathf.Min(srcSize, Mathf.Max(x0 + 1, Mathf.CeilToInt((x + 1) * scale)));
                int y0 = Mathf.FloorToInt(y * scale);
                int y1 = Mathf.Min(srcSize, Mathf.Max(y0 + 1, Mathf.CeilToInt((y + 1) * scale)));

                float r = 0f, g = 0f, b = 0f, a = 0f;
                int n = 0;
                for (int yy = y0; yy < y1; yy++)
                {
                    for (int xx = x0; xx < x1; xx++)
                    {
                        var c = src[yy * srcSize + xx];
                        float ca = c.a / 255f;
                        r += c.r * ca;
                        g += c.g * ca;
                        b += c.b * ca;
                        a += ca;
                        n++;
                    }
                }

                if (a <= 0.0001f)
                {
                    dst[y * dstSize + x] = new Color32(0, 0, 0, 0);
                    continue;
                }
                float inv = 1f / a;
                dst[y * dstSize + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(r * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(g * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(a / n * 255f), 0, 255));
            }
        }
        return dst;
    }

    static void WriteTexture(Color32[] px, int size, string path)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("[IconBuilder] 写出 " + path + " (" + size + "px)");
    }
}
