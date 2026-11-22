#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotField.EditorTools
{
    internal static class HierarchySearch
    {
        internal static Transform FindDeep(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                var hit = FindUnder(root.transform, name);
                if (hit != null) return hit;
            }
            return null;
        }

        internal static Transform FindUnder(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
                var hit = FindUnder(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
#endif
