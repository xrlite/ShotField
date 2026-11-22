#if UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ShotField.Core;

namespace ShotField.EditorTools
{
    /// <summary>主场景（SC01_Main）结构审计（只读，绝不修改场景）。判据全部来自设计值。</summary>
    public static class Sc01FinalAudit
    {
        private static readonly string[] WallFaces =
            { "RightWall", "EntryWallLeft", "EntryWallRight", "EntryLintel",
              "BackWindowLeft", "BackWindowRight", "BackWindowHeader", "BackWindowSill" };

        private static readonly string[] Frames =
            { "WindowFrameTop", "WindowFrameBottom", "WindowFrameFront", "WindowFrameBack" };

        private static readonly string[] Edges =
            { "SETW_OpenCapTop", "SETW_OpenPostFront", "SETW_OpenPostBack" };

        [MenuItem("ShotField/Verifiers/Run SC01 Final Audit")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Geometry();
                Material();
                Grade();
                Light();
                Rig();
                Actor();
            }
            Sc01PatchCore.Finish("AUDIT");
        }

        private static void Geometry()
        {
            ExpectActive("LeftWall", false, "A1 左墙已拆");
            ExpectActive("LeftBaseboard", false, "A2 左踢脚线已拆");
            ExpectActive("Ceiling", false, "A3 天花板已拆");
            ExpectActive("STUDIO_Truss", true, "A4 桁架已启用");
            ExpectActive("WindowMarker", false, "A5 窗洞封板已关");
            var edge = HierarchySearch.FindDeep("STUDIO_OpenEdge");
            Sc01PatchCore.Check(edge != null && edge.childCount >= 3, "A6 开口收边框齐全",
                edge == null ? "缺失" : "子件=" + edge.childCount);
        }

        private static void Material()
        {
            foreach (var name in WallFaces) ExpectMat(name, "MAT_STU_Wall_v05", "A7 墙面 " + name);
            ExpectMat("Floor", "MAT_STU_Floor_v05", "A8 地面");
            ExpectMat("RightBaseboard", "MAT_STU_Wood_URP", "A9 右踢脚线");
            ExpectMat("BackBaseboard", "MAT_STU_Wood_URP", "A10 后踢脚线");
            ExpectMat("Door", "MAT_STU_Wood_URP", "A11 门叶");
            ExpectMat("SC01_FreeCameraHandle", "MAT_STU_Frame_v05", "A12 自由机位手柄");
            ExpectGroup(Frames, "MAT_STU_Frame_v05", "A13 窗框");
            ExpectGroup(Edges, "MAT_STU_Frame_v05", "A14 开口收边");
            ExpectFixtures();

            var white = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.enabled && r.gameObject.activeInHierarchy &&
                    (r.sharedMaterial == null || r.sharedMaterial.name == "Lit")) white++;
            Sc01PatchCore.Check(white == 0, "A16 生效渲染器无默认白模", "白模数=" + white);
        }

        private static void Grade()
        {
            Sc01PatchCore.Check(Mathf.Abs(RenderSettings.ambientIntensity - 1f) < 0.001f,
                "A17 环境光强度 1.0", RenderSettings.ambientIntensity.ToString("F3"));
            Sc01PatchCore.Check(RenderSettings.ambientMode == AmbientMode.Trilight, "A18 环境光 Trilight",
                RenderSettings.ambientMode.ToString());
            Sc01PatchCore.Check(Sc01PatchCore.Near(RenderSettings.ambientSkyColor,
                PrevisPalette.AmbientSky), "A19 天顶色", RenderSettings.ambientSkyColor.ToString());
            Sc01PatchCore.Check(Sc01PatchCore.Near(RenderSettings.ambientGroundColor,
                PrevisPalette.AmbientGround), "A20 地面色", RenderSettings.ambientGroundColor.ToString());
            Sc01PatchCore.Check(Mathf.Abs(Exposure()) < 0.001f, "A21 曝光补偿 0",
                Exposure().ToString("F2"));
        }

        private static void Light()
        {
            var key = LightAt("LGT_STU_Key");
            Sc01PatchCore.Check(key != null && key.intensity > 24f && key.shadows == LightShadows.Soft,
                "A22 主光 26 且投影", key == null ? "缺失" : "int=" + key.intensity.ToString("F1"));
            var rim = LightAt("LGT_STU_Rim");
            Sc01PatchCore.Check(rim != null && rim.enabled && rim.intensity > 13f, "A23 轮廓光 14 已启用");
            var warm = LightAt("TableWarmLight");
            Sc01PatchCore.Check(warm != null && Mathf.Abs(warm.intensity - 9f) < 0.2f, "A24 茶几暖光 9.0");
            var shadows = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.isActiveAndEnabled && light.shadows != LightShadows.None &&
                    light.type != LightType.Directional) shadows++;
            Sc01PatchCore.Check(shadows == 3, "A25 生效投影灯 3 盏", "实际=" + shadows);
        }

        private static void Rig()
        {
            var canvas = HierarchySearch.FindDeep("Spatial_Workbench_Canvas");
            Sc01PatchCore.Check(canvas != null && Mathf.Abs(canvas.position.y - 1.35f) < 0.05f,
                "A26 工作台离地 1.35", canvas == null ? "缺失" : canvas.position.y.ToString("F2"));
            Sc01PatchCore.Check(canvas != null && canvas.position.x < 2.60f, "A27 工作台不穿右墙");
            var origin = HierarchySearch.FindDeep("XR_Origin");
            Sc01PatchCore.Check(origin != null && Sc01PatchCore.Near(origin.position,
                new Vector3(-2.20f, 0f, 0.55f), 0.01f), "A28 出生点在门洞",
                origin == null ? "缺失" : origin.position.ToString());
            var rig = origin == null ? null : origin.GetComponent<XROrigin>();
            Sc01PatchCore.Check(rig != null && Mathf.Abs(rig.CameraYOffset - 1.65f) < 0.01f,
                "A29 站立眼高 1.65", rig == null ? "缺失" : rig.CameraYOffset.ToString("F2"));
        }

        private static void Actor()
        {
            foreach (var name in new[] { "SC01_Performer_A", "SC01_Performer_B" })
            {
                var root = HierarchySearch.FindDeep(name);
                var painted = 0;
                var total = 0;
                if (root != null)
                    foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        total++;
                        if (r.sharedMaterial != null && r.sharedMaterial.name != "Lit") painted++;
                    }
                Sc01PatchCore.Check(total > 0 && painted == total, "A30 " + name + " 材质已挂",
                    painted + "/" + total);
            }
        }

        private static void ExpectActive(string name, bool active, string label)
        {
            var t = HierarchySearch.FindDeep(name);
            Sc01PatchCore.Check(t != null && t.gameObject.activeSelf == active, label,
                t == null ? "缺失" : "active=" + t.gameObject.activeSelf);
        }

        private static void ExpectMat(string name, string expected, string label)
        {
            var t = HierarchySearch.FindDeep(name);
            var r = t == null ? null : t.GetComponent<MeshRenderer>();
            var mat = r == null ? null : r.sharedMaterial;
            Sc01PatchCore.Check(mat != null && mat.name == expected, label,
                mat == null ? "无材质" : mat.name);
        }

        private static void ExpectGroup(string[] names, string expected, string label)
        {
            var ok = 0;
            foreach (var name in names)
            {
                var t = HierarchySearch.FindDeep(name);
                var r = t == null ? null : t.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == expected) ok++;
            }
            Sc01PatchCore.Check(ok == names.Length, label, ok + "/" + names.Length);
        }

        private static void ExpectFixtures()
        {
            var ok = 0;
            var total = 0;
            for (var slot = 1; slot <= 3; slot++)
            {
                var root = HierarchySearch.FindDeep("StudioFixture_Slot" + slot);
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    total++;
                    if (r.sharedMaterial != null && r.sharedMaterial.name == "MAT_STU_Frame_v05") ok++;
                }
            }
            Sc01PatchCore.Check(total > 0 && ok == total, "A15 灯光夹具全部换材质", ok + "/" + total);
        }

        private static Light LightAt(string name) => HierarchySearch.FindDeep(name)?.GetComponent<Light>();

        private static float Exposure()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Studio/SC01StudioVolume.asset"))
            {
                var v = new SerializedObject(asset).FindProperty("postExposure")?.FindPropertyRelative("m_Value");
                if (v != null) return v.floatValue;
            }
            return float.NaN;
        }
    }
}
#endif
