using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01StudioImportCheck
    {
        private const string ModelPath = "Assets/Models/Studio/SC01_Studio_Shell.fbx";
        private const string ScenePath = "Assets/Scenes/SC01_Blockout.unity";

        [MenuItem("ShotField/SC01/Verify Studio Import and Capture")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before verifying SC01.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            AssetDatabase.ImportAsset(ModelPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("Studio FBX did not import.");
            var meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToArray();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var slots = importer.GetExternalObjectMap().Keys
                .Where(source => source.type == typeof(Material)).Select(source => source.name)
                .OrderBy(name => name, StringComparer.Ordinal);
            Debug.Log($"[SC01 Studio] mesh count={meshes.Length}; material slots: {string.Join(", ", slots)}");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var studio = objects.Single(item => item.name == "STUDIO_Studio");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(studio.gameObject) != ModelPath)
                throw new InvalidOperationException("STUDIO_Studio does not reference the expected FBX.");
            if (objects.Any(item => item.name == "STU_Ceil"))
                throw new InvalidOperationException("Legacy object STU_Ceil still exists.");

            var parts = studio.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var name in new[]
            {
                "STU_WallF", "STU_WallB", "STU_WallL", "STU_WallR",
                "STU_CeilPanel_00", "STU_CeilPanel_01", "STU_CeilPanel_10", "STU_CeilPanel_11",
                "STU_Cloth_A", "STU_Cloth_B", "STU_HangPanel_Face",
            })
            {
                if (parts.Count(part => part.name == name && part.enabled && part.gameObject.activeInHierarchy) != 1)
                    throw new InvalidOperationException($"Expected one visible studio mesh: {name}");
            }

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save SC01.");
            Sc01LookCapture.CaptureFour();
        }
    }
}
