using System;
using UnityEngine;

namespace ShotField.Core
{
    public partial class ShotLibrary
    {
        /// <summary>
        /// 新建镜头。继承时深拷贝上一镜；首镜没有可继承对象时捕获当前场景。
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
    }
}
