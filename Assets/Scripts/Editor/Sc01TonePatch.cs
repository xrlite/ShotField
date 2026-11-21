#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B3c 顶面色调：吊顶原本是 #242428（线性反照率 0.018），在 0.26 的环境光下
    /// 反出来的线性亮度只有 0.005，无论怎么打光都是纯黑 —— 抬头就是一片黑洞。
    /// 这里把吊顶提到中性深灰、把桁架的金属度降下来，让它能被光"读"出来。
    /// </summary>
    internal static class Sc01TonePatch
    {
        private static readonly Color CeilingColor = new Color(0.460f, 0.460f, 0.480f);
        private static readonly Color TrussColor = new Color(0.255f, 0.265f, 0.275f);
        private static readonly Color BackdropColor = new Color(0.205f, 0.215f, 0.235f);
        private static readonly Color ClothColor = new Color(0.215f, 0.215f, 0.225f);
        private const float TrussMetallic = 0.45f;
        private const float TrussSmoothness = 0.30f;
        private const float MinLinearAlbedo = 0.15f;

        [MenuItem("ShotField/SC01/Patch B3c 顶面色调")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("Tone");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("TONE");
        }

        internal static bool Apply()
        {
            var ceiling = Sc01PatchCore.LoadMat("MAT_STU_CeilingDark_v05");
            Sc01PatchCore.Check(Tint(ceiling, CeilingColor, float.NaN, float.NaN), "B3c.1 吊顶提亮到中灰",
                ceiling == null ? "缺失" : ColorOf(ceiling).ToString());
            var linear = ColorOf(ceiling).linear.grayscale;
            Sc01PatchCore.Check(linear >= MinLinearAlbedo, "B3c.2 吊顶线性反照率够高（否则纯黑）",
                "linear=" + linear.ToString("F3") + " 下限=" + MinLinearAlbedo);

            var truss = Sc01PatchCore.LoadMat("MAT_STU_Truss_v05");
            Sc01PatchCore.Check(Tint(truss, TrussColor, TrussMetallic, TrussSmoothness), "B3c.3 桁架降金属度",
                truss == null ? "缺失" : "metallic=" + truss.GetFloat("_Metallic").ToString("F2"));

            // 城市背景板与黑布原本接近纯黑（#141A1B / #262628），在任何环境光下都反成 0，
            // 抬头或侧望就是一片纯黑。提到深灰后仍属"暗部"，但能被读出体积。
            var backdrop = Sc01PatchCore.LoadMat("MAT_STU_City_v05");
            Sc01PatchCore.Check(Tint(backdrop, BackdropColor, float.NaN, float.NaN), "B3c.4 城市背景板提亮",
                backdrop == null ? "缺失" : ColorOf(backdrop).ToString());

            var cloth = Sc01PatchCore.LoadMat("MAT_STU_BlackCloth_v05");
            Sc01PatchCore.Check(Tint(cloth, ClothColor, float.NaN, float.NaN), "B3c.5 黑布提亮",
                cloth == null ? "缺失" : ColorOf(cloth).ToString());
            return Sc01PatchCore.AllPassed;
        }

        private static bool Tint(Material mat, Color color, float metallic, float smoothness)
        {
            if (mat == null) return false;
            mat.SetColor("_BaseColor", color);
            if (!float.IsNaN(metallic)) mat.SetFloat("_Metallic", metallic);
            if (!float.IsNaN(smoothness)) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return Sc01PatchCore.Near(ColorOf(mat), color);
        }

        private static Color ColorOf(Material mat) => mat.GetColor("_BaseColor");
    }
}
#endif
