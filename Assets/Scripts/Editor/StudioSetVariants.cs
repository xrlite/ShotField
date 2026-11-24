using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotField.EditorTools
{
    public static class StudioSetVariants
    {
        const string Source = "Assets/Scenes/SC01_Main.unity";

        [MenuItem("ShotField/Art/Build alternate sets")]
        public static void Build()
        {
            Create("SC01_MorningRoom", true);
            Create("SC01_InterviewRoom", false);
        }

        static void Create(string name, bool morning)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (File.Exists(path)) return;
            var scene = EditorSceneManager.OpenPreviewScene(Source);
            try
            {
                var sun = Find(scene, "WindowMoonlight").GetComponent<Light>();
                sun.color = morning ? new Color(1f, .86f, .65f) : new Color(.54f, .68f, .86f);
                sun.intensity = morning ? 1.45f : .32f;
                var key = Find(scene, "TableWarmLight").GetComponent<Light>();
                key.spotAngle = morning ? 78f : 42f;
                key.intensity = morning ? 1.4f : 4.1f;
                if (morning)
                    Find(scene, "Plant").transform.position += new Vector3(0, 0, -1.1f);
                else
                {
                    Find(scene, "Sofa").SetActive(false);
                    Find(scene, "Plant").SetActive(false);
                    key.color = new Color(1f, .88f, .74f);
                }
                if (!EditorSceneManager.SaveScene(scene, path, true))
                    throw new IOException("Could not save " + path);
                Debug.Log("STUDIO-SET-SAVED=" + path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            throw new InvalidOperationException(scene.name + " missing " + name);
        }
    }
}
