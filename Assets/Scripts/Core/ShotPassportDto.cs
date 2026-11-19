using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    [Serializable]
    public partial class ShotPassportDto
    {
        public string shot_id, approval_status, updated_at, updated_by;
        public int revision;
        public CameraFields camera;
        public CameraExtras cameraExtras;
        public List<ActorFields> actors = new List<ActorFields>();
        public List<PropFields> props = new List<PropFields>();
        public ConstraintSpec[] constraints;
        public CheckSummary check_summary;
        public string intent;
        public string[] lockedRules;
        public List<ActorState> snapshotExtras;
        public bool approvalLocked;
        public TrajectoryDto cameraTrajectory;
        public ActorTakeDto actorTake;

        public static string QualifyShotId(string shotId, string sceneId) =>
            ShotPassportMapper.QualifyShotId(shotId, sceneId);

        public static ShotPassportDto FromShot(ShotData shot, string sceneId, ConstraintSpec[] specs) =>
            ShotPassportMapper.FromShot(shot, sceneId, specs);

        public ShotData ToShot() => ShotPassportMapper.ToShot(this);

        public static ConstraintSpec[] CopySpecs(ConstraintSpec[] specs) =>
            ShotPassportMapper.CopySpecs(specs);
    }
}
