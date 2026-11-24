#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 一键生成《桌上的信》测试场景。
    /// 目的：让约束检查有东西可测——不需要模型、不需要美术、不需要 VR。
    ///
    /// 场景内容：地板 / 桌子 / 主角A / 主角B / 信封（关键面不可见）
    /// 约束：可见性 + 越轴 + 构图
    /// </summary>
    public static class TestSceneBuilder
    {
        [MenuItem("ShotField/Scene/Build Test Scene (Unopened Letter)")]
        [MenuItem("ShotField/生成测试场景（桌上的信）")]
        public static void Build()
        {
            var root = new GameObject("ShotField_TestScene");
            Undo.RegisterCreatedObjectUndo(root, "生成测试场景");

            // ---------- 空间 ----------
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);   // 12m × 12m
            floor.transform.position = Vector3.zero;
            TagAsEnvironment(floor);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.SetParent(root.transform);
            table.transform.localScale = new Vector3(1.6f, 0.05f, 0.8f);  // 桌面
            table.transform.position = new Vector3(0f, 0.55f, 0f);        // 桌高 0.55m
            TagAsEnvironment(table);

            // ---------- 角色 ----------
            var actorA = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actorA.name = "ActorA_沈默";
            actorA.transform.SetParent(root.transform);
            actorA.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            actorA.transform.position = new Vector3(-1.1f, 0.9f, 0.6f);
            var markerA = actorA.AddComponent<SemanticMarker>();
            markerA.kind = SemanticMarker.Kind.ActorPrimary;
            markerA.markerId = "actorA";
            markerA.displayName = "沈默（主角A）";

            var actorB = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actorB.name = "ActorB_苏明";
            actorB.transform.SetParent(root.transform);
            actorB.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
            actorB.transform.position = new Vector3(1.1f, 0.9f, 0.6f);
            var markerB = actorB.AddComponent<SemanticMarker>();
            markerB.kind = SemanticMarker.Kind.ActorSecondary;
            markerB.markerId = "actorB";
            markerB.displayName = "苏明（主角B）";

            // ---------- 关键道具：信封 ----------
            var envelope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            envelope.name = "Envelope";
            envelope.transform.SetParent(root.transform);
            envelope.transform.localScale = new Vector3(0.16f, 0.011f, 0.11f);
            envelope.transform.position = new Vector3(0f, 0.583f, 0f);
            envelope.transform.rotation = Quaternion.Euler(0f, 180f, 0f);  // 正面朝 -Z（背向默认机位方向）

            var markerE = envelope.AddComponent<SemanticMarker>();
            markerE.kind = SemanticMarker.Kind.KeyProp;
            markerE.markerId = "envelope";
            markerE.displayName = "信封（关键道具）";
            markerE.criticalFaceNormal = Vector3.forward;   // 局部 +Z 即信封正面
            markerE.criticalFaceMustStayHidden = true;      // 关键面不可见
            markerE.criticalFaceSize = new Vector2(0.16f, 0.11f);

            // ---------- 系统 ----------
            var libGo = new GameObject("ShotLibrary");
            libGo.transform.SetParent(root.transform);
            var lib = libGo.AddComponent<ShotLibrary>();

            ConstraintHost(root, lib);

            // ---------- 相机 ----------
            var camGo = new GameObject("ShotCamera");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            // 初始机位：低机位，贴桌沿，看向信封侧面
            var camPos = new Vector3(-2.6f, 0.62f, -1.4f);
            var camTarget = new Vector3(0f, 0.55f, 0f);
            camGo.transform.position = camPos;
            camGo.transform.rotation = Quaternion.LookRotation(camTarget - camPos, Vector3.up);

            lib.ShotCamera = cam;

            Selection.activeGameObject = root;
            SceneView.RepaintAll();

            Debug.Log("[ShotField] 测试场景已生成。打开菜单「镜场 ShotField/镜头工作台」开始建镜头。");
        }

        private static void ConstraintHost(GameObject root, ShotLibrary lib)
        {
            var host = new GameObject("Constraints");
            host.transform.SetParent(root.transform);

            var vis = host.AddComponent<VisibilityConstraint>();
            vis.ruleName = "信封正面不可见";
            vis.targetMarkerId = "";        // 空 = 检查所有勾选对象
            vis.occluderMask = ~0;
            vis.blocking = true;
            vis.waivable = false;

            var axis = host.AddComponent<AxisConstraint>();
            axis.ruleName = "摄影机不得越轴";
            axis.actorAId = "actorA";
            axis.actorBId = "actorB";
            axis.blocking = false;          // 越轴是风险不是错误
            axis.waivable = true;

            var frame = host.AddComponent<FramingConstraint>();
            frame.ruleName = "主角A在画面左侧 1/3";
            frame.subjectId = "actorA";
            frame.minViewportX = 0f;
            frame.maxViewportX = 0.5f;
            frame.blocking = false;
            frame.waivable = true;
        }

        private static void TagAsEnvironment(GameObject go)
        {
            var m = go.AddComponent<SemanticMarker>();
            m.kind = SemanticMarker.Kind.Environment;
            m.markerId = go.name.ToLower();
            m.displayName = go.name;
        }
    }
}
#endif
