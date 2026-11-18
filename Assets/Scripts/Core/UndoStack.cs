using System;
using System.Collections.Generic;

namespace ShotField.Core
{
    /// <summary>
    /// 镜头快照撤销/重做栈（纯 C# 服务，容量上限默认 10 步）。
    /// 具备深拷贝语义，确保当前操作不会污染历史记录。
    /// </summary>
    public class UndoStack
    {
        private readonly int _maxCapacity;
        private readonly List<ShotData> _undoList = new List<ShotData>();
        private readonly List<ShotData> _redoList = new List<ShotData>();

        public int Capacity => _maxCapacity;
        public int UndoCount => _undoList.Count;
        public int RedoCount => _redoList.Count;
        public bool CanUndo => _undoList.Count > 0;
        public bool CanRedo => _redoList.Count > 0;

        public UndoStack(int maxCapacity = 10)
        {
            _maxCapacity = Math.Max(1, maxCapacity);
        }

        public void Push(ShotData state)
        {
            if (state == null) return;
            _undoList.Add(state.Clone());
            while (_undoList.Count > _maxCapacity)
            {
                _undoList.RemoveAt(0);
            }
            _redoList.Clear();
        }

        public ShotData Undo(ShotData currentState)
        {
            if (!CanUndo) return currentState;
            if (currentState != null)
            {
                _redoList.Add(currentState.Clone());
            }

            int lastIndex = _undoList.Count - 1;
            var target = _undoList[lastIndex];
            _undoList.RemoveAt(lastIndex);
            return target.Clone();
        }

        public ShotData Redo(ShotData currentState)
        {
            if (!CanRedo) return currentState;
            if (currentState != null)
            {
                _undoList.Add(currentState.Clone());
            }

            int lastIndex = _redoList.Count - 1;
            var target = _redoList[lastIndex];
            _redoList.RemoveAt(lastIndex);
            return target.Clone();
        }

        public ShotData PeekUndo()
        {
            if (!CanUndo) return null;
            return _undoList[_undoList.Count - 1].Clone();
        }

        public ShotData PeekOldestUndo()
        {
            if (!CanUndo) return null;
            return _undoList[0].Clone();
        }

        public void Clear()
        {
            _undoList.Clear();
            _redoList.Clear();
        }
    }
}
