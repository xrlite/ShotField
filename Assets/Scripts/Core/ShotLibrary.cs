using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    public enum SceneCaptureMode { Current, InheritLatestOrCapture, Empty }
    /// <summary>
    /// 镜头库：管理镜头列表、快照捕获与镜头继承。
    ///
    /// 关键设计（对应"不能让多个镜头共享一份可变场景数据"）：
    /// 每个 ShotData 持有自己的一份 SceneSnapshot 深拷贝；
    /// 应用某个镜头时，是把那份快照"刷"到场景里，而不是让场景和镜头指向同一块内存。
    /// </summary>
    public partial class ShotLibrary : MonoBehaviour
    {
        [SerializeField] private List<ShotData> shots = new List<ShotData>();
        [SerializeField] private string defaultUpdatedBy = "yangl";

        [SerializeField] private string sceneId = "SC01";
        public string SceneId => sceneId;
        public Camera ShotCamera;

        // Reject collisions, including legacy short IDs. A load never replaces a scene shot.
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

        public IReadOnlyList<ShotData> Shots { get { return shots; } }
        public int Count { get { return shots.Count; } }

        public ShotData Latest
        {
            get { return shots.Count > 0 ? shots[shots.Count - 1] : null; }
        }

        // ---------- 捕获 ----------

        /// <summary>把当前场景状态捕获成一份快照。</summary>
        public SceneSnapshot CaptureSnapshot()
        {
            var snap = new SceneSnapshot();
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m.gameObject.scene != gameObject.scene) continue;
                snap.actors.Add(new ActorState
                {
                    actorId = m.markerId,
                    position = m.transform.position,
                    rotation = m.transform.rotation,
                    scale = m.transform.localScale,
                    active = m.gameObject.activeSelf
                });
            }
            return snap;
        }

        /// <summary>把当前相机状态捕获成机位。</summary>
        public ShotCameraState CaptureCamera()
        {
            var cam = ShotCamera != null ? ShotCamera : Camera.main;
            return ShotCameraState.FromCamera(cam);
        }

        // ---------- 应用 ----------

        /// <summary>把一份快照刷到场景上（这是唯一允许的"应用"入口）。</summary>
        public void ApplySnapshot(SceneSnapshot snap)
        {
            if (snap == null) return;
            var markers = SemanticMarker.All(FindObjectsInactive.Include);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m.gameObject.scene != gameObject.scene) continue;
                var st = snap.Find(m.markerId);
                if (st == null) continue;
                m.transform.position = st.position;
                m.transform.rotation = st.rotation;
                m.transform.localScale = st.scale;
                m.gameObject.SetActive(st.active);
            }
            Physics.SyncTransforms();
        }

        public void ApplyShot(ShotData shot)
        {
            if (shot == null) return;
            ApplySnapshot(shot.snapshot);
            var cam = ShotCamera != null ? ShotCamera : Camera.main;
            shot.camera.ApplyTo(cam);
        }

        // ---------- 新建 / 继承 ----------

        /// <summary>
        /// 新建镜头。
        /// 继承时深拷贝上一镜；首镜没有可继承对象时捕获当前场景。
        /// </summary>
        public ShotData CreateShot(SceneCaptureMode captureMode, string intent)
        {
            var shot = new ShotData();
            shot.shotId = NextShotId();
            shot.revision = 1;
            shot.updatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("O");
            shot.updatedBy = defaultUpdatedBy;
            shot.intent = intent ?? "";

            shot.snapshot = captureMode switch
            {
                SceneCaptureMode.InheritLatestOrCapture when Latest != null => Latest.snapshot.Clone(),
                SceneCaptureMode.Empty => new SceneSnapshot(),
                _ => CaptureSnapshot()
            };

            shot.camera = CaptureCamera();
            shots.Add(shot);
            RecordUndo(shot);
            MarkDirty();
            return shot;
        }

        /// <summary>把当前机位写回某个镜头的独立机位状态（不动场景快照）。</summary>
        public void UpdateCameraOf(ShotData shot)
        {
            RecordUndo(shot);
            shot.camera = CaptureCamera();
            shot.revision += 1;
            shot.lastCheck.Clear();
            shot.checkedAt = null;
            shot.recordedCheck = null;
            shot.updatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("O");
            shot.updatedBy = defaultUpdatedBy;
        }

        /// <summary>把当前场景状态写回某个镜头（会改快照）。</summary>
        public void UpdateSnapshotOf(ShotData shot)
        {
            RecordUndo(shot);
            shot.snapshot = CaptureSnapshot();
            shot.revision += 1;
            shot.lastCheck.Clear();
            shot.checkedAt = null;
            shot.recordedCheck = null;
            shot.updatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("O");
            shot.updatedBy = defaultUpdatedBy;
        }

        public ShotData Find(string shotId)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].shotId == shotId) return shots[i];
            }
            return null;
        }

        public int IndexOf(ShotData shot)
        {
            return shots.IndexOf(shot);
        }

        private string NextShotId()
        {
            int n = shots.Count + 1;
            string id = sceneId + "-S" + n.ToString("00");
            while (Find(id) != null || Find(id.Substring(sceneId.Length + 1)) != null)
            {
                n++;
                id = sceneId + "-S" + n.ToString("00");
            }
            return id;
        }

        // ---------- 删除（只在编辑器菜单里调用，需人工确认） ----------

        public bool RemoveShot(string shotId)
        {
            var s = Find(shotId);
            if (s == null) return false;
            shots.Remove(s);
            return true;
        }

        public void ClearAll()
        {
            shots.Clear();
        }
    }
}
