#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    internal sealed class ShotCheckPanel
    {
        private Vector2 scrollResult;

        internal void Draw(ShotLibrary library, ShotData shot, ShotConstraint[] rules)
        {
            EditorGUILayout.LabelField("约束检查", EditorStyles.boldLabel);

            if (shot == null)
            {
                EditorGUILayout.HelpBox("先在上面的列表里选一个镜头。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("当前镜头：" + shot.shotId, EditorStyles.miniLabel);

            if (GUILayout.Button("应用并检查", GUILayout.Height(26f)))
            {
                ShotSceneActions.Apply(library, shot);
                RunCheck(library, shot, rules);
            }

            if (string.IsNullOrEmpty(shot.checkedAt))
            {
                EditorGUILayout.HelpBox("该镜头尚无检查结果。点「应用并检查」。", MessageType.None);
                return;
            }

            DrawSummary(shot.lastCheck);

            scrollResult = EditorGUILayout.BeginScrollView(scrollResult, GUILayout.MaxHeight(150f));
            for (int i = 0; i < shot.lastCheck.Count; i++)
            {
                var r = shot.lastCheck[i];
                DrawResultRow(r);
            }
            EditorGUILayout.EndScrollView();

            DrawLockSection(library, shot);
        }

        private void DrawSummary(List<ConstraintResult> lastResults)
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

        private void DrawLockSection(ShotLibrary library, ShotData shot)
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


        private static void RunCheck(ShotLibrary library, ShotData shot, ShotConstraint[] rules)
        {
            var results = new List<ConstraintResult>();
            if (rules.Length == 0)
                results.Add(ConstraintResult.Pending("无规则", "场景里没有挂任何 ShotConstraint", ""));

            int index = library.IndexOf(shot);
            var previous = index > 0 ? library.Shots[index - 1] : null;
            foreach (var rule in rules)
            {
                ConstraintResult result;
                try
                {
                    result = rule.Evaluate(shot, previous);
                }
                catch (System.Exception error)
                {
                    result = ConstraintResult.Pending(rule.ruleName, "规则执行异常：" + error.Message, "");
                }
                result ??= ConstraintResult.Pending(rule.ruleName, "无返回结果", "");
                if (string.IsNullOrEmpty(result.ruleName)) result.ruleName = rule.ruleName;
                results.Add(result);
            }

            shot.lastCheck = results;
            shot.checkedAt = System.DateTimeOffset.UtcNow.ToString("O");
            EditorUtility.SetDirty(library);
            SceneView.RepaintAll();
        }
    }
}
#endif
