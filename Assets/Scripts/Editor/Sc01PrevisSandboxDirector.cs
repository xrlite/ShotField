#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01PrevisSandboxDirector
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const string AmberHex = "#F2A33C";

        [MenuItem("ShotField/Previs/Setup Sandbox & Capture 1080P")]
        public static void SetupAndCapture()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            BakeFloorNavMesh();
            SetupPrevisLighting();
            SetupSixActors();
            SetupAmberCues();
            SetupSandboxCameraAndCapture(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PrevisSandbox] Previs 6色沙盘与琥珀轨迹构建完成并保存！");
        }

        private static void BakeFloorNavMesh()
        {
            var floor = GameObject.Find("Floor") ?? GameObject.Find("Ground");
            if (floor == null) return;
            var surface = floor.GetComponent<NavMeshSurface>();
            if (surface == null) surface = floor.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private static void SetupPrevisLighting()
        {
            var sun = GameObject.Find("Directional Light") ?? GameObject.Find("Sun");
            if (sun == null) return;
            var light = sun.GetComponent<Light>();
            if (light == null) return;
            light.color = new Color(0.96f, 0.95f, 0.94f);
            light.intensity = 1.15f; light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
        }

        private static void SetupSixActors()
        {
            var oldA = GameObject.Find("SC01_Performer_A"); if (oldA != null) oldA.SetActive(false);
            var oldB = GameObject.Find("SC01_Performer_B"); if (oldB != null) oldB.SetActive(false);
            var root = GameObject.Find("PREVIS_ACTORS") ?? new GameObject("PREVIS_ACTORS");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Models/Armature.fbx");
            var (colors, names, positions, yaws) = GetActorSpecs();

            for (int i = 0; i < names.Length; i++)
            {
                var child = root.transform.Find(names[i]);
                GameObject actorGo = child != null ? child.gameObject : null;
                if (actorGo == null && fbx != null) { actorGo = Object.Instantiate(fbx, root.transform); actorGo.name = names[i]; }
                if (actorGo == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(actorGo))
                    PrefabUtility.UnpackPrefabInstance(actorGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                actorGo.transform.position = positions[i]; actorGo.transform.rotation = Quaternion.Euler(0f, yaws[i], 0f);
                ApplySolidColor(actorGo, colors[i]);
                if (i == 0)
                {
                    var agent = actorGo.GetComponent<NavMeshAgent>() ?? actorGo.AddComponent<NavMeshAgent>();
                    if (agent != null) { agent.height = 1.75f; agent.radius = 0.30f; }
                    var nav = actorGo.GetComponent<ActorNavigator>() ?? actorGo.AddComponent<ActorNavigator>();
                    nav.agent = agent; nav.animator = actorGo.GetComponentInChildren<Animator>();
                }
            }
        }

        private static (Color[], string[], Vector3[], float[]) GetActorSpecs() => (
            new[] { new Color(0.95f, 0.15f, 0.15f), new Color(0.98f, 0.50f, 0.10f),
                    new Color(0.12f, 0.45f, 0.98f), new Color(0.98f, 0.78f, 0.10f),
                    new Color(0.15f, 0.85f, 0.30f), new Color(0.65f, 0.20f, 0.95f) },
            new[] { "Actor_Red_Protagonist", "Actor_Orange", "Actor_Blue", "Actor_Yellow", "Actor_Green", "Actor_Purple" },
            new[] { new Vector3(-0.10f, 0f, 1.40f), new Vector3(-1.40f, 0.20f, 1.20f), new Vector3(-1.80f, 0f, 3.60f),
                    new Vector3(-0.20f, 0f, 3.60f), new Vector3(1.30f, 0f, 3.50f), new Vector3(1.40f, 0f, 1.60f) },
            new[] { 30f, 75f, 120f, 195f, 210f, 280f }
        );

        private static void ApplySolidColor(GameObject go, Color col)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { color = col };
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int m = 0; m < mats.Length; m++) mats[m] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void SetupAmberCues()
        {
            ColorUtility.TryParseHtmlString(AmberHex, out var amber);
            var cues = GameObject.Find("PREVIS_CUES") ?? new GameObject("PREVIS_CUES");
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { color = amber };

            ResetChild(cues, "CameraArc");
            var arcGo = new GameObject("CameraArc"); arcGo.transform.SetParent(cues.transform);
            var lr = arcGo.AddComponent<LineRenderer>();
            lr.startWidth = lr.endWidth = 0.045f; lr.material = mat; lr.positionCount = 36;
            Vector3 center = new Vector3(-0.2f, 0.02f, 1.6f); float radius = 1.7f;
            for (int i = 0; i < 36; i++)
            {
                float angle = Mathf.Lerp(130f, 260f, i / 35f) * Mathf.Deg2Rad;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            ResetChild(cues, "ActorDottedPath");
            var pathGo = new GameObject("ActorDottedPath"); pathGo.transform.SetParent(cues.transform);
            var plr = pathGo.AddComponent<LineRenderer>();
            plr.startWidth = plr.endWidth = 0.035f; plr.material = mat; plr.positionCount = 20;
            Vector3 p0 = new Vector3(-0.20f, 0.02f, 3.4f), p1 = new Vector3(-0.55f, 0.02f, 2.2f), p2 = new Vector3(0.70f, 0.02f, 1.20f);
            for (int i = 0; i < 20; i++)
            {
                float t = i / 19f;
                plr.SetPosition(i, (1-t)*(1-t)*p0 + 2*(1-t)*t*p1 + t*t*p2);
            }

            ResetChild(cues, "AmberTargetRing");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "AmberTargetRing"; ring.transform.SetParent(cues.transform);
            ring.transform.position = new Vector3(0.70f, 0.02f, 1.20f);
            ring.transform.localScale = new Vector3(0.55f, 0.015f, 0.55f);
            var col = ring.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            ring.GetComponent<Renderer>().material = mat;
        }

        private static void ResetChild(GameObject parent, string name)
        {
            var child = parent.transform.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void SetupSandboxCameraAndCapture(UnityEngine.SceneManagement.Scene scene)
        {
            var hidden = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if ((n.Contains("Ceil") || n == "STUDIO_Truss" || n == "STUDIO_Studio" || n.StartsWith("EntryWall") || n == "EntryLintel") && t.gameObject.activeSelf)
                { t.gameObject.SetActive(false); hidden.Add(t.gameObject); }
            }

            var camGo = GameObject.Find("PrevisSandboxCamera") ?? new GameObject("PrevisSandboxCamera");
            var cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
            var ucamType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (ucamType != null && camGo.GetComponent(ucamType) == null) camGo.AddComponent(ucamType);

            camGo.transform.position = new Vector3(-3.80f, 5.40f, -1.80f);
            camGo.transform.LookAt(new Vector3(0.0f, 0.70f, 2.30f));
            cam.fieldOfView = 50f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG(); Object.DestroyImmediate(tex);
            // 以前这里往两个绝对路径各写一份，其中一个把本机用户名和另一个工具的
            // 私有工作目录写进了公开仓库。现在只认一个可配置的证据目录，缺省落 Temp。
            var evidenceDir = System.Environment.GetEnvironmentVariable("SHOTFIELD_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(evidenceDir))
            {
                evidenceDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShotFieldEvidence");
            }
            System.IO.Directory.CreateDirectory(evidenceDir);
            var path1 = System.IO.Path.Combine(evidenceDir, "previs_sandbox_preview.png");
            File.WriteAllBytes(path1, bytes);

            foreach (var g in hidden) g.SetActive(true);
            Debug.Log($"[PrevisSandbox] 1080P 预演预览图已生成: {path1}");
        }
    }
}
#endif