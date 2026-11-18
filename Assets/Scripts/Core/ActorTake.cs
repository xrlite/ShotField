using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public struct ActorKeyframe
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public string actionTag;

        public ActorKeyframe(float time, Vector3 pos, Quaternion rot, string action = "")
        {
            this.time = time;
            this.position = pos;
            this.rotation = rot;
            this.actionTag = action ?? "";
        }

        public static ActorKeyframe Lerp(ActorKeyframe a, ActorKeyframe b, float t) =>
            new ActorKeyframe(
                Mathf.Lerp(a.time, b.time, t),
                Vector3.Lerp(a.position, b.position, t),
                Quaternion.Slerp(a.rotation, b.rotation, t),
                t < 0.5f ? a.actionTag : b.actionTag);
    }

    [Serializable]
    public struct ActorKeyframeDto
    {
        public float t;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public string action;

        public ActorKeyframeDto(ActorKeyframe k)
        {
            t = k.time;
            px = k.position.x; py = k.position.y; pz = k.position.z;
            rx = k.rotation.x; ry = k.rotation.y; rz = k.rotation.z; rw = k.rotation.w;
            action = k.actionTag;
        }

        public ActorKeyframe ToKeyframe() =>
            new ActorKeyframe(t, new Vector3(px, py, pz), new Quaternion(rx, ry, rz, rw), action);
    }

    [Serializable]
    public class ActorTakeDto
    {
        public string takeId;
        public string actorId;
        public float duration;
        public List<ActorKeyframeDto> keyframes = new List<ActorKeyframeDto>();
    }

    [Serializable]
    public class ActorTake
    {
        public string takeId = "Take_01";
        public string actorId = "actorA";
        public float duration = 0f;
        public List<ActorKeyframe> keyframes = new List<ActorKeyframe>();

        public int Count => keyframes.Count;

        public void AddKeyframe(float t, Vector3 pos, Quaternion rot, string action = "")
        {
            keyframes.Add(new ActorKeyframe(t, pos, rot, action));
            if (t > duration) duration = t;
        }

        public ActorKeyframe Evaluate(float t)
        {
            if (keyframes == null || keyframes.Count == 0)
                return new ActorKeyframe(0f, Vector3.zero, Quaternion.identity);
            if (keyframes.Count == 1 || t <= keyframes[0].time)
                return keyframes[0];
            if (t >= duration)
                return keyframes[keyframes.Count - 1];

            int low = 0, high = keyframes.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (keyframes[mid].time < t) low = mid + 1;
                else high = mid - 1;
            }

            int idxA = Mathf.Clamp(low - 1, 0, keyframes.Count - 1);
            int idxB = Mathf.Clamp(low, 0, keyframes.Count - 1);
            if (idxA == idxB) return keyframes[idxA];

            var kA = keyframes[idxA];
            var kB = keyframes[idxB];
            float span = kB.time - kA.time;
            float factor = span > 0.0001f ? (t - kA.time) / span : 0f;
            return ActorKeyframe.Lerp(kA, kB, factor);
        }

        public ActorTakeDto ToDto()
        {
            if (keyframes == null || keyframes.Count == 0) return null;
            var dto = new ActorTakeDto { takeId = takeId, actorId = actorId, duration = duration };
            for (int i = 0; i < keyframes.Count; i++) dto.keyframes.Add(new ActorKeyframeDto(keyframes[i]));
            return dto;
        }

        public static ActorTake FromDto(ActorTakeDto dto)
        {
            if (dto == null || dto.keyframes == null || dto.keyframes.Count == 0) return null;
            var take = new ActorTake { takeId = dto.takeId, actorId = dto.actorId, duration = dto.duration };
            for (int i = 0; i < dto.keyframes.Count; i++) take.keyframes.Add(dto.keyframes[i].ToKeyframe());
            return take;
        }

        public ActorTake Clone()
        {
            var clone = new ActorTake { takeId = takeId, actorId = actorId, duration = duration };
            if (keyframes != null) clone.keyframes = new List<ActorKeyframe>(keyframes);
            return clone;
        }
    }
}
