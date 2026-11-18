#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01ArtSceneBuilder
    {
        // 坐标基准以 03_制作/SC01_占位场次制作包.md §2 为唯一真相源（2026-11-18 裁定项 13 已拍板）。
        // 单位米，原点 = 玄关内侧地面中心；+Z 向客厅纵深，+X 向窗。
        internal static readonly Vector3 TablePosition = new Vector3(1.4f, 0.4f, 2.2f);
        internal static readonly Vector3 EnvelopePosition = new Vector3(1.5f, 0.46f, 2.15f);
        internal static readonly Vector3 ActorPosition = new Vector3(0.8f, 0.86f, 1.25f);

        // 椅子错开主角站位：制造包未列此件，保留但不许与人物同点。
        // 角色按 1.72m 等比缩放后 X 半径约 0.67m（中心 0.8 → 右边界 1.47），
        // 故椅子 X 放到 2.0 才留出间隙。
        internal static readonly Vector3 ChairPosition = new Vector3(2.0f, 0.45f, 1.35f);

        // 信封平放茶几面（制作包 y=0.46 = 茶几面 0.45 + 半个厚度），长边指向 -X。
        // 构造侧 criticalFaceNormal=+Z、X 轴为 0.16m 长边，故先绕 X 转 -90 使法线朝上，再绕 Y 转 180 让长边朝 -X。
        internal static readonly Quaternion EnvelopeRotation =
            Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>最近一次构建时茶几实际顶面高度。Blockout 放茶杯时要用同一基准。</summary>
        internal static float LastTableTopY { get; private set; }

        internal static void BuildProps(Transform room)
        {
            Place(room, "Cabinet_Prop", "ShoeCabinet", new Vector3(0.2f, 0.45f, 0.5f), Quaternion.identity);
            // 沙发 X 从 0 改到 0.7：制作包只规定 Z=3.2，未规定 X；实测 AABB 下 X=0 会与冰箱（X=-0.6/Z=3.6）重叠 0.54m
            Place(room, "Sofa_Prop", "Sofa", new Vector3(0.7f, 0.425f, 3.2f), Quaternion.identity);
            Place(room, "Desk_Prop", "CoffeeTable", TablePosition, Quaternion.identity);
            Place(room, "Chair_Prop", "Chair", ChairPosition, Quaternion.identity);
            Place(room, "Plant_Prop", "Plant", new Vector3(-2.2f, 0.6f, 3.2f), Quaternion.identity);
            // 角色目标身高 1.72m：实测 prefab 仅 0.66m 高且悬空 0.53m，按等比缩放后落地
            Place(room, "ActorA_Character", "ActorA", ActorPosition, Quaternion.identity, 1.72f);
            // 信封贴在"茶几实际顶面"而不是制作包的 0.45：实测量茶几只有 0.19m 高，
            // 硬用 0.45 会让信封悬空 26cm。桌面真实高度待美术资产替换后自然回到 0.45。
            float tableTop = ComputeBounds(FindChild(room, "CoffeeTable")).max.y;
            LastTableTopY = tableTop;
            Place(room, "Envelope_Prop", "Envelope", EnvelopePosition, EnvelopeRotation, 0f, tableTop);
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c.gameObject;
            }
            throw new InvalidOperationException("SC01 scene child missing: " + name);
        }

        private static GameObject Place(Transform parent, string assetName, string sceneName,
            Vector3 position, Quaternion rotation, float targetHeight = 0f, float floorY = 0f)
        {
            string path = $"Assets/Prefabs/SC01/{assetName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("SC01 art prefab missing: " + path);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (!instance) throw new InvalidOperationException("SC01 prefab instantiation failed: " + path);
            instance.name = sceneName;
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;

            if (targetHeight > 0f)
            {
                var raw = ComputeBounds(instance);
                if (raw.size.y > 0.001f)
                {
                    float k = targetHeight / raw.size.y;
                    instance.transform.localScale *= k;
                }
            }

            // 落地对齐：把实际包围盒底面贴到 floorY，避免预制品单位不一致导致的悬空
            var b = ComputeBounds(instance);
            var p = instance.transform.localPosition;
            p.y += floorY - b.min.y;
            instance.transform.localPosition = p;
            return instance;
        }

        private static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                var empty = new Bounds(go.transform.position, Vector3.zero);
                return empty;
            }
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
