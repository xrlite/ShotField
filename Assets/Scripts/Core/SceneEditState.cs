using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 一位演员在编辑快照里的站位与朝向。id 取场景内 GameObject 名，重载时按名匹配。
    /// </summary>
    [Serializable]
    public class SceneActorDto
    {
        public string id;
        public Vector3 position;
        public Vector3 eulerAngles;

        public SceneActorDto() { }

        public SceneActorDto(string id, Vector3 position, Quaternion rotation)
        {
            this.id = id;
            this.position = position;
            this.eulerAngles = rotation.eulerAngles;
        }
    }

    /// <summary>一件本轮新增道具的登记：propId 对应 PropLibrary 条目，position 为落地点。</summary>
    [Serializable]
    public class ScenePropDto
    {
        public string propId;
        public Vector3 position;

        public ScenePropDto() { }

        public ScenePropDto(string propId, Vector3 position)
        {
            this.propId = propId;
            this.position = position;
        }
    }

    /// <summary>
    /// 场景编辑快照：演员站位 + 新增道具。纯数据 DTO，不碰场景、不引用 RuntimeUI/Editor。
    /// </summary>
    [Serializable]
    public class SceneEditStateDto
    {
        public string savedUtc;
        public SceneActorDto[] actors = Array.Empty<SceneActorDto>();
        public ScenePropDto[] props = Array.Empty<ScenePropDto>();
    }

    /// <summary>
    /// 场景编辑快照落盘/读取。路径解析顺序：环境变量 SHOTFIELD_SAVE_DIR →
    /// Editor 用 Application.dataPath/../Saves → 播放器用 persistentDataPath/Saves。
    /// 文件名固定 scene_edit.json。
    /// </summary>
    public static class SceneEditStateStore
    {
        public const string FileName = "scene_edit.json";

        public static string ResolveFolder()
        {
            var env = Environment.GetEnvironmentVariable("SHOTFIELD_SAVE_DIR");
            if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
            return Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Saves"))
                : Path.Combine(Application.persistentDataPath, "Saves");
        }

        public static string ResolvePath() => Path.Combine(ResolveFolder(), FileName);

        public static bool Exists() => File.Exists(ResolvePath());

        /// <summary>写入快照并返回绝对路径；目录不存在时自动创建。</summary>
        public static string Save(SceneEditStateDto state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.savedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'");
            Directory.CreateDirectory(ResolveFolder());
            var path = ResolvePath();
            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        /// <summary>读取存档；不存在或解析失败时返回 null，error 写明原因。</summary>
        public static SceneEditStateDto Load(out string error)
        {
            error = null;
            var path = ResolvePath();
            if (!File.Exists(path)) { error = "存档不存在: " + path; return null; }
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var dto = JsonUtility.FromJson<SceneEditStateDto>(json);
                if (dto == null) { error = "存档 JSON 为空: " + path; return null; }
                if (dto.actors == null) dto.actors = Array.Empty<SceneActorDto>();
                if (dto.props == null) dto.props = Array.Empty<ScenePropDto>();
                return dto;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }
}
