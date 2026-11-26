using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ShotField.Core;
using UnityEngine;

namespace ShotField.Tests.EditMode
{
    internal static class E2ETestHelpers
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string AssetFullPath(string relativePath) =>
            Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public static string ReadProjectText(string relativePath)
        {
            string full = AssetFullPath(relativePath);
            if (!File.Exists(full))
                throw new FileNotFoundException("Expected project file missing: " + relativePath, full);
            return File.ReadAllText(full);
        }

        public static Type FindTypeByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t != null && (t.Name == typeName || t.FullName == typeName))
                        return t;
                }
            }
            return null;
        }

        public static int CountMatches(string text, string pattern, RegexOptions opts = RegexOptions.None) =>
            string.IsNullOrEmpty(text) ? 0 : Regex.Matches(text, pattern, opts).Count;

        public static string ExtractYamlValue(string yaml, string key)
        {
            var m = Regex.Match(yaml, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.+?)\s*$");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        public static string ExtractYamlSection(string text, string marker)
        {
            int idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            int nextDoc = text.IndexOf("--- !u!", idx + marker.Length, StringComparison.Ordinal);
            return nextDoc > idx ? text.Substring(idx, nextDoc - idx) : text.Substring(idx);
        }

        public static ShotData CreateShot(string id, Vector3 camPos, Vector3 lookAt, string actorId, Vector3 actorPos)
        {
            var dir = (lookAt - camPos).sqrMagnitude > 0.0001f ? (lookAt - camPos).normalized : Vector3.forward;
            var shot = new ShotData
            {
                shotId = id,
                camera = new ShotCameraState
                {
                    position = camPos,
                    rotation = Quaternion.LookRotation(dir, Vector3.up),
                    usesUnityCameraRotation = true,
                    fieldOfView = 50f,
                    aspect = ShotCameraState.DefaultAspect,
                    nearClip = 0.05f,
                    farClip = 100f
                }
            };
            if (!string.IsNullOrEmpty(actorId))
            {
                shot.snapshot.actors.Add(new ActorState
                {
                    actorId = actorId,
                    position = actorPos,
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    active = true
                });
            }
            return shot;
        }

        public static void DestroyAllTracked(List<UnityEngine.Object> created)
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }
    }
}