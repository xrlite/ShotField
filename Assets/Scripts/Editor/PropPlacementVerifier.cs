#if UNITY_EDITOR
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 道具放置交互的静态接线校验。
    /// 只验证「组件装到位、引用非空、库里有可放置条目」，不验证运行时 Ghost 外观与射线手感
    /// （后者属 pending-真机 / 需模拟器人工目视，不进自动断言）。
    /// </summary>
    public static class PropPlacementVerifier
    {
        private const int ExpectedAssertions = 6;
        private const string ScenePath = "Assets/Scenes/SC01_Blockout.unity";

        public static void Run()
        {
            int passed = 0;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            VerifyAssert.Require("SC01 场景可打开", scene.IsValid(), ref passed);

            var placement = Object.FindAnyObjectByType<PropPlacementInteractor>();
            VerifyAssert.Require("场景存在 PropPlacementInteractor", placement != null, ref passed);

            VerifyAssert.Require("放置器持有 XRRayInteractor 引用",
                placement != null && placement.rayInteractor != null, ref passed);

            VerifyAssert.Require("放置器持有 PropBrowserUI 引用",
                placement != null && placement.browser != null, ref passed);

            var browser = Object.FindAnyObjectByType<PropBrowserUI>();
            VerifyAssert.Require("PropBrowserUI 至少有 1 条 Prefab 绑定",
                browser != null && browser.BindingCount >= 1, ref passed);

            int placeable = 0;
            for (int i = 0; i < PropLibrary.Count; i++)
            {
                var e = PropLibrary.All[i];
                if (e != null && !string.IsNullOrEmpty(e.prefabPath)) placeable++;
            }
            VerifyAssert.Require("PropLibrary 可放置条目 >= 5（当前 " + placeable + "）",
                placeable >= 5, ref passed);

            VerifyAssert.Finish("PROP-PLACEMENT-RESULT", passed, ExpectedAssertions);
        }
    }
}
#endif
