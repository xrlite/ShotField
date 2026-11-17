using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ShotField.Core
{
    public static class ShotPassportStore
    {
        public static string ProjectFolder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Passports"))
            : Path.Combine(Application.persistentDataPath, "Passports");

        public static string Save(ShotPassportDto passport, string folder)
        {
            if (passport == null || passport.revision < 1) throw new ArgumentException("护照版本无效。");
            ValidateId(passport.shot_id);
            var directory = Path.Combine(folder, passport.shot_id);
            var path = Path.Combine(directory, $"rev{passport.revision:D3}.json");
            if (File.Exists(path))
            {
                throw new InvalidOperationException($"版本 {Path.GetFileName(path)} 已存在，历史护照不可覆写。请增加 revision。");
            }
            Directory.CreateDirectory(directory);
            var json = JsonUtility.ToJson(passport, true);
            var staging = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(staging, json, new UTF8Encoding(false));
            File.Move(staging, path);
            return path;
        }

        public static string SaveNextRevision(ShotPassportDto passport, string folder)
        {
            if (passport == null || passport.revision < 1) throw new ArgumentException("护照版本无效。");
            ValidateId(passport.shot_id);
            var directory = Path.Combine(folder, passport.shot_id);
            var revision = passport.revision;
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "rev*.json"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (int.TryParse(name.AsSpan(3), out var existing) && existing >= revision)
                        revision = checked(existing + 1);
                }
            }
            var original = passport.revision;
            passport.revision = revision;
            try { return Save(passport, folder); }
            catch
            {
                passport.revision = original;
                throw;
            }
        }

        public static ShotPassportDto Load(string path)
        {
            var passport = JsonUtility.FromJson<ShotPassportDto>(File.ReadAllText(path));
            if (passport == null) throw new FormatException("护照 JSON 为空。");
            ValidateId(passport.shot_id);
            if (Path.GetFileName(path) != $"rev{passport.revision:D3}.json")
                throw new FormatException("文件名和护照 revision 不一致。");
            return passport;
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"\ASC[0-9]+-S[0-9]+\z"))
                throw new FormatException("镜头 ID 应类似 SC01-S02。");
        }
    }
}
