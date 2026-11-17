using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>约束检查的三态结果。对应产品定义：通过 / 违规 / 待确认。</summary>
    public enum CheckStatus
    {
        Pass = 0,
        Violation = 1,
        Pending = 2
    }

    /// <summary>景别。</summary>
    public enum ShotSize { Auto = 0, Wide = 1, Full = 2, Medium = 3, CloseUp = 4, Detail = 5 }

    /// <summary>机位角度。</summary>
    public enum ShotAngle { Auto = 0, EyeLevel = 1, Low = 2, High = 3, Dutch = 4 }

    /// <summary>审批状态。</summary>
    public enum ApprovalStatus { Draft = 0, Reviewed = 1, Approved = 2, Locked = 3 }

    /// <summary>单个对象的快照。存值，不存引用——这是"镜头之间不共享可变数据"的基础。</summary>
    [Serializable]
    public class ActorState
    {
        public string actorId;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public bool active = true;
    }

    /// <summary>场景快照：某一时刻整个片场的静态状态。深拷贝语义。</summary>
    [Serializable]
    public class SceneSnapshot
    {
        public List<ActorState> actors = new List<ActorState>();

        public SceneSnapshot Clone()
        {
            var copy = new SceneSnapshot();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                copy.actors.Add(new ActorState
                {
                    actorId = a.actorId,
                    position = a.position,
                    rotation = a.rotation,
                    scale = a.scale,
                    active = a.active
                });
            }
            return copy;
        }

        public ActorState Find(string actorId)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].actorId == actorId) return actors[i];
            }
            return null;
        }
    }

    /// <summary>机位状态：镜头自己的、独立于场景的那一半数据。</summary>
    [Serializable]
    public class ShotCameraState
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public float fieldOfView = 45f;
        public float nearClip = 0.05f;
        public float farClip = 100f;
        public ShotSize shotSize = ShotSize.Auto;
        public ShotAngle angle = ShotAngle.Auto;
        public float heightAboveFloor;   // 机位高度，米

        public static ShotCameraState FromCamera(Camera cam)
        {
            var s = new ShotCameraState();
            if (cam == null) return s;
            var t = cam.transform;
            s.position = t.position;
            s.rotation = t.rotation;
            s.fieldOfView = cam.fieldOfView;
            s.nearClip = cam.nearClipPlane;
            s.farClip = cam.farClipPlane;
            s.heightAboveFloor = t.position.y;
            return s;
        }

        public void ApplyTo(Camera cam)
        {
            if (cam == null) return;
            cam.transform.position = position;
            cam.transform.rotation = rotation;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
        }

        public Matrix4x4 ViewMatrix
        {
            get { return Matrix4x4.TRS(position, rotation, Vector3.one).inverse; }
        }

        public Matrix4x4 ProjectionMatrix(float aspect, bool useFov)
        {
            float fov = useFov ? fieldOfView : 60f;
            return Matrix4x4.Perspective(fov, aspect, nearClip, farClip);
        }
    }

    /// <summary>一条约束的检查结果。</summary>
    [Serializable]
    public class ConstraintResult
    {
        public string ruleName = "";
        public CheckStatus status = CheckStatus.Pending;
        public string detail = "";
        public string targetId = "";

        public static ConstraintResult Pass(string rule, string detail, string target)
        {
            return new ConstraintResult { ruleName = rule, status = CheckStatus.Pass, detail = detail, targetId = target };
        }

        public static ConstraintResult Violation(string rule, string detail, string target)
        {
            return new ConstraintResult { ruleName = rule, status = CheckStatus.Violation, detail = detail, targetId = target };
        }

        public static ConstraintResult Pending(string rule, string detail, string target)
        {
            return new ConstraintResult { ruleName = rule, status = CheckStatus.Pending, detail = detail, targetId = target };
        }
    }

    /// <summary>一个镜头 = 一份场景快照 + 一份独立机位 + 意图与审批记录。这就是镜头护照的代码形态。</summary>
    [Serializable]
    public class ShotData
    {
        public string shotId = "";
        public int revision = 1;
        public ShotCameraState camera = new ShotCameraState();
        public SceneSnapshot snapshot = new SceneSnapshot();

        [TextArea(2, 5)]
        public string intent = "";              // 导演意图：为什么这样拍

        public List<string> lockedRules = new List<string>();   // 不可修改的约束名
        public ApprovalStatus approval = ApprovalStatus.Draft;
        public string updatedAt = "";
        public string updatedBy = "";

        public ConstraintSpec[] recordedConstraints;
        public ShotPassportDto.CheckSummary recordedCheck;
        [NonSerialized] public string checkedAt;
        [NonSerialized] public List<ConstraintResult> lastCheck = new List<ConstraintResult>();

        public ShotData Clone()
        {
            var c = new ShotData
            {
                shotId = shotId,
                revision = revision,
                camera = new ShotCameraState
                {
                    position = camera.position,
                    rotation = camera.rotation,
                    fieldOfView = camera.fieldOfView,
                    nearClip = camera.nearClip,
                    farClip = camera.farClip,
                    shotSize = camera.shotSize,
                    angle = camera.angle,
                    heightAboveFloor = camera.heightAboveFloor
                },
                snapshot = snapshot.Clone(),
                intent = intent,
                approval = approval,
                updatedAt = updatedAt,
                updatedBy = updatedBy
            };
            c.lockedRules.AddRange(lockedRules);
            c.recordedConstraints = ShotPassportDto.CopySpecs(recordedConstraints);
            c.recordedCheck = recordedCheck?.Copy();
            return c;
        }
    }
}
