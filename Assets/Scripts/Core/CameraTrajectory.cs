using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    [Serializable]
    public struct TrajectoryPoint
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;

        public TrajectoryPoint(float time, Vector3 position, Quaternion rotation, float fov = 45f)
        {
            this.time = time;
            this.position = position;
            this.rotation = rotation;
            this.fieldOfView = fov;
        }

        public static TrajectoryPoint Lerp(TrajectoryPoint a, TrajectoryPoint b, float t) =>
            new TrajectoryPoint(
                Mathf.Lerp(a.time, b.time, t),
                Vector3.Lerp(a.position, b.position, t),
                Quaternion.Slerp(a.rotation, b.rotation, t),
                Mathf.Lerp(a.fieldOfView, b.fieldOfView, t));
    }

    [Serializable]
    public struct TrajectorySampleDto
    {
        public float t;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public float fov;

        public TrajectorySampleDto(TrajectoryPoint p)
        {
            t = p.time;
            px = p.position.x; py = p.position.y; pz = p.position.z;
            rx = p.rotation.x; ry = p.rotation.y; rz = p.rotation.z; rw = p.rotation.w;
            fov = p.fieldOfView;
        }

        public TrajectoryPoint ToPoint() =>
            new TrajectoryPoint(t, new Vector3(px, py, pz), new Quaternion(rx, ry, rz, rw), fov);
    }

    [Serializable]
    public class TrajectoryDto
    {
        public float duration;
        public List<TrajectorySampleDto> samples = new List<TrajectorySampleDto>();
    }

    [Serializable]
    public class CameraTrajectory
    {
        public List<TrajectoryPoint> points = new List<TrajectoryPoint>();
        public float totalDuration = 0f;
        public int Count => points.Count;

        public void AddPoint(float time, Vector3 pos, Quaternion rot, float fov = 45f)
        {
            if (points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (Mathf.Abs(time - last.time) < 0.0005f)
                {
                    points[points.Count - 1] = new TrajectoryPoint(last.time, pos, rot, fov);
                    return;
                }
                if (time < last.time) time = last.time + 0.001f;
            }
            points.Add(new TrajectoryPoint(time, pos, rot, fov));
            if (time > totalDuration) totalDuration = time;
        }

        public TrajectoryPoint Evaluate(float t)
        {
            if (points == null || points.Count == 0)
                return new TrajectoryPoint(0f, Vector3.zero, Quaternion.identity);
            if (t >= totalDuration) return points[points.Count - 1];
            KeyframeTrack.FindSegment(points, p => p.time, t, out int idxA, out int idxB, out float alpha);
            if (idxA < 0) return new TrajectoryPoint(0f, Vector3.zero, Quaternion.identity);
            return idxA == idxB ? points[idxA] : TrajectoryPoint.Lerp(points[idxA], points[idxB], alpha);
        }

        public CameraTrajectory Smooth(int windowSize = 5, bool preserveEndpoints = true)
        {
            var smoothed = new CameraTrajectory { totalDuration = totalDuration };
            if (points == null || points.Count == 0) return smoothed;
            if (points.Count <= 2)
            {
                smoothed.points = new List<TrajectoryPoint>(points);
                return smoothed;
            }

            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                if (preserveEndpoints && (i == 0 || i == n - 1))
                {
                    smoothed.points.Add(points[i]);
                    continue;
                }

                Vector3 pos;
                float fov;
                if (n >= 5 && i >= 2 && i <= n - 3)
                {
                    pos = (-3f * points[i - 2].position + 12f * points[i - 1].position + 17f * points[i].position
                         + 12f * points[i + 1].position - 3f * points[i + 2].position) / 35f;
                    fov = (-3f * points[i - 2].fieldOfView + 12f * points[i - 1].fieldOfView + 17f * points[i].fieldOfView
                         + 12f * points[i + 1].fieldOfView - 3f * points[i + 2].fieldOfView) / 35f;
                }
                else
                {
                    int prev = Mathf.Max(0, i - 1);
                    int next = Mathf.Min(n - 1, i + 1);
                    pos = (points[prev].position + 2f * points[i].position + points[next].position) * 0.25f;
                    fov = (points[prev].fieldOfView + 2f * points[i].fieldOfView + points[next].fieldOfView) * 0.25f;
                }

                int pIdx = Mathf.Max(0, i - 1);
                int nIdx = Mathf.Min(n - 1, i + 1);
                Quaternion neighborMid = Quaternion.Slerp(points[pIdx].rotation, points[nIdx].rotation, 0.5f);
                Quaternion rot = Quaternion.Slerp(points[i].rotation, neighborMid, 0.25f);

                smoothed.points.Add(new TrajectoryPoint(points[i].time, pos, rot, fov));
            }
            return smoothed;
        }

        public TrajectoryDto ToDto()
        {
            if (points == null || points.Count == 0) return null;
            var dto = new TrajectoryDto { duration = totalDuration };
            for (int i = 0; i < points.Count; i++) dto.samples.Add(new TrajectorySampleDto(points[i]));
            return dto;
        }

        public static CameraTrajectory FromDto(TrajectoryDto dto)
        {
            if (dto == null || dto.samples == null || dto.samples.Count == 0) return null;
            var traj = new CameraTrajectory { totalDuration = dto.duration };
            foreach (var sample in dto.samples) traj.points.Add(sample.ToPoint());
            return traj;
        }

        public CameraTrajectory Clone()
        {
            var clone = new CameraTrajectory { totalDuration = totalDuration };
            if (points != null) clone.points = new List<TrajectoryPoint>(points);
            return clone;
        }
    }
}
