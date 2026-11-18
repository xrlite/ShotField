using System;

namespace ShotField.Core
{
    /// <summary>
    /// 定时自动保存核心调度服务（纯 C# 服务，无 MonoBehaviour 依赖）。
    /// 默认 30 秒周期，支持脏标记检查，仅在有修改时触发保存事件。
    /// </summary>
    public class AutoSaveService
    {
        private float _interval;
        private float _timer;
        private bool _isDirty;

        public float Interval
        {
            get => _interval;
            set => _interval = Math.Max(1.0f, value);
        }

        public float ElapsedTime => _timer;
        public bool IsDirty => _isDirty;

        public AutoSaveService(float intervalSeconds = 30.0f)
        {
            _interval = Math.Max(1.0f, intervalSeconds);
            _timer = 0f;
            _isDirty = false;
        }

        public void MarkDirty() => _isDirty = true;
        public void ClearDirty() => _isDirty = false;

        public void ResetTimer() => _timer = 0f;

        /// <summary>
        /// 步进时钟。当时间到达间隔且处于脏状态时，消耗脏标记并返回 true。
        /// 若未达周期或无脏标记，返回 false。
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return false;
            _timer += deltaTime;

            if (_timer >= _interval)
            {
                _timer = 0f;
                if (_isDirty)
                {
                    _isDirty = false;
                    return true;
                }
            }
            return false;
        }
    }
}
