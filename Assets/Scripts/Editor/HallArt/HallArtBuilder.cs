#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>Builds the reference-matched dark hall in an independent art scene; saves scene + hall prefab.</summary>
    internal static class HallArtBuilder
    {
        static readonly Color Charcoal = new Color(0.11f, 0.10f, 0.086f);
        static readonly Color WallTone = new Color(0.085f, 0.075f, 0.062f);
        static readonly Color Ivory = new Color(0.953f, 0.933f, 0.894f);
        static readonly Color Beige = new Color(0.847f, 0.812f, 0.753f);
        static readonly Color Graphite = new Color(0.20f, 0.188f, 0.165f);
        static readonly Color WarmGray = new Color(0.431f, 0.404f, 0.361f);
        static readonly Color Gold = new Color(0.88f, 0.58f, 0.18f);

        static Material Lit(string name, Color c, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static Material Glow(string name, Color c, float power)
        {
            var m = Lit(name, c, 0.4f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * power);
            return m;
        }

        static Material GlowOnly(string name, Color c, float power)
        {
            var m = Lit(name, new Color(0.05f, 0.03f, 0.02f), 0.2f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * power);
            return m;
        }

        static Material Glass(string name, Color c, float alpha, bool additive = false)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            return m;
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            bool noShadow = mat.name == "Beam" || mat.name == "Emitter" || mat.name.StartsWith("Monitor")
                || mat.name.StartsWith("Thumb") || mat.name == "MainFrame";
            r.shadowCastingMode = noShadow
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static GameObject Box(Transform p, string n, Material m, Vector3 pos, Vector3 size, float b)
        {
            return Part(p, n, HallMeshKit.BevelBox(size.x, size.y, size.z, b), m, pos, Quaternion.identity);
        }

        public static GameObject Build(bool saveScene, bool savePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("[HallArt]").transform;
            var floor = Lit("HallFloor", Charcoal, 0.15f);
            var ivory = Lit("HallIvory", Ivory, 0.35f);
            var beige = Lit("HallBeige", Beige, 0.25f);
            var dark = Lit("HallDark", Graphite, 0.4f);
            var goldRim = Glow("HallGoldRim", Gold, 0.7f);

            Part(root, "Floor", HallMeshKit.BevelBox(24f, 0.1f, 24f, 0.02f), floor, new Vector3(0f, -0.05f, 0f), Quaternion.identity);
            Part(root, "Platform", HallMeshKit.Disc(2.6f, 0.07f, 0.03f, 96), ivory, Vector3.zero, Quaternion.identity);
            Part(root, "PlatformGoldRing", HallMeshKit.Ring(2.6f, 0.014f, 0.075f, 128), goldRim, Vector3.zero, Quaternion.identity);

            var walls = Lit("HallWall", WallTone, 0.1f);
            Box(root, "WallBack", walls, new Vector3(0f, 2.3f, -6.5f), new Vector3(13f, 4.6f, 0.2f), 0.02f);
            Box(root, "WallLeft", walls, new Vector3(-6.2f, 2.3f, 0f), new Vector3(0.2f, 4.6f, 13f), 0.02f);
            Box(root, "WallRight", walls, new Vector3(6.2f, 2.3f, 0f), new Vector3(0.2f, 4.6f, 13f), 0.02f);
            Box(root, "WallFront", walls, new Vector3(0f, 2.3f, 6.5f), new Vector3(13f, 4.6f, 0.2f), 0.02f);

            var table = new GameObject("DirectorTable").transform; table.SetParent(root, false);
            table.localPosition = new Vector3(0f, 0f, 0.9f);
            Box(table, "TablePedestal", Lit("HallPedestal", new Color(0.07f, 0.062f, 0.052f), 0.3f), new Vector3(0f, 0.44f, 0f), new Vector3(0.72f, 0.82f, 0.5f), 0.03f);
            Box(table, "TableTop", ivory, new Vector3(0f, 0.87f, 0f), new Vector3(1.9f, 0.06f, 0.85f), 0.02f);
            Box(table, "InlayFront", goldRim, new Vector3(0f, 0.901f, 0.41f), new Vector3(1.82f, 0.004f, 0.006f), 0.001f);
            Box(table, "InlayBack", goldRim, new Vector3(0f, 0.901f, -0.41f), new Vector3(1.82f, 0.004f, 0.006f), 0.001f);

            var dio = new GameObject("MiniatureSet").transform; dio.SetParent(table, false);
            dio.localPosition = new Vector3(0f, 0.90f, 0f);
            Box(dio, "DioFloor", ivory, new Vector3(0f, 0.01f, 0f), new Vector3(0.72f, 0.02f, 0.46f), 0.006f);
            Box(dio, "DioWallBack", ivory, new Vector3(0f, 0.15f, -0.22f), new Vector3(0.72f, 0.28f, 0.02f), 0.006f);
            Box(dio, "DioWallLeft", ivory, new Vector3(-0.35f, 0.15f, 0f), new Vector3(0.02f, 0.28f, 0.46f), 0.006f);
            Box(dio, "DioSofaSeat", ivory, new Vector3(0.05f, 0.06f, -0.13f), new Vector3(0.26f, 0.07f, 0.1f), 0.015f);
            Box(dio, "DioSofaBack", ivory, new Vector3(0.05f, 0.12f, -0.18f), new Vector3(0.26f, 0.12f, 0.03f), 0.012f);
            Box(dio, "DioChairA", ivory, new Vector3(-0.16f, 0.06f, 0.1f), new Vector3(0.09f, 0.08f, 0.09f), 0.015f);
            Box(dio, "DioChairB", ivory, new Vector3(0.24f, 0.06f, 0.1f), new Vector3(0.09f, 0.08f, 0.09f), 0.015f);
            Box(dio, "DioCoffee", ivory, new Vector3(0.05f, 0.045f, 0.02f), new Vector3(0.12f, 0.035f, 0.06f), 0.008f);
            Box(dio, "DioCamHead", dark, new Vector3(0.3f, 0.16f, 0.16f), new Vector3(0.05f, 0.04f, 0.06f), 0.008f);
            Box(dio, "DioCamPole", dark, new Vector3(0.3f, 0.08f, 0.16f), new Vector3(0.012f, 0.14f, 0.012f), 0.003f);

            Chair(root, "ChairLeft", beige, new Vector3(-1.35f, 0.07f, 0.9f), 90f);
            Chair(root, "ChairRight", beige, new Vector3(1.35f, 0.07f, 0.9f), -90f);

            float az = -1.5f;
            Part(root, "ArchOuter", HallMeshKit.Arch(1.7f, 3.5f, 0.22f, 0.34f, 0.05f, 40), Glow("ArchGraphite", Graphite, 0.10f), new Vector3(0f, 0.07f, az), Quaternion.identity);
            Part(root, "ArchMiddle", HallMeshKit.Arch(1.42f, 3.15f, 0.2f, 0.3f, 0.045f, 40), Glow("ArchWarmGray", WarmGray, 0.12f), new Vector3(0f, 0.07f, az + 0.42f), Quaternion.identity);
            Part(root, "ArchInner", HallMeshKit.Arch(1.14f, 2.8f, 0.18f, 0.26f, 0.04f, 40), Glow("ArchGold", Gold, 0.6f), new Vector3(0f, 0.07f, az + 0.84f), Quaternion.identity);
            Part(root, "PortalGlow", HallMeshKit.BevelBox(2.4f, 2.9f, 0.05f, 0.02f), GlowOnly("PortalWarm", new Color(1f, 0.62f, 0.30f), 1.6f), new Vector3(0f, 1.45f, az - 1.1f), Quaternion.identity);

            var monitor = new GameObject("GlassMonitor").transform; monitor.SetParent(root, false);
            monitor.localPosition = new Vector3(0f, 1.62f, 0.35f);
            monitor.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Box(monitor, "MonitorGlass", Glass("MonitorGlass", new Color(0.08f, 0.09f, 0.10f), 0.82f), Vector3.zero, new Vector3(1.0f, 0.56f, 0.012f), 0.004f);
            for (int i = 0; i < 4; i++)
                Box(monitor, "Thumb" + i, Glow("Thumb" + i, new Color(0.35f, 0.33f, 0.30f), 0.05f), new Vector3(0.3f, 0.16f - i * 0.11f, 0.01f), new Vector3(0.24f, 0.08f, 0.004f), 0.002f);
            Box(monitor, "MainFrame", Glow("MainFrame", new Color(0.40f, 0.38f, 0.34f), 0.06f), new Vector3(-0.14f, 0.02f, 0.01f), new Vector3(0.56f, 0.34f, 0.004f), 0.002f);
            Box(monitor, "Playhead", goldRim, new Vector3(-0.14f, -0.2f, 0.012f), new Vector3(0.56f, 0.006f, 0.004f), 0.001f);

            Lamp(root, "KeySpot", new Vector3(0f, 4.6f, 0.9f), new Vector3(0f, 0.6f, 0.9f), 30f, 58f, true);
            Part(root, "KeyEmitter", HallMeshKit.Disc(0.16f, 0.03f, 0.01f, 32), Glow("Emitter", new Color(1f, 0.93f, 0.8f), 2.2f), new Vector3(0f, 4.6f, 0.9f), Quaternion.identity);
            Part(root, "KeyBeam", HallMeshKit.Cone(0.16f, 2.0f, 4.55f, 0.1f, 48), Glass("Beam", new Color(1f, 0.9f, 0.72f), 0.015f, true), new Vector3(0f, 0f, 0.9f), Quaternion.identity);
            Lamp(root, "DioFill", new Vector3(0f, 2.2f, 2.2f), new Vector3(0f, 1.0f, 0.9f), 6f, 50f, false);
            Lamp(root, "FloorPool", new Vector3(0f, 5.2f, 0.4f), new Vector3(0f, 0f, 0.4f), 3.5f, 85f, false);
            Lamp(root, "PortalSpill", new Vector3(0f, 1.5f, az - 0.7f), new Vector3(0f, 1.2f, 2f), 2f, 90f, false);
            Lamp(root, "WashLeft", new Vector3(-3.5f, 3.6f, -4.5f), new Vector3(-4.5f, 1.2f, -6.4f), 3f, 80f, false);
            Lamp(root, "WashRight", new Vector3(3.5f, 3.6f, -4.5f), new Vector3(4.5f, 1.2f, -6.4f), 3f, 80f, false);

            if (savePrefab)
            {
                System.IO.Directory.CreateDirectory("Assets/Prefabs/Stages");
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/Stages/HallArt.prefab");
            }
            if (saveScene) EditorSceneManager.SaveScene(scene, "Assets/Scenes/Art_Hall.unity");
            return root.gameObject;
        }

        static void Chair(Transform root, string name, Material m, Vector3 pos, float yaw)
        {
            var c = new GameObject(name).transform; c.SetParent(root, false);
            c.localPosition = pos; c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(c, "Seat", m, new Vector3(0f, 0.32f, 0f), new Vector3(0.55f, 0.12f, 0.5f), 0.03f);
            Box(c, "Back", m, new Vector3(0f, 0.6f, -0.24f), new Vector3(0.55f, 0.5f, 0.1f), 0.03f);
            Box(c, "ArmL", m, new Vector3(-0.26f, 0.45f, 0f), new Vector3(0.08f, 0.22f, 0.45f), 0.025f);
            Box(c, "ArmR", m, new Vector3(0.26f, 0.45f, 0f), new Vector3(0.08f, 0.22f, 0.45f), 0.025f);
            for (int i = 0; i < 4; i++)
                Box(c, "Leg" + i, m, new Vector3(i % 2 == 0 ? -0.22f : 0.22f, 0.13f, i < 2 ? -0.2f : 0.2f), new Vector3(0.04f, 0.26f, 0.04f), 0.01f);
        }

        static void Lamp(Transform root, string name, Vector3 pos, Vector3 target, float intensity, float angle, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((target - pos).normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.intensity = intensity; l.spotAngle = angle; l.range = 16f;
            l.color = new Color(1f, 0.92f, 0.78f);
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.75f;
        }
    }
}
#endif
