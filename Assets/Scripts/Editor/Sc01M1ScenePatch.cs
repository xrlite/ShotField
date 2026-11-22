#if UNITY_EDITOR
using System.Linq;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M1 场景补丁：补齐演员 NavMeshAgent/ActorNavigator、落地吸附、去重 SemanticMarker、
    /// 失活旧 Performer_A/B 并改挂引用、局部补齐沙盘代理、持久化画布面板。先扫描打印，再落盘保存。
    /// </summary>
    public static class Sc01M1ScenePatch
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const string Tag = "[M1-SCENE-PATCH]";

        [MenuItem("ShotField/M1/Apply M1 Scene Patch")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DiagnoseScene("BEFORE");
            ApplyActorComponents();
            GroundActors();
            DedupMarkers();
            RetirePerformers();
            TopUpProxies();
            EnsureCanvasComponents();
            DiagnoseScene("AFTER");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + " scene patch saved.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void DiagnoseScene(string phase)
        {
            var navigators = Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include);
            int agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include).Length;
            Debug.Log($"{Tag} {phase} actors={navigators.Length} agents={agents}");
            foreach (var nav in navigators.OrderBy(n => n.name))
            {
                var marker = nav.GetComponent<SemanticMarker>();
                var agent = nav.agent != null ? nav.agent : nav.GetComponent<NavMeshAgent>();
                var mats = nav.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
                Debug.Log($"{Tag} {phase} ACTOR {nav.name} active={nav.gameObject.activeInHierarchy} agent={(agent != null)} " +
                    $"onMesh={(agent != null && agent.isOnNavMesh)} animator={(nav.animator != null)} " +
                    $"marker={(marker != null ? marker.markerId : "<none>")} pos={nav.transform.position} mats=[{string.Join(",", mats.Select(m => m.name))}]");
            }
            var markers = Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Include);
            var dup = markers.GroupBy(m => m.markerId).Where(g => g.Count() > 1).Select(g => g.Key);
            Debug.Log($"{Tag} {phase} markers={markers.Length} dupIds=[{string.Join(",", dup)}]");
            foreach (var m in markers.OrderBy(m => m.markerId))
                Debug.Log($"{Tag} {phase} MARKER id={m.markerId} kind={m.kind} go={m.name} active={m.gameObject.activeInHierarchy}");
            DumpReferences(phase);
            DumpBoard(phase);
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include))
                Debug.Log($"{Tag} {phase} LINE {lr.name} startColor={lr.startColor} matColor={(lr.sharedMaterial != null ? lr.sharedMaterial.color.ToString() : "<null>")}");
        }

        private static void DumpReferences(string phase)
        {
            var take = Object.FindAnyObjectByType<ActorTakePlayer>(FindObjectsInactive.Include);
            if (take != null) Debug.Log($"{Tag} {phase} TAKEPLAYER actorTransform={(take.actorTransform != null ? take.actorTransform.name : "<null>")}");
            var timeline = Object.FindAnyObjectByType<DirectorTimeline>(FindObjectsInactive.Include);
            if (timeline != null)
                Debug.Log($"{Tag} {phase} TIMELINE actorA={(timeline.actorA != null ? timeline.actorA.name : "<null>")} actorB={(timeline.actorB != null ? timeline.actorB.name : "<null>")}");
            foreach (var p in new[] { "SC01_Performer_A", "SC01_Performer_B" })
            { var go = HierarchySearch.FindDeep(p)?.gameObject; Debug.Log($"{Tag} {phase} PERFORMER {p} active={(go != null && go.activeInHierarchy)}"); }
        }

        private static void DumpBoard(string phase)
        {
            var board = Object.FindAnyObjectByType<TabletopDirector>(FindObjectsInactive.Include);
            if (board == null) { Debug.Log($"{Tag} {phase} BOARD <missing>"); return; }
            var proxies = board.tabletopProxyRoot != null
                ? board.tabletopProxyRoot.GetComponentsInChildren<Transform>(true).Where(t => t != board.tabletopProxyRoot).ToArray() : new Transform[0];
            Debug.Log($"{Tag} {phase} BOARD anchor={(board.tabletopAnchor != null ? board.tabletopAnchor.name : "<null>")} " +
                $"proxyRoot={(board.tabletopProxyRoot != null ? board.tabletopProxyRoot.name : "<null>")} proxies={proxies.Length} observer={(board.observerCamera != null ? board.observerCamera.name : "<null>")}");
            foreach (var pr in proxies) Debug.Log($"{Tag} {phase} PROXY {pr.name} active={pr.gameObject.activeInHierarchy}");
        }

        private static void ApplyActorComponents()
        {
            foreach (var nav in CanonicalActors())
            {
                var agent = nav.GetComponent<NavMeshAgent>();
                if (agent == null) agent = nav.gameObject.AddComponent<NavMeshAgent>();
                agent.height = 1.75f; agent.radius = 0.30f; agent.speed = nav.walkSpeed;
                agent.acceleration = 8f; agent.stoppingDistance = 0.15f; agent.updateRotation = false;
                nav.agent = agent;
                if (nav.animator == null) nav.animator = nav.GetComponentInChildren<Animator>();
                nav.walkSpeed = 1.35f; nav.runSpeed = 3.80f;
            }
        }

        private static void GroundActors()
        {
            foreach (var nav in CanonicalActors())
            {
                var ownAgent = nav.GetComponent<NavMeshAgent>();
                bool wired = nav.agent == ownAgent;
                var p = nav.transform.position;
                if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas))
                {
                    float hitY = hit.position.y;
                    float y = Mathf.Abs(p.y - hitY) > 0.05f ? hitY : p.y;
                    nav.transform.position = new Vector3(p.x, y, p.z);
                    Debug.Log($"{Tag} GROUND {nav.name} beforeY={p.y:0.000} hitY={hitY:0.000} afterY={y:0.000} agentWired={wired}");
                }
                else Debug.Log($"{Tag} GROUND {nav.name} 未落地 SamplePosition 失败 beforeY={p.y:0.000} agentWired={wired}");
            }
        }

        private static void DedupMarkers()
        {
            var actors = CanonicalActors();
            for (int i = 0; i < actors.Length; i++)
            {
                var marker = actors[i].GetComponent<SemanticMarker>();
                if (marker == null) marker = actors[i].gameObject.AddComponent<SemanticMarker>();
                marker.markerId = "actor" + (char)('A' + i);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            }
        }

        private static void RetirePerformers()
        {
            var red = FindCanonical("Red");
            var blue = FindCanonical("Blue");
            var take = Object.FindAnyObjectByType<ActorTakePlayer>(FindObjectsInactive.Include);
            if (take != null && take.actorTransform != null && IsLegacy(take.actorTransform))
                take.actorTransform = red != null ? red.transform : take.actorTransform;
            var timeline = Object.FindAnyObjectByType<DirectorTimeline>(FindObjectsInactive.Include);
            if (timeline != null)
            {
                if (timeline.actorA != null && IsLegacy(timeline.actorA.transform)) timeline.actorA = red;
                if (timeline.actorB != null && IsLegacy(timeline.actorB.transform)) timeline.actorB = blue;
            }
            foreach (var nav in Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include).Where(IsLegacyNav))
            {
                var marker = nav.GetComponent<SemanticMarker>();
                if (marker != null) marker.markerId = "";
                nav.gameObject.SetActive(false);
            }
        }

        private static void TopUpProxies()
        {
            var board = Object.FindAnyObjectByType<TabletopDirector>(FindObjectsInactive.Include);
            if (board == null) return;
            board.observerCamera = null;
            var actors = CanonicalActors();
            if (board.tabletopProxyRoot == null)
            {
                var createdRoot = new GameObject("TabletopProxyRoot");
                createdRoot.transform.SetParent(board.TabletopAnchor);
                board.tabletopProxyRoot = createdRoot.transform;
            }
            var root = board.tabletopProxyRoot;
            var existing = root.GetComponentsInChildren<TabletopProxyFollower>(true);
            foreach (var nav in actors)
            {
                if (existing.Any(f => f.actor == nav.transform)) continue;
                var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                proxy.name = "TabletopProxy_" + nav.name;
                proxy.transform.SetParent(root);
                proxy.transform.localScale = nav.transform.lossyScale * board.miniScale;
                board.SyncWorldToProxy(nav.transform, proxy.transform);
                proxy.AddComponent<TabletopProxyFollower>().Bind(nav.transform, board);
            }
            root.gameObject.SetActive(false);
        }

        private static void EnsureCanvasComponents()
        {
            var wb = Object.FindAnyObjectByType<SpatialWorkbench>(FindObjectsInactive.Include);
            if (wb == null) return;
            if (wb.GetComponent<DesktopStudioPanel>() == null) wb.gameObject.AddComponent<DesktopStudioPanel>();
            if (wb.GetComponent<DesktopStudioEditView>() == null) wb.gameObject.AddComponent<DesktopStudioEditView>();
            if (wb.GetComponent<SceneEditRestore>() == null) wb.gameObject.AddComponent<SceneEditRestore>();
        }

        private static ActorNavigator[] CanonicalActors() =>
            Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                .Where(n => n.name.StartsWith("PrevisCast_")).OrderBy(n => n.name).ToArray();

        private static ActorNavigator FindCanonical(string keyword) =>
            CanonicalActors().FirstOrDefault(n => n.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsLegacyNav(ActorNavigator nav) => nav != null && !nav.name.StartsWith("PrevisCast_");

        private static bool IsLegacy(Transform t) => t != null && !t.name.StartsWith("PrevisCast_");
    }
}
#endif
