using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShotField.Core
{
    /// <summary>一次道具摆放的位姿快照（世界空间）。</summary>
    [Serializable]
    public struct PropPose
    {
        public string propId;
        public Vector3 position;
        public Quaternion rotation;

        public PropPose(string id, Vector3 pos, Quaternion rot)
        {
            propId = id;
            position = pos;
            rotation = rot;
        }
    }

    /// <summary>一次完整拖拽 = 一条记录，撤销时回到 before。</summary>
    public sealed class PropMoveRecord
    {
        public readonly PropPose before;
        public readonly PropPose after;

        public PropMoveRecord(PropPose before, PropPose after)
        {
            this.before = before;
            this.after = after;
        }

        /// <summary>位移与转角都小于阈值时视为没动，不记历史。</summary>
        public bool IsMeaningful(float positionEpsilon = 0.01f, float angleEpsilon = 1f)
        {
            return Vector3.Distance(before.position, after.position) > positionEpsilon
                || Quaternion.Angle(before.rotation, after.rotation) > angleEpsilon;
        }
    }

    /// <summary>
    /// 道具摆放撤销栈。刻意不与 UndoStack 合并：那个存镜头快照 ShotData，
    /// 这个存道具位姿，两类操作混在一个栈里会互相冲掉对方的历史。
    /// </summary>
    public sealed class PropMoveHistory
    {
        private readonly int _maxCapacity;
        private readonly List<PropMoveRecord> _undo = new List<PropMoveRecord>();
        private readonly List<PropMoveRecord> _redo = new List<PropMoveRecord>();

        public int Capacity { get { return _maxCapacity; } }
        public int UndoCount { get { return _undo.Count; } }
        public int RedoCount { get { return _redo.Count; } }
        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        public PropMoveHistory(int maxCapacity = 20)
        {
            _maxCapacity = Math.Max(1, maxCapacity);
        }

        public void Push(PropMoveRecord record)
        {
            if (record == null) return;
            _undo.Add(record);
            while (_undo.Count > _maxCapacity) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>弹出最近一条并交给调用方回滚，返回的记录同时进入重做栈。</summary>
        public PropMoveRecord Undo()
        {
            if (!CanUndo) return null;
            int last = _undo.Count - 1;
            var record = _undo[last];
            _undo.RemoveAt(last);
            _redo.Add(record);
            return record;
        }

        public PropMoveRecord Redo()
        {
            if (!CanRedo) return null;
            int last = _redo.Count - 1;
            var record = _redo[last];
            _redo.RemoveAt(last);
            _undo.Add(record);
            return record;
        }

        public PropMoveRecord PeekUndo()
        {
            return CanUndo ? _undo[_undo.Count - 1] : null;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
