using System;
using System.IO;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public enum LaunchEntryMode
    {
        None = 0,
        NewIdea = 1,
        DemoCase = 2,
        ContinueProject = 3
    }

    /// <summary>
    /// 欢迎页三入口与通用项目状态管理：
    /// 负责识别非室内题材、检测本地已保存项目/护照，以及恢复最近项目进度。
    /// </summary>
    public static class WelcomeHubProjectState
    {
        private const string HasSavedKey = "ShotField.HasSavedProject.v1";
        private const string LastScriptKey = "ShotField.LastScript.v1";
        private const string NonInteriorKey = "ShotField.NonInterior.v1";

        public const string LetterDemoTitle = "《桌上的信》";
        public const string LetterDemoScript =
            "《桌上的信》\n第1场 内景 客厅 日\n桌上放着一封未拆开的信，茶几横在中央。\n老张：这封信你看了吗？\n小李：还没，我不敢看。";

        private static readonly string[] NonInteriorKeywords =
        {
            "外景", "科幻", "太空", "飞船", "星际", "仙侠", "御剑", "山巅", "古风",
            "追车", "街头", "城市", "荒野", "废土", "赛道", "户外", "广场",
            "sci-fi", "space", "city", "street", "outdoor", "xianxia", "chase"
        };

        public static LaunchEntryMode CurrentMode { get; private set; } = LaunchEntryMode.None;
        public static string PendingScriptText { get; private set; } = string.Empty;
        public static bool IsNonInteriorScope { get; private set; }

        public static bool HasSavedProject()
        {
            if (PlayerPrefs.GetInt(HasSavedKey, 0) == 1) return true;
            var lib = UnityEngine.Object.FindAnyObjectByType<ShotLibrary>();
            if (lib != null && lib.Count > 0) return true;
            return CountPassportFiles(ShotPassportStore.ProjectFolder) > 0;
        }

        public static void BeginNewIdea(string starterScript = "", bool nonInterior = false)
        {
            CurrentMode = LaunchEntryMode.NewIdea;
            PendingScriptText = starterScript ?? string.Empty;
            IsNonInteriorScope = nonInterior || DetectNonInteriorScope(PendingScriptText);
            TabletopSetComposer.SetNonInteriorScope(IsNonInteriorScope);
        }

        public static void BeginLetterDemoCase()
        {
            CurrentMode = LaunchEntryMode.DemoCase;
            PendingScriptText = LetterDemoScript;
            IsNonInteriorScope = false;
            TabletopSetComposer.SetNonInteriorScope(false);
        }

        public static bool DetectNonInteriorScope(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return false;
            if (rawText.Contains("内景") && rawText.Contains("客厅")) return false;
            foreach (var kw in NonInteriorKeywords)
            {
                if (rawText.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static void SaveProjectMetadata(string scriptText, bool nonInterior)
        {
            IsNonInteriorScope = nonInterior;
            PlayerPrefs.SetInt(HasSavedKey, 1);
            if (!string.IsNullOrWhiteSpace(scriptText))
                PlayerPrefs.SetString(LastScriptKey, scriptText.Trim());
            PlayerPrefs.SetInt(NonInteriorKey, nonInterior ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool TryRestoreSavedProject(out string status)
        {
            if (!HasSavedProject())
            {
                status = "暂无已保存的项目或分镜护照，请先选择「我有一个创意」或「从案例开始」。";
                return false;
            }

            CurrentMode = LaunchEntryMode.ContinueProject;
            string savedScript = PlayerPrefs.GetString(LastScriptKey, string.Empty);
            bool nonInterior = PlayerPrefs.GetInt(NonInteriorKey, 0) == 1;
            PendingScriptText = savedScript;
            IsNonInteriorScope = nonInterior;
            TabletopSetComposer.SetNonInteriorScope(nonInterior);

            int restoredShots = RestorePassportsIntoLibrary();
            if (restoredShots > 0 || !string.IsNullOrEmpty(savedScript))
            {
                status = restoredShots > 0
                    ? $"已恢复项目：{restoredShots} 个镜头护照{(string.IsNullOrEmpty(savedScript) ? "" : "及上次剧本")}"
                    : "已恢复上次保存的剧本与空间设置";
                return true;
            }

            var lib = UnityEngine.Object.FindAnyObjectByType<ShotLibrary>();
            if (lib != null && lib.Count > 0)
            {
                status = $"已恢复当前场次项目：{lib.Count} 个镜头";
                return true;
            }

            status = "未找到可恢复的本地项目数据，请从「我有一个创意」或「从案例开始」进入。";
            return false;
        }

        private static int CountPassportFiles(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return 0;
            try
            {
                return Directory.GetFiles(folder, "rev*.json", SearchOption.AllDirectories).Length;
            }
            catch (Exception) { return 0; }
        }

        private static int RestorePassportsIntoLibrary()
        {
            var lib = UnityEngine.Object.FindAnyObjectByType<ShotLibrary>();
            if (lib == null) return 0;
            int loaded = 0;
            loaded += LoadFromFolder(lib, ShotPassportStore.ProjectFolder);
            loaded += LoadFromFolder(lib, Path.Combine(ShotPassportStore.ProjectFolder, "AutoSave"));
            if (lib.Latest != null) lib.ApplyShot(lib.Latest);
            return loaded;
        }

        private static int LoadFromFolder(ShotLibrary lib, string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return 0;
            int count = 0;
            foreach (var dir in Directory.GetDirectories(folder))
            {
                string id = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(id) || id == "AutoSave") continue;
                try
                {
                    var dto = ShotPassportStore.LoadLatest(id, folder);
                    if (dto == null) continue;
                    var shot = dto.ToShot();
                    if (lib.Find(shot.shotId) != null) continue;
                    lib.AddLoaded(shot);
                    count++;
                }
                catch (Exception) { /* 跳过不兼容或非当前场次的护照文件 */ }
            }
            return count;
        }
    }
}
