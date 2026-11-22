using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// Serializable 3D vector transfer object for network payloads and AI communication.
    /// Compatible with UnityEngine.JsonUtility and native Vector3 conversions.
    /// </summary>
    [Serializable]
    public struct Vector3Dto
    {
        public float x;
        public float y;
        public float z;

        public Vector3Dto(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 ToUnityVector3()
        {
            return new Vector3(x, y, z);
        }

        public static Vector3Dto FromUnityVector3(Vector3 v)
        {
            return new Vector3Dto(v.x, v.y, v.z);
        }

        public static implicit operator Vector3(Vector3Dto dto)
        {
            return new Vector3(dto.x, dto.y, dto.z);
        }

        public static implicit operator Vector3Dto(Vector3 v)
        {
            return new Vector3Dto(v.x, v.y, v.z);
        }

        public override string ToString()
        {
            return $"({x:F3}, {y:F3}, {z:F3})";
        }
    }

    /// <summary>
    /// Serializable quaternion transfer object for orientation representation.
    /// </summary>
    [Serializable]
    public struct QuaternionDto
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public QuaternionDto(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Quaternion ToUnityQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }

        public static QuaternionDto FromUnityQuaternion(Quaternion q)
        {
            return new QuaternionDto(q.x, q.y, q.z, q.w);
        }

        public static implicit operator Quaternion(QuaternionDto dto)
        {
            return new Quaternion(dto.x, dto.y, dto.z, dto.w);
        }

        public static implicit operator QuaternionDto(Quaternion q)
        {
            return new QuaternionDto(q.x, q.y, q.z, q.w);
        }
    }

    /// <summary>
    /// Axis-aligned bounding box representation for spatial entities.
    /// </summary>
    [Serializable]
    public class BoundsDto
    {
        public Vector3Dto center;
        public Vector3Dto size;
        public Vector3Dto min;
        public Vector3Dto max;

        public BoundsDto()
        {
        }

        public BoundsDto(Vector3Dto center, Vector3Dto size)
        {
            this.center = center;
            this.size = size;
            this.min = new Vector3Dto(center.x - size.x * 0.5f, center.y - size.y * 0.5f, center.z - size.z * 0.5f);
            this.max = new Vector3Dto(center.x + size.x * 0.5f, center.y + size.y * 0.5f, center.z + size.z * 0.5f);
        }

        public Bounds ToUnityBounds()
        {
            return new Bounds(center.ToUnityVector3(), size.ToUnityVector3());
        }
    }

    /// <summary>
    /// NavMesh floor and walkable surface boundaries summary.
    /// </summary>
    [Serializable]
    public class NavMeshBoundsDto
    {
        public BoundsDto floorBounds;
        public BoundsDto walkableBounds;
        public List<BoundsDto> obstacles = new List<BoundsDto>();
        public bool isSampled;
        public string sampleTimestamp;

        public NavMeshBoundsDto()
        {
        }
    }
}
