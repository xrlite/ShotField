#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.Core;

namespace ShotField.EditorTools
{
    internal sealed class ShotListPanel
    {
        private Vector2 scrollShots;

        internal int Draw(ShotLibrary library, int selectedIndex)
        {
            EditorGUILayout.LabelField("镜头列表", EditorStyles.boldLabel);

            if (library.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有镜头。先摆好场景，点上面「从当前场景新建镜头」。", MessageType.Info);
                return selectedIndex;
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
                        ShotSceneActions.Apply(library, s);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            return selectedIndex;
        }

        private string Shorten(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
#endif
