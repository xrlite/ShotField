using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 用现有沙盘代理根生成可读的 1:20 微缩片场：地面、墙、门与核心家具。
    /// 只新增运行时 primitive，不改场景资产、不加载外部模型、不伪造资产对账数据。
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class TabletopSetComposer : MonoBehaviour
    {
        private static readonly Color FloorColor = new Color(0.18f, 0.17f, 0.17f, 1f);
        private static readonly Color WallColor = new Color(0.11f, 0.11f, 0.13f, 1f);
        private static readonly Color SofaColor = new Color(0.84f, 0.80f, 0.72f, 1f);
        private static readonly Color WoodColor = new Color(0.32f, 0.22f, 0.15f, 1f);
        private static readonly Color GoldColor = new Color(0.86f, 0.70f, 0.36f, 1f);
        private static readonly Color PlantColor = new Color(0.20f, 0.38f, 0.26f, 1f);

        private static readonly string[] LivingRoomItems =
        {
            "MiniSet_BackWall", "MiniSet_LeftWall", "MiniSet_Door", "MiniSet_Sofa",
            "MiniSet_SofaBack", "MiniSet_CoffeeTable", "MiniSet_Cabinet", "MiniSet_Plant"
        };

        private Transform _root;
        private GameObject _genericGrid;

        public int BuiltCount { get; private set; }
        public bool IsNonInteriorScope { get; private set; }

        public static void SetNonInteriorScope(bool nonInterior)
        {
            var composer = FindAnyObjectByType<TabletopSetComposer>();
            if (composer != null) composer.ApplyScope(nonInterior);
            if (!nonInterior) return;
            var director = FindAnyObjectByType<TabletopDirector>();
            if (director != null && director.currentMode != TabletopDirector.ScaleMode.TabletopMini)
                director.SwitchMode(TabletopDirector.ScaleMode.TabletopMini);
        }

        private void Awake()
        {
            var director = GetComponent<TabletopDirector>();
            if (director == null) director = FindAnyObjectByType<TabletopDirector>();
            if (director == null || director.tabletopProxyRoot == null) return;

            _root = director.tabletopProxyRoot;
            var root = _root;
            root.localPosition = new Vector3(-0.075f, 0.016f, -0.025f);
            foreach (var follower in root.GetComponentsInChildren<TabletopProxyFollower>(true))
                follower.gameObject.SetActive(true);
            if (root.Find("MiniSet_Floor") == null)
            {
                CreateBox(root, "MiniSet_Floor", new Vector3(1.5f, -0.16f, 0.5f),
                    new Vector3(7.6f, 0.16f, 7.6f), FloorColor);
                CreateBox(root, "MiniSet_BackWall", new Vector3(1.5f, 1.25f, 4.15f),
                    new Vector3(7.6f, 2.5f, 0.14f), WallColor);
                CreateBox(root, "MiniSet_LeftWall", new Vector3(-2.25f, 1.25f, 0.5f),
                    new Vector3(0.14f, 2.5f, 7.6f), WallColor);
                CreateBox(root, "MiniSet_Door", new Vector3(1.5f, 0.95f, 4.05f),
                    new Vector3(0.95f, 1.9f, 0.08f), GoldColor);
                CreateBox(root, "MiniSet_Sofa", new Vector3(1.45f, 0.38f, 0.65f),
                    new Vector3(2.5f, 0.65f, 0.95f), SofaColor);
                CreateBox(root, "MiniSet_SofaBack", new Vector3(1.45f, 0.78f, 0.25f),
                    new Vector3(2.5f, 0.75f, 0.22f), SofaColor);
                CreateBox(root, "MiniSet_CoffeeTable", new Vector3(1.45f, 0.22f, 2.15f),
                    new Vector3(1.35f, 0.18f, 0.75f), WoodColor);
                CreateBox(root, "MiniSet_Cabinet", new Vector3(-1.25f, 0.62f, 3.55f),
                    new Vector3(0.85f, 1.25f, 0.55f), WoodColor);
                CreateBox(root, "MiniSet_Plant", new Vector3(3.35f, 0.45f, 3.45f),
                    new Vector3(0.55f, 0.9f, 0.55f), PlantColor, 0.25f, 0f);
                CreateBox(root, "MiniSet_GoldTrim", new Vector3(1.5f, -0.07f, 0.5f),
                    new Vector3(7.72f, 0.03f, 7.72f), GoldColor, 0.65f, 0.55f);
            }
            ApplyScope(WelcomeHubProjectState.IsNonInteriorScope);
        }

        public void ApplyScope(bool nonInterior)
        {
            IsNonInteriorScope = nonInterior;
            if (_root == null) return;
            foreach (var itemName in LivingRoomItems)
            {
                var child = _root.Find(itemName);
                if (child != null) child.gameObject.SetActive(!nonInterior);
            }
            var trim = _root.Find("MiniSet_GoldTrim");
            if (trim != null) trim.gameObject.SetActive(!nonInterior);
            if (nonInterior && _genericGrid == null) _genericGrid = BuildGenericGrid(_root);
            if (_genericGrid != null) _genericGrid.SetActive(nonInterior);
        }

        private static GameObject BuildGenericGrid(Transform root)
        {
            var existing = root.Find("MiniSet_GenericGrid");
            if (existing != null) return existing.gameObject;
            var grid = new GameObject("MiniSet_GenericGrid");
            grid.transform.SetParent(root, false);
            var lineColor = new Color(0.79f, 0.66f, 0.38f, 1f);
            for (int i = -2; i <= 2; i++)
            {
                float offset = i * 1.4f;
                CreateBox(grid.transform, $"GridX_{i}", new Vector3(1.5f, -0.068f, 0.5f + offset),
                    new Vector3(7.2f, 0.024f, 0.04f), lineColor, 0.5f, 0.3f);
                CreateBox(grid.transform, $"GridZ_{i}", new Vector3(1.5f + offset, -0.068f, 0.5f),
                    new Vector3(0.04f, 0.024f, 7.2f), lineColor, 0.5f, 0.3f);
            }
            return grid;
        }

        private static void CreateBox(Transform root, string name, Vector3 position, Vector3 size, Color color,
            float smoothness = 0.35f, float metallic = 0.05f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
