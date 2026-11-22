using System;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Serializable RGBA color representation compatible with JsonUtility.
    /// </summary>
    [Serializable]
    public struct ColorRgbaDto
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public ColorRgbaDto(float r, float g, float b, float a = 1.0f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color ToUnityColor()
        {
            return new Color(r, g, b, a);
        }

        public static ColorRgbaDto FromUnityColor(Color c)
        {
            return new ColorRgbaDto(c.r, c.g, c.b, c.a);
        }
    }

    /// <summary>
    /// Generic transport envelope for scene commands.
    /// Encapsulates command metadata and raw JSON payload for polymorph-safe serialization.
    /// </summary>
    [Serializable]
    public class SceneCommandEnvelopeDto
    {
        public string commandId;
        public int commandType;
        public string targetId;
        public long timestampUtc;
        public string payloadJson;

        public SceneCommandEnvelopeDto()
        {
        }

        public SceneCommandEnvelopeDto(string id, SceneCommandType type, string target, string payload)
        {
            commandId = id;
            commandType = (int)type;
            targetId = target;
            timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            payloadJson = payload;
        }
    }

    /// <summary>
    /// Payload parameters for spawning a prop or actor into the scene.
    /// </summary>
    [Serializable]
    public class SpawnCommandPayload
    {
        public string propId;
        public string instanceId;
        public Vector3Dto position;
        public QuaternionDto rotation;
        public Vector3Dto scale;

        public SpawnCommandPayload()
        {
            scale = new Vector3Dto(1f, 1f, 1f);
        }
    }

    /// <summary>
    /// Payload parameters for moving an actor or prop to a target location.
    /// </summary>
    [Serializable]
    public class MoveCommandPayload
    {
        public string actorId;
        public Vector3Dto targetPosition;
        public float desiredSpeed;
        public float baseOffset;

        public MoveCommandPayload()
        {
            desiredSpeed = 1.5f;
            baseOffset = 0.748f;
        }
    }

    /// <summary>
    /// Payload parameters for assigning a material color or palette scheme.
    /// </summary>
    [Serializable]
    public class AssignColorPayload
    {
        public string targetId;
        public string colorKey;
        public ColorRgbaDto customColor;

        public AssignColorPayload()
        {
        }

        public AssignColorPayload(string targetId, string colorKey)
        {
            this.targetId = targetId;
            this.colorKey = colorKey;
        }
    }
}
