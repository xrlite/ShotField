#if UNITY_EDITOR
using System;
using System.IO;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class ShotLibraryRuntimeVerifier
    {
        // 断言总数必须与下方 Assert 调用条数一致；新增断言时同步改这里。
        private const int ExpectedAssertions = 10;

        [MenuItem("镜场 ShotField/验证/验证 ShotLibrary 运行时撤销与自动保存")]
        public static void Run()
        {
            var go = new GameObject("Test_ShotLibrary_Host");
            var lib = go.AddComponent<ShotLibrary>();
            int passCount = 0;

            void Assert(string name, bool condition)
            {
                if (condition)
                {
                    passCount++;
                    Debug.Log($"[ShotLibraryRuntimeVerifier] PASS: {name}");
                }
                else
                {
                    Debug.LogError($"[ShotLibraryRuntimeVerifier] FAIL: {name}");
                    throw new Exception("Assertion failed: " + name);
                }
            }

            try
            {
                // Test 1: 运行时引用与初始化
                Assert("UndoStack 已挂载且容量为 10", lib.UndoStack != null && lib.UndoStack.Capacity == 10);
                Assert("AutoSaveService 已挂载且初始周期为 30s", lib.AutoSave != null && Math.Abs(lib.AutoSave.Interval - 30f) < 0.001f);

                // Test 2: 创建镜头自动压入 UndoStack 并标脏
                var camObj = new GameObject("Test_Cam");
                var cam = camObj.AddComponent<Camera>();
                lib.ShotCamera = cam;

                var s01 = lib.CreateShot(SceneCaptureMode.Empty, "Initial Shot");
                Assert("创建镜头后已记入 UndoStack", lib.UndoStack.UndoCount > 0);
                Assert("创建镜头后 AutoSave 处于脏状态", lib.AutoSave.IsDirty);

                // Test 3: 机位修改与撤销
                cam.transform.position = new Vector3(10f, 20f, 30f);
                lib.UpdateCameraOf(s01);
                Assert("更新机位后撤销栈计数增加", lib.UndoStack.UndoCount >= 2);

                bool undoSuccess = lib.Undo();
                Assert("执行 Undo 成功", undoSuccess);
                Assert("Undo 后机位状态已物理还原", cam.transform.position != new Vector3(10f, 20f, 30f));

                // Test 4: 重做 Redo
                bool redoSuccess = lib.Redo();
                Assert("执行 Redo 成功", redoSuccess);
                Assert("Redo 后机位恢复为修改后的坐标", cam.transform.position == new Vector3(10f, 20f, 30f));

                // Test 5: 定时自动保存触发
                lib.AutoSave.ResetTimer();
                lib.AutoSave.MarkDirty();
                bool triggered = lib.AutoSave.Tick(30.1f);
                Assert("步进 30s 且有脏标记时正确触发自动保存事件", triggered && !lib.AutoSave.IsDirty);

                if (passCount != ExpectedAssertions)
                {
                    Debug.LogError($"SHOTLIBRARY-RUNTIME-RESULT=FAIL 断言计数不符 ({passCount}/{ExpectedAssertions})");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"SHOTLIBRARY-RUNTIME-RESULT=PASS ({passCount}/{ExpectedAssertions})");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
