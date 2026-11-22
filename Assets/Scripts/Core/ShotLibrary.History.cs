using System;
using System.IO;
using UnityEngine;

namespace ShotField.Core
{
    public partial class ShotLibrary
    {
        public UndoStack UndoStack { get; } = new UndoStack(10);
        public AutoSaveService AutoSave { get; } = new AutoSaveService(30f);

        public event Action<ShotData> OnUndoExecuted;
        public event Action<ShotData> OnRedoExecuted;
        public event Action<string> OnAutoSaved;

        public void MarkDirty() => AutoSave?.MarkDirty();

        public void RecordUndo(ShotData shot)
        {
            if (shot == null) return;
            UndoStack?.Push(shot);
        }

        public bool Undo()
        {
            if (UndoStack == null || !UndoStack.CanUndo || Latest == null) return false;
            var current = Latest;
            var target = UndoStack.Undo(current);
            if (target == null) return false;

            ApplyShot(target);
            int index = IndexOf(current);
            if (index >= 0 && index < shots.Count)
            {
                shots[index] = target;
            }
            OnUndoExecuted?.Invoke(target);
            MarkDirty();
            return true;
        }

        public bool Redo()
        {
            if (UndoStack == null || !UndoStack.CanRedo || Latest == null) return false;
            var current = Latest;
            var target = UndoStack.Redo(current);
            if (target == null) return false;

            ApplyShot(target);
            int index = IndexOf(current);
            if (index >= 0 && index < shots.Count)
            {
                shots[index] = target;
            }
            OnRedoExecuted?.Invoke(target);
            MarkDirty();
            return true;
        }

        public void TriggerAutoSave()
        {
            if (Latest == null) return;
            try
            {
                var folder = Path.Combine(ShotPassportStore.ProjectFolder, "AutoSave");
                var dto = ShotPassportMapper.FromShot(Latest, sceneId, Latest.recordedConstraints ?? Array.Empty<ConstraintSpec>());
                var path = ShotPassportStore.SaveNextRevision(dto, folder);
                OnAutoSaved?.Invoke(path);
                Debug.Log($"[ShotLibrary AutoSave] 自动保存成功: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShotLibrary AutoSave] 自动保存异常: {ex.Message}");
            }
        }

        // 快捷键已移到 RuntimeUI/DesktopStudioPanel.HandleShortcuts()：
        // 工程 activeInputHandler=1（纯新输入系统），Core 程序集不引用 Unity.InputSystem，
        // 此处调用旧 UnityEngine.Input 会每帧抛 InvalidOperationException。
        protected virtual void Update()
        {
            if (AutoSave != null && AutoSave.Tick(Time.deltaTime))
            {
                TriggerAutoSave();
            }
        }
    }
}
