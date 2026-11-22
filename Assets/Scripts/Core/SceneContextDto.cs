using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// Lightweight summary of shot passport metadata associated with a scene object.
    /// Compatible with UnityEngine.JsonUtility serialization.
    /// </summary>
    [Serializable]
    public class PassportSummaryDto
    {
        public string latestShotId;
        public int revision;
        public string approvalStatus;
        public int activeConstraintCount;
        public string lastModifiedTimestamp;

        public PassportSummaryDto()
        {
        }

        public PassportSummaryDto(string shotId, int rev, string status, int constraints, string timestamp)
        {
            latestShotId = shotId;
            revision = rev;
            approvalStatus = status;
            activeConstraintCount = constraints;
            lastModifiedTimestamp = timestamp;
        }
    }

    /// <summary>
    /// Summary of an active director spatial constraint in the scene.
    /// </summary>
    [Serializable]
    public class SceneConstraintSummaryDto
    {
        public string constraintId;
        public string ruleType;
        public string sourceId;
        public string targetId;
        public bool isBlocking;
        public bool isWaivable;

        public SceneConstraintSummaryDto()
        {
        }

        public SceneConstraintSummaryDto(string id, string rule, string src, string tgt, bool blocking, bool waivable)
        {
            constraintId = id;
            ruleType = rule;
            sourceId = src;
            targetId = tgt;
            isBlocking = blocking;
            isWaivable = waivable;
        }
    }

    /// <summary>
    /// Serializable representation of an entity in the virtual studio scene.
    /// </summary>
    [Serializable]
    public class SceneObjectDto
    {
        public string objectId;
        public string displayName;
        public string archetype;
        public string category;
        public Vector3Dto position;
        public QuaternionDto rotation;
        public BoundsDto bounds;
        public bool isActive;
        public PassportSummaryDto passportSummary;

        public SceneObjectDto()
        {
        }
    }

    /// <summary>
    /// Complete scene snapshot transfer object for AI context feeding and network transmission.
    /// </summary>
    [Serializable]
    public class SceneContextDto
    {
        public string sceneId;
        public long timestampUtc;
        public NavMeshBoundsDto walkableBounds;
        public List<SceneObjectDto> objects = new List<SceneObjectDto>();
        public List<SceneConstraintSummaryDto> activeConstraints = new List<SceneConstraintSummaryDto>();

        public SceneContextDto()
        {
        }

        public SceneContextDto(string sceneId, long timestampUtc, NavMeshBoundsDto walkableBounds)
        {
            this.sceneId = sceneId;
            this.timestampUtc = timestampUtc;
            this.walkableBounds = walkableBounds;
        }
    }
}
