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
                var dto = ShotPassportDto.FromShot(Latest, sceneId, Latest.recordedConstraints ?? Array.Empty<ConstraintSpec>());
                var path = ShotPassportStore.SaveNextRevision(dto, folder);
                OnAutoSaved?.Invoke(path);
                Debug.Log($"[ShotLibrary AutoSave] 自动保存成功: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ShotLibrary AutoSave] 自动保存异常: {ex.Message}");
            }
        }

        protected virtual void Update()
        {
            if (AutoSave != null && AutoSave.Tick(Time.deltaTime))
            {
                TriggerAutoSave();
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Z) && !Input.GetKey(KeyCode.LeftShift))
                {
                    Undo();
                }
                else if (Input.GetKeyDown(KeyCode.Y) || (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Z)))
                {
                    Redo();
                }
            }
        }
    }
}
