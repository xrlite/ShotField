#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShotField.EditorTools
{
    /// <summary>
    /// SC01_Final 定点打补丁的公共设施。铁律：
    /// 1) 只 patch 不重建 —— 绝不调用 Sc01Blockout.BuildAndSaveScene()；
    /// 2) 幂等 —— 重复运行结果一致；
    /// 3) 每支补丁结尾打印 SC01-XXX-RESULT=PASS/FAIL 供批处理判定。
    /// </summary>
    internal static class Sc01PatchCore
    {
        internal const string FinalPath = "Assets/Scenes/SC01_Final.unity";
        internal const string BlockoutPath = "Assets/Scenes/SC01_Blockout.unity";
        internal const string MatDir = "Assets/Models/Studio/Materials/";
        internal const string ArmatureMatDir =
            "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Materials/";

        private static int _pass;
        private static int _total;

        internal static bool AllPassed => _total > 0 && _pass == _total;
        internal static int Pass => _pass;
        internal static int Total => _total;

        internal static void Begin()
        {
            _pass = 0;
            _total = 0;
        }

        internal static void Check(bool ok, string label, string detail = null)
        {
            _total++;
            if (ok) { _pass++; return; }
            Debug.LogError("[SC01-PATCH] FAIL " + label +
                           (string.IsNullOrEmpty(detail) ? string.Empty : " | " + detail));
        }

        internal static bool OpenFinal()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == BlockoutPath)
            {
                Debug.LogError("[SC01-PATCH] 拒绝在 SC01_Blockout.unity 上打补丁（那是白模重建场景）。");
                return false;
            }
            if (active.path != FinalPath)
                active = EditorSceneManager.OpenScene(FinalPath, OpenSceneMode.Single);
            if (active.path != FinalPath)
                Debug.LogError("[SC01-PATCH] 无法打开 " + FinalPath + "，当前=" + active.path);
            return active.path == FinalPath;
        }

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
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var hit = FindUnder(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        internal static bool SetActive(string name, bool active, string label)
        {
            var t = FindDeep(name);
            if (t == null) { Check(false, label, "找不到 " + name); return false; }
            if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
            Check(t.gameObject.activeSelf == active, label, name + " 期望 active=" + active);
            return true;
        }

        internal static Material LoadMat(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Material>(MatDir + fileName + ".mat");

        internal static Material LoadArmatureMat(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Material>(ArmatureMatDir + fileName + ".mat");

        internal static bool Paint(Transform target, Material mat, string label)
        {
            if (target == null) { Check(false, label, "对象为空"); return false; }
            if (mat == null) { Check(false, label, "材质为空"); return false; }
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) { Check(false, label, target.name + " 无 MeshRenderer"); return false; }
            var slots = renderer.sharedMaterials.Length;
            if (slots == 0) { Check(false, label, target.name + " 无材质槽"); return false; }
            var arr = new Material[slots];
            for (var i = 0; i < slots; i++) arr[i] = mat;
            renderer.sharedMaterials = arr;
            var done = renderer.sharedMaterials[0] == mat;
            Check(done, label, target.name);
            return done;
        }

        internal static bool PaintNamed(string name, Material mat, string label) =>
            Paint(FindDeep(name), mat, label);

        internal static void Commit()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FinalPath))
                throw new IOException("保存失败: " + FinalPath);
            Debug.Log("[SC01-PATCH] SAVED=" + FinalPath + " bytes=" +
                      new FileInfo(Path.GetFullPath(FinalPath)).Length);
        }

        internal static string Backup(string tag)
        {
            var source = Path.GetFullPath(FinalPath);
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, "SC01_Before" + tag + "_" +
                                      DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            File.Copy(source, target, false);
            Debug.Log("[SC01-PATCH] BACKUP=" + target + " bytes=" + new FileInfo(target).Length);
            return target;
        }

        internal static void Finish(string token)
        {
            var line = "SC01-" + token + "-RESULT=" + (AllPassed ? "PASS" : "FAIL") +
                       " (" + _pass + "/" + _total + ")";
            if (AllPassed) Debug.Log(line); else Debug.LogError(line);
            if (Application.isBatchMode) EditorApplication.Exit(AllPassed ? 0 : 1);
        }

        internal static bool Near(Color a, Color b, float eps = 0.005f) =>
            Mathf.Abs(a.r - b.r) < eps && Mathf.Abs(a.g - b.g) < eps && Mathf.Abs(a.b - b.b) < eps;

        internal static bool Near(Vector3 a, Vector3 b, float eps = 0.002f) =>
            (a - b).sqrMagnitude < eps * eps;
    }
}
#endif
