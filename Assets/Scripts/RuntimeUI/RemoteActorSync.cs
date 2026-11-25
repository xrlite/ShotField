using System;
using System.Text;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 联机位姿落地层：把 SceneEdit / ActorPose 写到场景里对应的演员。
    /// actorId 与实例名做规范化匹配（大小写、下划线、actor 前缀都不敏感）；
    /// 找不到演员时显式返回失败原因，绝不静默吞掉。
    /// </summary>
    public static class RemoteActorSync
    {
        /// <summary>测试或特殊宿主可注入解析器（key 已规范化）；生产默认遍历 ActorSelection。</summary>
        public static Func<string, Transform> OverrideTransformResolver;

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var lower = id.Trim().ToLowerInvariant();
            var sb = new StringBuilder(lower.Length);
            foreach (var c in lower)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            var flat = sb.ToString();
            return flat.StartsWith("actor") ? flat.Substring(5) : flat;
        }

        public static ActorNavigator Find(string actorId)
        {
            var key = NormalizeId(actorId);
            if (key.Length == 0) return null;
            foreach (var actor in ActorSelection.All)
            {
                if (actor == null) continue;
                if (NormalizeId(actor.actorId) == key) return actor;
                if (NormalizeId(actor.gameObject.name) == key) return actor;
            }
            return null;
        }

        public static bool ApplySceneEdit(SceneEditData data, out string reason)
        {
            if (data == null) { reason = "null-edit"; return false; }
            return Apply(data.actorId, new Vector3(data.px, data.py, data.pz), data.Rotation, out reason);
        }

        public static bool ApplyPose(ActorPoseData data, out string reason)
        {
            if (data == null) { reason = "null-pose"; return false; }
            return Apply(data.actorId, data.Position, data.Rotation, out reason);
        }

        public static bool Apply(string actorId, Vector3 position, Quaternion rotation, out string reason)
        {
            var key = NormalizeId(actorId);
            var actor = Find(actorId);
            Transform target = actor != null ? actor.transform : null;
            if (target == null && OverrideTransformResolver != null && key.Length > 0)
                target = OverrideTransformResolver(key);
            if (target == null) { reason = "actor-not-found"; return false; }
            // NavMesh 在场时走 Warp（保留寻路网格约束）；临时/无网格对象退化为直接置位。
            if (actor != null && actor.agent != null && actor.agent.isOnNavMesh) actor.Teleport(position);
            else target.position = position;
            target.rotation = rotation;
            reason = "applied";
            return true;
        }

        public static SceneEditData CaptureSceneEdit(ActorNavigator actor, int revision, string actionTag = "")
        {
            var t = actor.transform;
            var q = t.rotation;
            return new SceneEditData
            {
                actorId = ResolveId(actor),
                px = t.position.x, py = t.position.y, pz = t.position.z,
                rx = q.x, ry = q.y, rz = q.z, rw = q.w, fullRotation = true,
                actionTag = actionTag ?? "",
                revision = revision,
                clientTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public static ActorPoseData CapturePose(ActorNavigator actor, int sequence, string actionTag = "")
        {
            var t = actor.transform;
            var q = t.rotation;
            return new ActorPoseData
            {
                actorId = ResolveId(actor),
                px = t.position.x, py = t.position.y, pz = t.position.z,
                rx = q.x, ry = q.y, rz = q.z, rw = q.w,
                actionTag = actionTag ?? "",
                sequence = sequence
            };
        }

        public static string ResolveId(ActorNavigator actor)
        {
            if (actor == null) return "";
            if (!string.IsNullOrEmpty(actor.actorId)) return actor.actorId;
            return NormalizeId(actor.gameObject.name);
        }
    }
}
