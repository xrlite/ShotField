using System;
using System.Linq;
using ShotField.Constraints;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class ShotPassportPanel
    {
        public static void Draw(ShotLibrary library, ShotData selected, ShotConstraint[] rules)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected == null))
                {
                    if (GUILayout.Button("保存护照"))
                        Try(() => Save(library, selected, rules));
                }
                if (GUILayout.Button("加载护照"))
                    Try(() => Load(library, rules));
            }
        }

        private static void Save(ShotLibrary library, ShotData shot, ShotConstraint[] rules)
        {
            if (rules.Length == 0) throw new InvalidOperationException("当前场景没有可保存的约束。");
            var dto = ShotPassportMapper.FromShot(shot, library.SceneId, Specs(rules));
            var path = ShotPassportStore.Save(dto, ShotPassportStore.ProjectFolder);
            EditorUtility.DisplayDialog("已保存护照", path, "确定");
        }

        private static void Load(ShotLibrary library, ShotConstraint[] rules)
        {
            var path = EditorUtility.OpenFilePanel("加载镜头护照", ShotPassportStore.ProjectFolder, "json");
            if (string.IsNullOrEmpty(path)) return;
            var passport = ShotPassportStore.Load(path);
            var recorded = passport.constraints;
            if (recorded == null) throw new FormatException("护照缺少约束列表。");
            var current = Specs(rules);
            if (!recorded.Select(item => JsonUtility.ToJson(item)).OrderBy(item => item, StringComparer.Ordinal)
                .SequenceEqual(current.Select(item => JsonUtility.ToJson(item)).OrderBy(item => item, StringComparer.Ordinal)))
                EditorUtility.DisplayDialog("约束已变更", "当前场景与护照中的规则不同。加载保留原护照与当前场景规则。", "确定");
            Undo.RecordObject(library, "加载镜头护照");
            library.AddLoaded(passport.ToShot());
            EditorUtility.SetDirty(library);
        }

        internal static ConstraintSpec[] Specs(ShotConstraint[] rules) =>
            Array.ConvertAll(rules, rule => rule.ToSpec());

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception error) { EditorUtility.DisplayDialog("护照操作失败", error.Message, "确定"); }
        }
    }
}
