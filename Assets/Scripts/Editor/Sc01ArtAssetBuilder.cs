#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGLTF;

namespace ShotField.EditorTools
{
    internal static class Sc01ArtAssetBuilder
    {
        private const string ModelRoot = "Assets/Models/Furniture";
        private static readonly (string folder, string file)[] Furniture =
        {
            ("modern_arm_chair_01", "modern_arm_chair_01_1k.gltf"),
            ("modern_coffee_table_01", "modern_coffee_table_01_1k.gltf"),
            ("Sofa_01", "Sofa_01_1k.gltf"),
            ("modern_wooden_cabinet", "modern_wooden_cabinet_1k.gltf"),
            ("potted_plant_02", "potted_plant_02_1k.gltf")
        };

        internal static void BuildAll()
        {
            ConfigureHumanoid();
            foreach (var item in Furniture) ImportFurniture(item.folder, item.file);
            Sc01EnvelopeFactory.Build();
            Sc01PrefabFactory.BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SC01-ART-ASSET-RESULT=PASS");
        }

        private static void ConfigureHumanoid()
        {
            const string path = "Assets/Models/Characters/Biped_Humanoid.fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Biped Humanoid FBX importer unavailable.");
            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Biped Humanoid did not generate a valid Humanoid Avatar.");
        }

        private static void ImportFurniture(string folder, string gltfFile)
        {
            string sourceAsset = $"{ModelRoot}/{folder}/{gltfFile}";
            string outputAsset = $"{ModelRoot}/{folder}/Imported/{folder}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(outputAsset) == null)
            {
                string source = Path.GetFullPath(sourceAsset);
                string output = Path.GetFullPath($"{ModelRoot}/{folder}/Imported");
                Directory.CreateDirectory(output);
                var importer = new GLTFEditorImporter();
                importer.setupForPath(source, output, folder, false);
                importer.Load(true);
                for (int i = 0; i < 20000 && !File.Exists(Path.GetFullPath(outputAsset)); i++) importer.Update();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputAsset);
            if (prefab == null) throw new InvalidOperationException($"GLTF import failed: {sourceAsset}");
            UpgradeMaterials($"{ModelRoot}/{folder}/Imported");
        }

        private static void UpgradeMaterials(string folder)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader unavailable.");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null) continue;
                if (material.shader != shader)
                {
                    var baseMap = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    var normalMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                    var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    material.shader = shader;
                    material.SetColor("_BaseColor", color);
                    if (baseMap) material.SetTexture("_BaseMap", baseMap);
                    if (normalMap)
                    {
                        material.SetTexture("_BumpMap", normalMap);
                        material.EnableKeyword("_NORMALMAP");
                    }
                }
                material.SetFloat("_Cull", 0f);
                material.SetFloat("_Smoothness", 0.18f);
                material.doubleSidedGI = true;
                EditorUtility.SetDirty(material);
            }
        }
    }
}
#endif
