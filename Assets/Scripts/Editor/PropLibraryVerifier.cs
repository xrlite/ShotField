#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M2 道具库验收：数据层条目完整、路径可加载、分类与语义可查。
    /// 分母用 ExpectedAssertions，不硬编码；计数不符即 FAIL。
    /// </summary>
    public static class PropLibraryVerifier
    {
        // 12 条真实条目 + 6 条结构性断言 = 10 条（其中 1 条聚合覆盖 12 条路径）
        private const int ExpectedAssertions = 10;

        [MenuItem("镜场 ShotField/验证/验证道具库 PropLibrary")]
        public static void Run()
        {
            int passCount = 0;
            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[PropLibraryVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[PropLibraryVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            var all = PropLibrary.All;

            Assert("条目总数为 12", PropLibrary.Count == 12);

            var ids = new HashSet<string>();
            bool idUnique = true;
            for (int i = 0; i < all.Count; i++)
            {
                if (!ids.Add(all[i].id)) idUnique = false;
            }
            Assert("全部 id 唯一无重复", idUnique);

            bool allLoadable = true;
            int loadableCount = 0;
            var missing = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                string p = all[i].prefabPath;
                if (string.IsNullOrEmpty(p)) { allLoadable = false; missing.Add(all[i].id + "(空路径)"); continue; }
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (asset == null) { allLoadable = false; missing.Add(all[i].id + "(" + p + ")"); }
                else loadableCount++;
            }
            if (!allLoadable) Debug.LogError("[PropLibraryVerifier] 加载失败项: " + string.Join(", ", missing));
            Assert($"全部 {all.Count} 条 prefabPath 经 AssetDatabase 可加载（实际 {loadableCount}）", allLoadable && loadableCount == all.Count);

            var paths = new HashSet<string>();
            bool pathUnique = true;
            for (int i = 0; i < all.Count; i++)
            {
                if (!paths.Add(all[i].prefabPath)) pathUnique = false;
            }
            Assert("prefabPath 无重复登记", pathUnique);

            Assert("有资产的分类数 >= 4", PropLibrary.NonEmptyCategoryCount() >= 4);

            Assert("家具分类条目数为 8", PropLibrary.ByCategory(PropCategory.Furniture).Count == 8);

            var empty = PropLibrary.ByCategory(PropCategory.Vehicle);
            Assert("空分类返回空列表而非 null", empty != null && empty.Count == 0);

            var envelope = PropLibrary.Find("envelope");
            Assert("envelope 命中且语义为 KeyProp",
                envelope != null && envelope.semanticKind == SemanticMarker.Kind.KeyProp);

            var actor = PropLibrary.Find("actorA");
            Assert("actorA 命中且语义为 ActorPrimary",
                actor != null && actor.semanticKind == SemanticMarker.Kind.ActorPrimary);

            Assert("全部条目均有真实资产（无程序化占位）",
                PropLibrary.CountWithPrefab() == PropLibrary.Count);

            if (passCount != ExpectedAssertions)
            {
                Debug.LogError($"PROP-LIBRARY-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"PROP-LIBRARY-RESULT=PASS ({passCount}/{ExpectedAssertions})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
