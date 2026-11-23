#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 单构建入口断言：构建场景表恰好 1 个启用条目，路径与 BuildAndroid.ScenePath 一致，
    /// 且该场景资产在工程内真实存在。防止第二场景混入构建入口。
    /// </summary>
    public static class SceneSingleEntryVerifier
    {
        [MenuItem("ShotField/Verifiers/Run Scene Single Entry Verifier")]
        public static void Run()
        {
            int passed = 0;

            var scenes = EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0];
            var enabled = scenes.Where(s => s != null && s.enabled).ToArray();

            VerifyAssert.Require("构建场景表恰好 1 个启用条目", enabled.Length == 1, ref passed);

            VerifyAssert.Require("唯一启用条目路径与 BuildAndroid.ScenePath 一致",
                enabled.Length == 1 && enabled[0].path == BuildAndroid.ScenePath, ref passed);

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BuildAndroid.ScenePath);
            VerifyAssert.Require("ScenePath 指向的场景资产在工程内真实存在", asset != null, ref passed);

            VerifyAssert.Finish("SCENE-SINGLE-ENTRY-RESULT", passed, 3);
        }
    }
}
#endif
