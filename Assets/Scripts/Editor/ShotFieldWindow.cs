#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 镜头工作台（编辑器内）。
    /// 这是 PC 端最小闭环的第一版：建镜头、继承、应用、跑约束检查。
    /// 不依赖 VR、不依赖 PICO SDK、不依赖模拟器。
    /// </summary>
    public class ShotFieldWindow : EditorWindow
    {
        private ShotLibrary library;
        private Vector2 scrollShots;
        private Vector2 scrollResult;
        private string newShotIntent = "";
        private bool inheritFromPrevious = true;
        private int selectedIndex = -1;
        private List<ConstraintResult> lastResults = new List<ConstraintResult>();
        private string lastCheckedShotId = "";
        private Vector2 scrollDetail;

        [MenuItem("镜场 ShotField/镜头工作台")]
        public static void Open()
        {
            var w = GetWindow<ShotFieldWindow>("镜场 · 镜头工作台");
            w.minSize = new Vector2(560f, 520f);
            w.Show();
        }

        private void OnEnable()
        {
            RescanLibrary();
        }

        private void RescanLibrary()
        {
            library = Object.FindAnyObjectByType<ShotLibrary>();
            if (library != null && library.ShotCamera == null)
            {
                var main = Camera.main;
                if (main != null) library.ShotCamera = main;
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            if (library == null)
            {
                EditorGUILayout.HelpBox(
                    "场景里还没有 ShotLibrary。\n\n" +
                    "1. 新建一个空物体，命名 ShotLibrary\n" +
                    "2. 挂上 ShotLibrary 组件，把主相机拖进 ShotCamera 字段",
                    MessageType.Warning);
                if (GUILayout.Button("在场景中自动创建")) AutoCreate();
                return;
            }

            DrawCreateSection();
            ShotPassportPanel.Draw(library, CurrentSelected(), FindObjects());
            EditorGUILayout.Space(6f);
            DrawShotList();
            EditorGUILayout.Space(6f);
            DrawCheckSection();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("镜头意图的约束与验收层", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("PC 端最小闭环 v0.1 —— 建镜头 / 继承 / 约束检查", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新扫描场景", GUILayout.Width(120f))) RescanLibrary();
                if (library != null)
                {
                    EditorGUILayout.LabelField(
                        string.Format("镜头数：{0}　标记对象：{1}　约束规则：{2}",
                            library.Count,
                            SemanticMarker.All().Length,
                            FindObjects().Length),
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.Space(4f);
        }

        private void DrawCreateSection()
        {
            EditorGUILayout.LabelField("新建镜头", EditorStyles.boldLabel);

            newShotIntent = EditorGUILayout.TextField("导演意图", newShotIntent);
            inheritFromPrevious = EditorGUILayout.Toggle("继承上一镜的场景（只换机位）", inheritFromPrevious);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从当前场景新建镜头", GUILayout.Height(24f)))
                {
                    var shot = library.CreateShot(inheritFromPrevious, newShotIntent, true);
                    selectedIndex = library.Count - 1;
                    newShotIntent = "";
                    EditorUtility.SetDirty(library);
                }

                if (GUILayout.Button("把当前机位写回选中镜头", GUILayout.Height(24f)))
                {
                    var shot = CurrentSelected();
                    if (shot != null)
                    {
                        library.UpdateCameraOf(shot);
                        EditorUtility.SetDirty(library);
                    }
                }
            }
        }

        private void DrawShotList()
        {
            EditorGUILayout.LabelField("镜头列表", EditorStyles.boldLabel);

            if (library.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有镜头。先摆好场景，点上面「从当前场景新建镜头」。", MessageType.Info);
                return;
            }

            scrollShots = EditorGUILayout.BeginScrollView(scrollShots, GUILayout.MaxHeight(190f));
            for (int i = 0; i < library.Count; i++)
            {
                var s = library.Shots[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isSel = selectedIndex == i;
                    if (GUILayout.Toggle(isSel, "", GUILayout.Width(16f)))
                    {
                        selectedIndex = i;
                    }

                    var label = string.Format("{0}  rev{1}  {2}  [{3}]",
                        s.shotId, s.revision, s.approval.ToString(),
                        string.IsNullOrEmpty(s.intent) ? "无意图描述" : Shorten(s.intent, 18));

                    if (GUILayout.Button(label, EditorStyles.label, GUILayout.ExpandWidth(true)))
                    {
                        selectedIndex = i;
                    }

                    if (GUILayout.Button("应用", GUILayout.Width(52f)))
                    {
                        library.ApplyShot(s);
                        SceneView.RepaintAll();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCheckSection()
        {
            EditorGUILayout.LabelField("约束检查", EditorStyles.boldLabel);

            var shot = CurrentSelected();
            if (shot == null)
            {
                EditorGUILayout.HelpBox("先在上面的列表里选一个镜头。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("当前镜头：" + shot.shotId, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查此镜头", GUILayout.Height(26f)))
                {
                    RunCheck(shot);
                }
                if (GUILayout.Button("应用并检查", GUILayout.Height(26f)))
                {
                    library.ApplyShot(shot);
                    SceneView.RepaintAll();
                    RunCheck(shot);
                }
            }

            if (lastCheckedShotId != shot.shotId)
            {
                EditorGUILayout.HelpBox("该镜头的检查结果还没跑。点「检查此镜头」。", MessageType.None);
                return;
            }

            DrawSummary(shot);

            scrollResult = EditorGUILayout.BeginScrollView(scrollResult, GUILayout.MaxHeight(150f));
            for (int i = 0; i < lastResults.Count; i++)
            {
                var r = lastResults[i];
                DrawResultRow(r);
            }
            EditorGUILayout.EndScrollView();

            DrawLockSection(shot);
        }

        private void DrawSummary(ShotData shot)
        {
            int pass = 0, vio = 0, pend = 0;
            for (int i = 0; i < lastResults.Count; i++)
            {
                if (lastResults[i].status == CheckStatus.Pass) pass++;
                else if (lastResults[i].status == CheckStatus.Violation) vio++;
                else pend++;
            }

            var color = vio > 0 ? new Color(0.85f, 0.19f, 0.15f) : (pend > 0 ? new Color(0.6f, 0.5f, 0.1f) : new Color(0.15f, 0.5f, 0.2f));
            var old = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(string.Format("通过 {0}　违规 {1}　待确认 {2}", pass, vio, pend), EditorStyles.boldLabel);
            GUI.color = old;

            if (vio > 0)
            {
                EditorGUILayout.HelpBox("存在违规项。按规则，此镜头不应进入审批。", MessageType.Error);
            }
            else if (pend > 0)
            {
                EditorGUILayout.HelpBox("有未确定项，需要人工补齐信息后再判定。", MessageType.Warning);
            }
        }

        private void DrawResultRow(ConstraintResult r)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string mark = r.status == CheckStatus.Pass ? "通过"
                            : (r.status == CheckStatus.Violation ? "违规" : "待确认");

                var color = r.status == CheckStatus.Pass ? new Color(0.15f, 0.5f, 0.2f)
                          : (r.status == CheckStatus.Violation ? new Color(0.85f, 0.19f, 0.15f)
                          : new Color(0.6f, 0.5f, 0.1f));

                var old = GUI.color;
                GUI.color = color;
                EditorGUILayout.LabelField(mark + "　" + r.ruleName, EditorStyles.boldLabel);
                GUI.color = old;

                if (!string.IsNullOrEmpty(r.detail))
                {
                    EditorGUILayout.LabelField(r.detail, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawLockSection(ShotData shot)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("锁定与审批", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("标记为已审批"))
                {
                    shot.approval = ApprovalStatus.Approved;
                    shot.revision++;
                    shot.updatedAt = System.DateTimeOffset.UtcNow.ToString("O");
                    EditorUtility.SetDirty(library);
                }
                if (GUILayout.Button("标记为锁定"))
                {
                    shot.approval = ApprovalStatus.Locked;
                    shot.revision++;
                    shot.updatedAt = System.DateTimeOffset.UtcNow.ToString("O");
                    EditorUtility.SetDirty(library);
                }
                if (GUILayout.Button("退回草稿"))
                {
                    shot.approval = ApprovalStatus.Draft;
                    shot.revision++;
                    shot.updatedAt = System.DateTimeOffset.UtcNow.ToString("O");
                    EditorUtility.SetDirty(library);
                }
            }
            EditorGUILayout.LabelField("意图：" + (string.IsNullOrEmpty(shot.intent) ? "（未填写）" : shot.intent), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(string.Format("版本 rev{0}　更新 {1}　by {2}", shot.revision, shot.updatedAt, shot.updatedBy), EditorStyles.miniLabel);
        }

        private void RunCheck(ShotData shot)
        {
            lastResults.Clear();
            lastCheckedShotId = shot.shotId;

            var all = FindObjects();
            if (all.Length == 0)
            {
                lastResults.Add(ConstraintResult.Pending("无规则", "场景里没有挂任何 ShotConstraint", ""));
                return;
            }

            // 上一镜：用于越轴比对
            int idx = library.IndexOf(shot);
            ShotData prev = (idx > 0) ? library.Shots[idx - 1] : null;

            for (int i = 0; i < all.Length; i++)
            {
                ConstraintResult r;
                try
                {
                    r = all[i].Evaluate(shot, prev);
                }
                catch (System.Exception e)
                {
                    r = ConstraintResult.Pending(all[i].ruleName, "规则执行异常：" + e.Message, "");
                }
                if (r == null) r = ConstraintResult.Pending(all[i].ruleName, "无返回结果", "");
                if (string.IsNullOrEmpty(r.ruleName)) r.ruleName = all[i].ruleName;
                lastResults.Add(r);
            }

            shot.lastCheck = new List<ConstraintResult>(lastResults);
            shot.checkedAt = System.DateTimeOffset.UtcNow.ToString("O");
            EditorUtility.SetDirty(library);
            SceneView.RepaintAll();
        }

        private ShotConstraint[] FindObjects()
        {
            return Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
        }

        private ShotData CurrentSelected()
        {
            if (library == null) return null;
            if (selectedIndex < 0 || selectedIndex >= library.Count) return null;
            return library.Shots[selectedIndex];
        }

        private void AutoCreate()
        {
            var go = new GameObject("ShotLibrary");
            library = go.AddComponent<ShotLibrary>();
            var cam = Camera.main;
            if (cam != null) library.ShotCamera = cam;
            Undo.RegisterCreatedObjectUndo(go, "创建 ShotLibrary");
            Selection.activeGameObject = go;
        }

        private string Shorten(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
#endif
