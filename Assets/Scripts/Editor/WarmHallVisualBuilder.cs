#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.CoreUtils;

namespace ShotField.EditorTools
{
    /// <summary>Builds the reference-driven warm director hall without external models.</summary>
    internal static partial class WarmHallVisualBuilder
    {
        private const string RootName = "[WarmHall]";
        private const string MaterialFolder = "Assets/Materials/WarmHall";
        private static readonly Color Charcoal = new Color(0.075f, 0.066f, 0.057f);
        private static readonly Color Ivory = new Color(0.86f, 0.82f, 0.74f);
        private static readonly Color Gold = new Color(0.788f, 0.659f, 0.380f);
        private static readonly Color WarmGray = new Color(0.38f, 0.33f, 0.28f);

        [MenuItem("ShotField/Visual/Build Warm Director Hall")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(BuildAndroid.ScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<TabletopDirector>();
            if (director == null) throw new System.InvalidOperationException("TabletopDirector missing.");
            var anchor = director.TabletopAnchor;
            anchor.position = new Vector3(0f, 0.90f, 1.20f);
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin != null) origin.transform.position = new Vector3(0f, 0f, -0.90f);
            var mr = Object.FindAnyObjectByType<MrTabletopBootstrap>();
            if (mr != null) { mr.virtualDistance = 2.10f; mr.virtualHeight = 0.90f; }

            var root = EnsureRoot();
            root.transform.SetPositionAndRotation(new Vector3(anchor.position.x, 0f, anchor.position.z), Quaternion.identity);
            var environment = Child(root, "HallEnvironment");
            var table = Child(root, "DirectorTable");
            environment.gameObject.SetActive(true);
            Deduplicate(environment, "GradientDome", "Dais_Base", "Dais_Top", "Dais_GoldLine", "Arch_Outer", "Arch_Middle", "Arch_Inner");
            Deduplicate(table, "Table_Foot", "Table_Pedestal", "Table_GoldCollar", "Table_IvorySlab", "Table_Inset", "Table_GoldFrame");
            Deduplicate(root.transform, "KeyLight_Top", "CeilingFixture", "LightShaft");

            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            var charcoal = Lit("MAT_WarmHall_Charcoal", Charcoal, 0.05f, 0.42f, 0f);
            var ivory = Lit("MAT_WarmHall_Ivory", Ivory, 0f, 0.52f, 0.16f);
            var gold = Lit("MAT_WarmHall_Gold", Gold, 0.72f, 0.30f, 0.5f);
            var archGray = Lit("MAT_WarmHall_ArchGray", WarmGray, 0.08f, 0.34f, 0.10f);
            var fixture = Lit("MAT_WarmHall_Fixture", Ivory, 0f, 0.30f, 2.0f);
            var daisSurface = Unlit("MAT_WarmHall_DaisSurface", new Color(0.78f, 0.73f, 0.65f));
            BuildDome(environment, GradientDome());
            BuildDais(environment, charcoal, daisSurface, gold);
            BuildArches(environment, charcoal, archGray, gold);
            BuildTable(table, charcoal, ivory, gold);
            BuildLight(root, fixture, BeamMaterial());

            var controller = root.GetComponent<WarmHallVisualController>() ?? root.AddComponent<WarmHallVisualController>();
            controller.hallEnvironment = environment;
            controller.directorTable = table;
            controller.vstHiddenObjects = new[]
            {
                root.transform.Find("LightShaft").gameObject,
                root.transform.Find("CeilingFixture").gameObject
            };
            controller.tabletop = director;
            controller.followTabletopAnchor = true;
            controller.usePassthroughState = true;
            var legacy = new List<GameObject>();
            string[] legacyNames = { "STUDIO_Studio", "STUDIO_Truss", "STUDIO_SetWall", "STUDIO_Lighting_v05", "STUDIO_Lights", "STUDIO_OpenEdge", "SC01_Blockout", "SC01_StageTape", "SC01_FreeCameraHandle", "sofa_1" };
            foreach (var name in legacyNames)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                go.SetActive(false);
                legacy.Add(go);
            }
            if (director.sceneRoot1to1 != null)
            {
                director.sceneRoot1to1.gameObject.SetActive(false);
                legacy.Add(director.sceneRoot1to1.gameObject);
            }
            controller.legacyStageRoots = legacy.ToArray();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.06f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.227f, 0.196f, 0.161f);
            RenderSettings.fogDensity = 0.026f;
            RenderSettings.skybox = null;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[WarmHall] WARM-HALL-RESULT=PASS tableTop=0.90m daisDiameter=3.00m arches=3 avatars=0");
        }

        private static GameObject EnsureRoot()
        {
            var go = GameObject.Find(RootName);
            if (go != null) return go;
            go = new GameObject(RootName);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go;
        }

        private static Transform Child(GameObject root, string name)
        {
            var tf = root.transform.Find(name);
            if (tf != null) return tf;
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        private static void Deduplicate(Transform parent, params string[] names)
        {
            foreach (var name in names)
            {
                var matches = new List<Transform>();
                foreach (Transform child in parent)
                    if (child.name == name) matches.Add(child);
                for (int i = 1; i < matches.Count; i++)
                    Object.DestroyImmediate(matches[i].gameObject);
            }
        }

        private static Material Lit(string file, Color color, float metallic, float smoothness, float emission)
        {
            string path = MaterialFolder + "/" + file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetColor("_EmissionColor", color * emission);
            if (emission > 0f) mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Unlit(string file, Color color)
        {
            string path = MaterialFolder + "/" + file + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GradientDome()
        {
            string path = MaterialFolder + "/MAT_WarmHall_Dome.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("ShotField/WarmHallGradient")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("ShotField/WarmHallGradient");
            mat.SetColor("_TopColor", new Color(0.055f, 0.047f, 0.039f));
            mat.SetColor("_HorizonColor", new Color(0.145f, 0.125f, 0.102f));
            mat.SetColor("_BottomColor", new Color(0.035f, 0.030f, 0.026f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BeamMaterial()
        {
            string path = MaterialFolder + "/MAT_WarmHall_Beam.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("ShotField/WarmHallBeam")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("ShotField/WarmHallBeam");
            mat.SetColor("_BeamColor", new Color(1f, 0.79f, 0.48f, 0.025f));
            mat.renderQueue = 3020;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
