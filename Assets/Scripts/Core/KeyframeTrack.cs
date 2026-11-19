using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>
    /// 通用时序关键帧分段定位工具（消除 ActorTake 与 CameraTrajectory 的二分查找重复）。
    /// </summary>
    public static class KeyframeTrack
    {
        public static void FindSegment<T>(
            IReadOnlyList<T> items,
            Func<T, float> getTime,
            float t,
            out int idxA,
            out int idxB,
            out float alpha)
        {
            if (items == null || items.Count == 0)
            {
                idxA = idxB = -1;
                alpha = 0f;
                return;
            }

            if (items.Count == 1 || t <= getTime(items[0]))
            {
                idxA = idxB = 0;
                alpha = 0f;
                return;
            }

            int last = items.Count - 1;
            if (t >= getTime(items[last]))
            {
                idxA = idxB = last;
                alpha = 0f;
                return;
            }

            int low = 0, high = last;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (getTime(items[mid]) < t) low = mid + 1;
                else high = mid - 1;
            }

            idxA = Mathf.Clamp(low - 1, 0, last);
            idxB = Mathf.Clamp(low, 0, last);
            float tA = getTime(items[idxA]);
            float tB = getTime(items[idxB]);
            float span = tB - tA;
            alpha = span > 0.0001f ? Mathf.Clamp01((t - tA) / span) : 0f;
        }
    }
}
