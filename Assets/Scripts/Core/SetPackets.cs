using System;
using UnityEngine;

namespace ShotField.Core
{
    public enum ParticipantRole { Director, Producer, Actor, Observer }

    public enum PacketType { JoinRequest, JoinResponse, CameraSync, PassportSync, Comment, Heartbeat }

    [Serializable]
    public class SetPacket
    {
        public string type;
        public string senderId;
        public string payload;

        public static SetPacket Create<T>(PacketType packetType, string sender, T data)
        {
            return new SetPacket
            {
                type = packetType.ToString(),
                senderId = sender,
                payload = JsonUtility.ToJson(data)
            };
        }

        public T Unpack<T>() => JsonUtility.FromJson<T>(payload);
    }

    [Serializable]
    public class JoinRequestData
    {
        public string role;
        public string displayName;
    }

    [Serializable]
    public class JoinResponseData
    {
        public string clientId;
        public string assignedRole;
        public int currentShotIndex;
        public string sceneId;
    }

    [Serializable]
    public class CameraSyncData
    {
        public int shotIndex;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public float fov;

        public CameraSyncData(int shotIndex, Vector3 pos, Quaternion rot, float fov)
        {
            this.shotIndex = shotIndex;
            px = pos.x; py = pos.y; pz = pos.z;
            rx = rot.x; ry = rot.y; rz = rot.z; rw = rot.w;
            this.fov = fov;
        }

        public Vector3 Position => new Vector3(px, py, pz);
        public Quaternion Rotation => new Quaternion(rx, ry, rz, rw);
    }

    [Serializable]
    public class PassportSyncData
    {
        public string shotId;
        public int revision;
        public string passportJson;
    }

    [Serializable]
    public class CommentData
    {
        public string sender;
        public string text;
        public string timestamp;
        public string shotId;
    }
}
