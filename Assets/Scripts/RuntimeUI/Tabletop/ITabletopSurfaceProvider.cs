using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘承载面来源抽象。真实实现走 MR 平面识别（PICO 官方接口），
    /// 本文件只提供契约与一个"永远拿不到面"的空实现，供模拟器与无 MR 环境降级。
    ///
    /// 设计约束：实现方不得在这里做任何生命周期管理，只回答"当前有没有可用桌面"。
    /// TabletopDirector 通过 <see cref="TabletopSurfaceBinder"/> 消费本接口，
    /// 拿不到面时保持 tabletopAnchor 的手工位姿，不得报错或阻断主流程。
    /// </summary>
    public interface ITabletopSurfaceProvider
    {
        /// <summary>尝试取得一块可用于摆放沙盘的平面。false 表示当前不可用，调用方须降级。</summary>
        bool TryGetSurface(out TabletopSurface surface);
    }

    /// <summary>一块承载面的最小描述：中心、法线、尺寸。坐标系为世界空间。</summary>
    public readonly struct TabletopSurface
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly Vector2 Size;

        public TabletopSurface(Vector3 center, Vector3 normal, Vector2 size)
        {
            Center = center;
            Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Size = size;
        }

        /// <summary>尺寸是否有意义（两个轴都大于 0）。</summary>
        public bool HasExtent => Size.x > 0.01f && Size.y > 0.01f;

        /// <summary>法线是否接近水平朝上，即是否是一张"能放东西的桌子"而不是墙。</summary>
        public bool FacesUp => Vector3.Dot(Normal, Vector3.up) > 0.7f;
    }

    /// <summary>
    /// 空实现：永远返回 false。模拟器、PC 编辑器、以及未接入 MR 的设备统一用它，
    /// 这样上层不需要写 #if 分支，也不会因为缺少 provider 抛空引用。
    /// </summary>
    public sealed class NullTabletopSurfaceProvider : ITabletopSurfaceProvider
    {
        public static readonly NullTabletopSurfaceProvider Instance = new NullTabletopSurfaceProvider();

        public bool TryGetSurface(out TabletopSurface surface)
        {
            surface = default;
            return false;
        }
    }
}
