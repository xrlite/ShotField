#if UNITY_EDITOR
using System;
using System.IO;
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
    /// M1 编辑链门禁：选人/落位/标记/存档往返/面板挂载/道具绑定六条可失败断言。
    /// 场景真的缺 Agent、标记重名、Performer 仍可见、存档回读漂移、面板缺失、绑定不足时必须 FAIL。
    /// </summary>
    public static class Sc01M1EditVerifier
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const int Expected = 6;

        [MenuItem("ShotField/Verifiers/Run M1 Edit Verifier")]
        public static void Run()
        {
            int passed = 0;
            void Require(string label, bool condition)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + label);
                passed++;
                Debug.Log($"[M1-EDIT] PASS: {label}");
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. 六位正式演员都有 Agent + Navigator，且初始位置能被 NavMesh 覆盖。
            var actors = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                .Where(n => n.name.StartsWith("PrevisCast_")).OrderBy(n => n.name).ToArray();
            bool actorsOk = actors.Length == 6;
            foreach (var nav in actors)
            {
                var agent = nav.agent != null ? nav.agent : nav.GetComponent<NavMeshAgent>();
                bool covered = agent != null && (agent.isOnNavMesh ||
                    NavMesh.SamplePosition(nav.transform.position, out _, 2f, NavMesh.AllAreas));
                if (agent == null || !covered) actorsOk = false;
                Debug.Log($"[M1-EDIT] actor={nav.name} agent={(agent != null)} covered={covered}");
            }
            Require("六位 PrevisCast 演员均挂 NavMeshAgent + ActorNavigator 且初始位置可行走", actorsOk);

            // 2. 标记 id 唯一，actorA 仍能命中一位可见演员。
            var markers = UnityEngine.Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Include);
            var used = markers.Where(m => !string.IsNullOrEmpty(m.markerId))
                .GroupBy(m => m.markerId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            var actorA = SemanticMarker.Find("actorA");
            Require("场景内 SemanticMarker id 无重复，且 Find(\"actorA\") 命中一位可见演员",
                used.Length == 0 && actorA != null && actorA.gameObject.activeInHierarchy);

            // 3. 旧 Performer_A/B 不可见，不与六色阵容共存。
            var performerA = HierarchySearch.FindDeep("SC01_Performer_A")?.gameObject;
            var performerB = HierarchySearch.FindDeep("SC01_Performer_B")?.gameObject;
            Require("SC01_Performer_A/B 已失活，不与六色演员同时可见",
                (performerA == null || !performerA.activeInHierarchy) &&
                (performerB == null || !performerB.activeInHierarchy));

            // 4. 存档快照保存→读取往返：6 演员 + 1 道具，位置误差 <0.001。
            Require("快照保存/读取往返一致（6 演员 + 1 道具）", RoundtripOk());

            // 5. 画布上同时存在 PC 工作室面板与编辑视图组件。
            var wb = UnityEngine.Object.FindAnyObjectByType<SpatialWorkbench>(FindObjectsInactive.Include);
            var panel = FindOnCanvas<DesktopStudioPanel>(wb);
            var editView = FindOnCanvas<DesktopStudioEditView>(wb);
            Require("画布挂载 DesktopStudioPanel 与 DesktopStudioEditView", panel != null && editView != null);

            // 6. 道具库 18 条，浏览器绑定 ≥6。
            var browser = FindOnCanvas<PropBrowserUI>(wb);
            Require("PropLibrary.Count == 18 且 PropBrowserUI 绑定 ≥6",
                PropLibrary.Count == 18 && browser != null && browser.BindingCount >= 6);

            string result = passed == Expected ? "PASS" : "FAIL";
            Debug.Log($"M1-EDIT-RESULT={result} ({passed}/{Expected})");
            if (Application.isBatchMode) EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }

        private static T FindOnCanvas<T>(SpatialWorkbench wb) where T : Component
        {
            var hit = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return hit != null ? hit : (wb != null ? wb.GetComponent<T>() : null);
        }

        private static bool RoundtripOk()
        {
            var oldEnv = Environment.GetEnvironmentVariable("SHOTFIELD_SAVE_DIR");
            var tmp = Path.Combine(Application.dataPath, "..", "Saves_M1Verify");
            Directory.CreateDirectory(tmp);
            try
            {
                Environment.SetEnvironmentVariable("SHOTFIELD_SAVE_DIR", tmp);
                var dto = new SceneEditStateDto();
                dto.actors = new[]
                {
                    new SceneActorDto("PrevisCast_A_Red", new Vector3(-1.12f, 0.05f, 2.07f), Quaternion.identity),
                    new SceneActorDto("PrevisCast_B_Blue", new Vector3(-0.64f, 0.05f, 1.08f), Quaternion.Euler(0, 30, 0)),
                    new SceneActorDto("PrevisCast_C_Yellow", new Vector3(0.30f, 0.05f, 0.50f), Quaternion.identity),
                    new SceneActorDto("PrevisCast_D_Green", new Vector3(1.40f, 0.05f, 0.50f), Quaternion.Euler(0, 90, 0)),
                    new SceneActorDto("PrevisCast_E_Violet", new Vector3(2.34f, 0.05f, 1.08f), Quaternion.identity),
                    new SceneActorDto("PrevisCast_F_Magenta", new Vector3(2.82f, 0.05f, 2.07f), Quaternion.Euler(0, 180, 0))
                };
                dto.props = new[] { new ScenePropDto("envelope", new Vector3(0.85f, 0.05f, 2.42f)) };
                SceneEditStateStore.Save(dto);

                var back = SceneEditStateStore.Load(out var error);
                bool ok = error == null && back != null && back.actors.Length == 6 && back.props.Length == 1;
                if (ok)
                {
                    foreach (var src in dto.actors)
                    {
                        var a = back.actors.FirstOrDefault(x => x.id == src.id);
                        if (a == null || Vector3.Distance(a.position, src.position) >= 0.001f) ok = false;
                    }
                    if (back.props[0].propId != "envelope" ||
                        Vector3.Distance(back.props[0].position, dto.props[0].position) >= 0.001f) ok = false;
                }
                return ok;
            }
            finally
            {
                Environment.SetEnvironmentVariable("SHOTFIELD_SAVE_DIR", oldEnv);
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }
    }
}
#endif
