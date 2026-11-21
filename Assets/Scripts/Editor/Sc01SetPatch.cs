#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// B1 几何：把"影棚里塞了一个封闭民宅盒子"拆成半开放布景。
    /// 保留右墙 + 后墙（4 块窗墙）+ 带门洞的入口墙；关掉左墙、左踢脚线、天花板，
    /// 让 4.02 米桁架与影棚纵深透进来；同时给 -3.00 处 0.10 米墙断面加三件收边框。
    /// </summary>
    internal static class Sc01SetPatch
    {
        private const string EdgeRootName = "STUDIO_OpenEdge";
        private const float OpeningX = -3.00f;

        [MenuItem("ShotField/SC01/Patch B1 半开放布景")]
        public static void Run()
        {
            Sc01PatchCore.Begin();
            if (Sc01PatchCore.OpenFinal())
            {
                Sc01PatchCore.Backup("OpenSet");
                Apply();
                Sc01PatchCore.Commit();
            }
            Sc01PatchCore.Finish("OPENSET");
        }

        internal static bool Apply()
        {
            Sc01PatchCore.SetActive("LeftWall", false, "B1.1 左墙关闭");
            Sc01PatchCore.SetActive("LeftBaseboard", false, "B1.2 左踢脚线关闭");
            Sc01PatchCore.SetActive("Ceiling", false, "B1.3 天花板关闭");
            Sc01PatchCore.SetActive("STUDIO_Truss", true, "B1.4 桁架启用");
            Sc01PatchCore.SetActive("WindowMarker", false, "B1.5 窗洞封板关闭");
            BuildEdge();
            return Sc01PatchCore.AllPassed;
        }

        private static void BuildEdge()
        {
            var root = Sc01PatchCore.FindDeep(EdgeRootName);
            if (root == null)
            {
                var go = new GameObject(EdgeRootName);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                root = go.transform;
            }
            Sc01PatchCore.Check(root != null, "B1.6 收边框根节点存在",
                "根节点=" + (root == null ? "null" : root.name));

            Box(root, "SETW_OpenCapTop", new Vector3(OpeningX, 2.84f, 2.00f),
                new Vector3(0.18f, 0.08f, 4.00f));
            Box(root, "SETW_OpenPostFront", new Vector3(OpeningX, 1.40f, 0.00f),
                new Vector3(0.18f, 2.80f, 0.18f));
            Box(root, "SETW_OpenPostBack", new Vector3(OpeningX, 1.40f, 4.00f),
                new Vector3(0.18f, 2.80f, 0.18f));
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            Sc01PatchCore.Check(
                Sc01PatchCore.Near(go.transform.localPosition, position) &&
                Sc01PatchCore.Near(go.transform.localScale, size, 0.005f),
                "B1.7 收边件 " + name,
                "pos=" + go.transform.localPosition + " scale=" + go.transform.localScale);
        }
    }
}
#endif
