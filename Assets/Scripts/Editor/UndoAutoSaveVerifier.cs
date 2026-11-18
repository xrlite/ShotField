using System;
using UnityEngine;
using UnityEditor;
using ShotField.Core;

namespace ShotField.EditorTools
{
    public static class UndoAutoSaveVerifier
    {
        public static void Run()
        {
            int passCount = 0;
            int totalTests = 5;

            try
            {
                // Test 1: UndoStack 容量严格限制为 10 步，最老历史被安全挤出
                var stack = new UndoStack(10);
                for (int i = 1; i <= 12; i++)
                {
                    var shot = new ShotData
                    {
                        shotId = $"SC01-{i:D2}",
                        revision = i,
                        intent = $"意图版本 {i}",
                        camera = new ShotCameraState { position = new Vector3(0, 0, i) }
                    };
                    stack.Push(shot);
                }

                bool t1Ok = stack.UndoCount == 10 &&
                            stack.Capacity == 10 &&
                            stack.PeekOldestUndo() != null &&
                            stack.PeekOldestUndo().revision == 3 &&
                            stack.PeekUndo().revision == 12;

                if (t1Ok)
                {
                    passCount++;
                    Debug.Log($"[UndoAutoSaveVerifier] Test 1 PASS: UndoStack 写入 12 步严格上限 10 步 (当前数量={stack.UndoCount}, 最早保留 rev={stack.PeekOldestUndo().revision})");
                }
                else
                {
                    Debug.LogError($"[UndoAutoSaveVerifier] Test 1 FAIL: 容量限制异常 count={stack.UndoCount}");
                }

                // Test 2: Undo 恢复前一状态，并正确压入 Redo
                var currentShot = new ShotData
                {
                    shotId = "SC01-13",
                    revision = 13,
                    intent = "当前进行中版本 13",
                    camera = new ShotCameraState { position = new Vector3(0, 0, 13) }
                };

                var previousShot = stack.Undo(currentShot);
                bool t2Ok = previousShot != null &&
                            previousShot.revision == 12 &&
                            stack.UndoCount == 9 &&
                            stack.CanRedo &&
                            stack.RedoCount == 1;

                if (t2Ok)
                {
                    passCount++;
                    Debug.Log($"[UndoAutoSaveVerifier] Test 2 PASS: Undo 成功恢复前一状态 rev={previousShot.revision}, Redo 待命 count={stack.RedoCount}");
                }
                else
                {
                    Debug.LogError($"[UndoAutoSaveVerifier] Test 2 FAIL: Undo 状态不匹配");
                }

                // Test 3: Redo 恢复被撤销的状态
                var redoneShot = stack.Redo(previousShot);
                bool t3Ok = redoneShot != null &&
                            redoneShot.revision == 13 &&
                            stack.UndoCount == 10 &&
                            !stack.CanRedo &&
                            stack.RedoCount == 0;

                if (t3Ok)
                {
                    passCount++;
                    Debug.Log($"[UndoAutoSaveVerifier] Test 3 PASS: Redo 成功恢复撤销状态 rev={redoneShot.revision}, Redo 栈清空");
                }
                else
                {
                    Debug.LogError($"[UndoAutoSaveVerifier] Test 3 FAIL: Redo 状态不匹配");
                }

                // Test 4: AutoSave 未脏时不触发
                var autoSave = new AutoSaveService(30.0f);
                bool trigger1 = autoSave.Tick(15.0f);
                bool trigger2 = autoSave.Tick(20.0f); // 累计 35s > 30s，但未脏
                bool t4Ok = !trigger1 && !trigger2 && !autoSave.IsDirty;

                if (t4Ok)
                {
                    passCount++;
                    Debug.Log("[UndoAutoSaveVerifier] Test 4 PASS: 未标记脏时即使超过 30s 亦不触发自动保存");
                }
                else
                {
                    Debug.LogError("[UndoAutoSaveVerifier] Test 4 FAIL: 未脏状态意外触发保存");
                }

                // Test 5: AutoSave 标记脏后周期到达准确触发并消费脏标记
                autoSave.MarkDirty();
                bool preDirty = autoSave.IsDirty;
                bool trigger3 = autoSave.Tick(15.0f); // 15s 未达 30s 周期
                bool trigger4 = autoSave.Tick(15.1f); // 累计 30.1s >= 30s，且脏
                bool postDirty = autoSave.IsDirty;
                bool trigger5 = autoSave.Tick(35.0f); // 之后未再标记脏，不二次触发

                bool t5Ok = preDirty && !trigger3 && trigger4 && !postDirty && !trigger5;
                if (t5Ok)
                {
                    passCount++;
                    Debug.Log("[UndoAutoSaveVerifier] Test 5 PASS: 脏标记与 30s 周期判定触发准确，保存后脏标记自动消费");
                }
                else
                {
                    Debug.LogError($"[UndoAutoSaveVerifier] Test 5 FAIL: 脏标记周期触发异常 pre={preDirty}, t3={trigger3}, t4={trigger4}, post={postDirty}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UndoAutoSaveVerifier] EXCEPTION: {ex}");
            }

            if (passCount == totalTests)
            {
                Debug.Log($"UNDO-AUTOSAVE-RESULT=PASS ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"UNDO-AUTOSAVE-RESULT=FAIL ({passCount}/{totalTests})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
