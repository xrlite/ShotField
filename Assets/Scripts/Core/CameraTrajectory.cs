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

        public static TrajectoryPoint Lerp(TrajectoryPoint a, TrajectoryPoint b, float t)
        {
            return new TrajectoryPoint(
                Mathf.Lerp(a.time, b.time, t),
                Vector3.Lerp(a.position, b.position, t),
                Quaternion.Slerp(a.rotation, b.rotation, t),
                Mathf.Lerp(a.fieldOfView, b.fieldOfView, t)
            );
        }
    }

    [Serializable]
    public class CameraTrajectory
    {
        public List<TrajectoryPoint> points = new List<TrajectoryPoint>();
        public float totalDuration = 0f;

        public int Count => points.Count;

        public void AddPoint(float time, Vector3 pos, Quaternion rot, float fov = 45f)
        {
            points.Add(new TrajectoryPoint(time, pos, rot, fov));
            if (time > totalDuration) totalDuration = time;
        }

        public TrajectoryPoint Evaluate(float t)
        {
            if (points == null || points.Count == 0)
                return new TrajectoryPoint(0f, Vector3.zero, Quaternion.identity);

            if (points.Count == 1 || t <= points[0].time)
                return points[0];

            if (t >= points[points.Count - 1].time)
                return points[points.Count - 1];

            int low = 0, high = points.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (points[mid].time < t)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            int indexA = Mathf.Clamp(low - 1, 0, points.Count - 1);
            int indexB = Mathf.Clamp(low, 0, points.Count - 1);
            if (indexA == indexB) return points[indexA];

            var pA = points[indexA];
            var pB = points[indexB];
            float span = pB.time - pA.time;
            float factor = span > 0.0001f ? (t - pA.time) / span : 0f;
            return TrajectoryPoint.Lerp(pA, pB, factor);
        }

        public CameraTrajectory Smooth(int windowSize = 3)
        {
            var smoothed = new CameraTrajectory { totalDuration = totalDuration };
            if (points == null || points.Count == 0) return smoothed;
            if (points.Count <= 2 || windowSize <= 1)
            {
                smoothed.points = new List<TrajectoryPoint>(points);
                return smoothed;
            }

            int half = windowSize / 2;
            for (int i = 0; i < points.Count; i++)
            {
                int start = Mathf.Max(0, i - half);
                int end = Mathf.Min(points.Count - 1, i + half);
                Vector3 sumPos = Vector3.zero;
                float sumFov = 0f;
                int count = end - start + 1;

                Quaternion baseRot = points[i].rotation;
                for (int j = start; j <= end; j++)
                {
                    sumPos += points[j].position;
                    sumFov += points[j].fieldOfView;
                }

                Quaternion avgRot = baseRot;
                if (count > 1)
                {
                    for (int j = start; j <= end; j++)
                    {
                        float weight = 1f / (j - start + 1);
                        avgRot = Quaternion.Slerp(avgRot, points[j].rotation, weight);
                    }
                }

                smoothed.points.Add(new TrajectoryPoint(
                    points[i].time,
                    sumPos / count,
                    avgRot,
                    sumFov / count
                ));
            }
            return smoothed;
        }

        public CameraTrajectory Clone()
        {
            var clone = new CameraTrajectory { totalDuration = totalDuration };
            if (points != null) clone.points = new List<TrajectoryPoint>(points);
            return clone;
        }
    }
}
