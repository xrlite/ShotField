#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using ShotField.Core;
using ShotField.RuntimeUI;

namespace ShotField.EditorTools
{
    public static class Sc01PrevisBuilder
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const string RedMatPath = "Assets/Models/Characters/MAT_Actor_Red.mat";
        private const string BlueMatPath = "Assets/Models/Characters/MAT_Actor_Blue.mat";
        private const string PlayablePath = "Assets/Scenes/SC01_UnopenedLetter.playable";
        private static readonly Color AmberColor = new Color(0.949f, 0.639f, 0.235f, 0.85f);

        [MenuItem("ShotField/Previs/Run Batch Assembly (M3)")]
        public static void RunBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CleanupLegacySandbox();
            var (actorA, actorB) = SetupPerformers();
            var directorCam = SetupDirectorRig();
            SetupTimelineAndLinkages(actorA, actorB, directorCam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SC01PrevisBuilder] main scene 60.0s Previs assembly successfully completed!");
        }

        private static void CleanupLegacySandbox()
        {
            foreach (var name in new[] { "PREVIS_ACTORS", "PREVIS_CUES", "PrevisSandboxCamera" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }
        }

        private static (GameObject a, GameObject b) SetupPerformers()
        {
            var matA = AssetDatabase.LoadAssetAtPath<Material>(RedMatPath);
            var matB = AssetDatabase.LoadAssetAtPath<Material>(BlueMatPath);

            var a = Sc01PatchCore.FindDeep("SC01_Performer_A")?.gameObject;
            if (a != null)
            {
                a.SetActive(true);
                a.transform.position = new Vector3(-2.20f, 0.00f, 0.15f);
                a.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, 0f, 0.70f).normalized);
                ApplyMaterial(a, matA);
                EnsurePerformerComponents(a, 1.35f, 3.80f);
            }

            var b = Sc01PatchCore.FindDeep("SC01_Performer_B")?.gameObject;
            if (b != null)
            {
                b.SetActive(true);
                b.transform.position = new Vector3(1.18f, 0.00f, 3.05f);
                b.transform.rotation = Quaternion.Euler(0f, 198f, 0f);
                ApplyMaterial(b, matB);
                EnsurePerformerComponents(b, 1.35f, 3.80f);
            }
            return (a, b);
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void EnsurePerformerComponents(GameObject go, float walk, float run)
        {
            var agent = go.GetComponent<NavMeshAgent>();
            if (!agent) agent = go.AddComponent<NavMeshAgent>();
            agent.height = 1.75f;
            agent.radius = 0.30f;
            agent.speed = walk;
            agent.acceleration = 8.0f;
            agent.stoppingDistance = 0.15f;
            agent.updateRotation = false;

            var nav = go.GetComponent<ActorNavigator>();
            if (!nav) nav = go.AddComponent<ActorNavigator>();
            nav.agent = agent;
            nav.animator = go.GetComponentInChildren<Animator>();
            nav.walkSpeed = walk;
            nav.runSpeed = run;
        }

        private static Camera SetupDirectorRig()
        {
            var dir = Sc01PatchCore.FindDeep("Director")?.gameObject ?? new GameObject("Director");
            dir.SetActive(true);
            dir.layer = 0;

            var cam = dir.GetComponent<Camera>();
            if (!cam) cam = dir.AddComponent<Camera>();
            cam.cullingMask |= 1 << 0;

            var lr = dir.GetComponent<LineRenderer>();
            if (!lr) lr = dir.AddComponent<LineRenderer>();
            lr.enabled = true;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.startWidth = lr.endWidth = 0.012f;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.gameObject.layer = 0;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            lr.sharedMaterial = new Material(shader) { color = AmberColor };
            lr.startColor = lr.endColor = AmberColor;

            var traj = Sc01PrevisShotData.CreateSequenceTrajectory();
            lr.positionCount = traj.Count;
            for (int i = 0; i < traj.Count; i++) lr.SetPosition(i, traj.points[i].position);

            var recorder = Object.FindAnyObjectByType<CameraMotionRecorder>();
            if (recorder != null)
            {
                recorder.filmCamera = cam;
                recorder.trajectoryLine = lr;
                recorder.recordColor = AmberColor;
                recorder.readyColor = AmberColor;
                recorder.rawTrajectory = traj;
                recorder.smoothedTrajectory = traj.Clone();
                recorder.useSmoothedForPlayback = false;
            }
            return cam;
        }

        private static void SetupTimelineAndLinkages(GameObject a, GameObject b, Camera cam)
        {
            var timeline = Object.FindAnyObjectByType<DirectorTimeline>();
            if (timeline == null)
            {
                var go = GameObject.Find("Spatial_Workbench_Canvas") ?? new GameObject("SC01_DirectorTimeline");
                timeline = go.AddComponent<DirectorTimeline>();
            }

            timeline.duration = 60.0f;
            timeline.loop = false;
            timeline.playbackSpeed = 1.0f;
            timeline.currentTime = 0.0f;
            timeline.cameraRecorder = Object.FindAnyObjectByType<CameraMotionRecorder>();
            if (a != null) timeline.actorA = a.GetComponent<ActorNavigator>();
            if (b != null) timeline.actorB = b.GetComponent<ActorNavigator>();

            var dir = GameObject.Find("Director");
            if (dir != null)
            {
                var pd = dir.GetComponent<PlayableDirector>();
                if (!pd) pd = dir.AddComponent<PlayableDirector>();
                pd.playableAsset = AssetDatabase.LoadAssetAtPath<PlayableAsset>(PlayablePath);
                pd.timeUpdateMode = DirectorUpdateMode.Manual;
                timeline.playableDirector = pd;
            }

            var actorPlayer = Object.FindAnyObjectByType<ActorTakePlayer>();
            if (actorPlayer != null && a != null)
            {
                actorPlayer.actorTransform = a.transform;
                var take = new ActorTake { takeId = "SC01_Take_A", actorId = "SC01_Performer_A", duration = 60.0f };
                for (int i = 0; i < Sc01PrevisShotData.TotalFrames; i++)
                {
                    float t = (float)i / (Sc01PrevisShotData.TotalFrames - 1) * 60.0f;
                    take.AddKeyframe(t, Sc01PrevisShotData.EvaluateActorA(t), Sc01PrevisShotData.EvaluateActorARotation(t), "PrevisWalk");
                }
                actorPlayer.currentTake = take;
            }

            timeline.AutoWireReferences();
            timeline.Seek(0f);
        }
    }
}
#endif
