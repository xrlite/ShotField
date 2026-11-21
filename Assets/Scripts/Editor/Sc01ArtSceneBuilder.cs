#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01ArtSceneBuilder
    {
        // 坐标基准改为 06_素材/白模/SC01_Blender场景_20261120.blend（用户认可的布局）。
        // 换算三式：UnityX = BlenderX - 3；UnityZ = BlenderY；UnityY = BlenderZ。
        // 原「制作包 §2」口径把 Blender 的 X 原值直接搬来用，导致整屋家具挤在右半区。
        internal static readonly Vector3 TablePosition = new Vector3(-1.6f, 0.225f, 2.2f);
        internal static readonly Vector3 EnvelopePosition = new Vector3(-1.5f, 0.455f, 2.15f);
        internal static readonly Vector3 ActorPosition = new Vector3(-2.55f, 0f, 2.48f);

        // 单人椅在茶几右下（Blender 2.05/1.32）。
        internal static readonly Vector3 ChairPosition = new Vector3(-0.95f, 0.43f, 1.32f);

        // 信封平放茶几面（制作包 y=0.46 = 茶几面 0.45 + 半个厚度），长边指向 -X。
        // 构造侧 criticalFaceNormal=+Z、X 轴为 0.16m 长边，故先绕 X 转 -90 使法线朝上，再绕 Y 转 180 让长边朝 -X。
        internal static readonly Quaternion EnvelopeRotation =
            Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f);

        /// <summary>最近一次构建时茶几实际顶面高度。Blockout 放茶杯时要用同一基准。</summary>
        internal static float LastTableTopY { get; private set; }

        internal static void BuildProps(Transform room)
        {
            // 玄关柜贴左墙、门洞左侧（Blender 0.17/0.95 → Unity -2.83/0.95）。
            Place(room, "Cabinet_Prop", "ShoeCabinet", new Vector3(-2.83f, 0.45f, 0.95f), Quaternion.identity);
            // 沙发靠后墙居中（Blender 3.0/3.45 → Unity 0/3.45）。
            Place(room, "Sofa_Prop", "Sofa", new Vector3(0f, 0.41f, 3.45f), Quaternion.identity);
            Place(room, "Desk_Prop", "CoffeeTable", TablePosition, Quaternion.identity);
            Place(room, "Chair_Prop", "Chair", ChairPosition, Quaternion.identity);
            // 绿植靠左后角（Blender 0.72/3.45 → Unity -2.28/3.45）。
            Place(room, "Plant_Prop", "Plant", new Vector3(-2.28f, 0.72f, 3.45f), Quaternion.identity);
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
            Physics.SyncTransforms();
            // 以渲染几何为准：Collider 是预制品里写死的近似框，等比缩放后与真实模型对不上，
            // 会算出错误的底面高度（沿地导致悬空）。
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var rb = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) rb.Encapsulate(renderers[i].bounds);
                return rb;
            }
            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                var cb = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) cb.Encapsulate(colliders[i].bounds);
                return cb;
            }
            return new Bounds(go.transform.position, Vector3.zero);
        }
    }
}
#endif
