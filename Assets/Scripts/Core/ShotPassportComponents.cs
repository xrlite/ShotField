using System;

namespace ShotField.Core
{
    [Serializable]
    public class ConstraintSpec
    {
        public string type, ruleName, propId, face, actorA, actorB, targetId;
        public bool blocking, waivable;
        public float minX, maxX, epsilon;
        public int occluderMask;
        public ConstraintSpec Copy() => (ConstraintSpec)MemberwiseClone();
    }

    public partial class ShotPassportDto
    {
        [Serializable]
        public class CameraFields
        {
            public float[] pos, rot;
            public float fov, height;
        }

        [Serializable]
        public class CameraExtras
        {
            public float nearClip, farClip, aspect;
            public bool usesUnityCameraRotation;
            public ShotSize shotSize;
            public ShotAngle angle;
        }

        [Serializable]
        public class ActorFields
        {
            public string id, pose = "";
            public float[] pos;
            public float rot;
        }

        [Serializable]
        public class PropFields
        {
            public string id, state;
            public float[] pos;
        }

        [Serializable]
        public class CheckSummary
        {
            public int pass, violation, pending;
            public string checked_at = "";
            public CheckSummary Copy() => (CheckSummary)MemberwiseClone();
        }
    }
}
