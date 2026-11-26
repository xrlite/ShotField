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
        private static readonly Color FloorColor = new Color(0.82f, 0.78f, 0.70f, 1f);
        private static readonly Color WallColor = new Color(0.22f, 0.20f, 0.18f, 1f);
        private static readonly Color SofaColor = new Color(0.90f, 0.87f, 0.80f, 1f);
        private static readonly Color WoodColor = new Color(0.42f, 0.30f, 0.20f, 1f);
        private static readonly Color GoldColor = new Color(0.84f, 0.68f, 0.36f, 1f);
        private static readonly Color PlantColor = new Color(0.24f, 0.45f, 0.30f, 1f);

        public int BuiltCount { get; private set; }

        private void Awake()
        {
            var director = GetComponent<TabletopDirector>();
            if (director == null) director = FindAnyObjectByType<TabletopDirector>();
            if (director == null || director.tabletopProxyRoot == null) return;

            var root = director.tabletopProxyRoot;
            root.localPosition = new Vector3(-0.075f, 0.016f, -0.025f);
            foreach (var follower in root.GetComponentsInChildren<TabletopProxyFollower>(true))
                follower.gameObject.SetActive(true);
            if (root.Find("MiniSet_Floor") != null) return;

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
                new Vector3(0.55f, 0.9f, 0.55f), PlantColor);
        }

        private static void CreateBox(Transform root, string name, Vector3 position, Vector3 size, Color color)
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
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
