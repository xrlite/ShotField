using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public enum SceneCaptureMode { Current, InheritLatestOrCapture, Empty }

    /// <summary>
    /// 镜头库：管理镜头列表、快照捕获与镜头继承。
    /// 关键设计：每个 ShotData 持有自己的一份 SceneSnapshot 深拷贝；
    /// 应用某个镜头时，是把快照刷到场景里，而不是让场景和镜头指向同一块内存。
    /// </summary>
    public partial class ShotLibrary : MonoBehaviour
    {
        [SerializeField] private List<ShotData> shots = new List<ShotData>();
        // 署名默认值不能写具体人名：这是公开仓库，且 Inspector 会把它当成默认作者。
        // 真实署名由调用方在保存镜头时填入（ShotPassport 的 updatedBy）。
        [SerializeField] private string defaultUpdatedBy = "unknown";
        [SerializeField] private string sceneId = "SC01";

        public string SceneId => sceneId;
        public Camera ShotCamera;

        public IReadOnlyList<ShotData> Shots => shots;
        public int Count => shots.Count;
        public ShotData Latest => shots.Count > 0 ? shots[shots.Count - 1] : null;

        public void AddLoaded(ShotData shot)
        {
            if (!shot.shotId.StartsWith(sceneId + "-", StringComparison.Ordinal))
                throw new InvalidOperationException("护照不属于当前场次。");
            var shortId = shot.shotId.Substring(sceneId.Length + 1);
            if (Find(shot.shotId) != null || Find(shortId) != null)
                throw new InvalidOperationException("镜头 ID 已存在：" + shot.shotId);
            shots.Add(shot);
            RecordUndo(shot);
            MarkDirty();
        }

        public ShotData Find(string shotId)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].shotId == shotId) return shots[i];
            }
            return null;
        }

        public int IndexOf(ShotData shot) => shots.IndexOf(shot);

        public bool RemoveShot(string shotId)
        {
            var s = Find(shotId);
            if (s == null) return false;
            shots.Remove(s);
            return true;
        }

        public void ClearAll() => shots.Clear();
    }
}
