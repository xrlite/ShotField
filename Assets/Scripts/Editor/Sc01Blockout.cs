#if UNITY_EDITOR
using ShotField.Constraints;
using ShotField.Core;
using UnityEditor;
using UnityEngine;

namespace ShotField.EditorTools
{
    internal static class Sc01Blockout
    {
        [MenuItem("镜场 ShotField/创建 SC01 客厅白模")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<ShotLibrary>() != null)
            {
                EditorUtility.DisplayDialog("创建 SC01", "请在空场景里运行此命令。", "关闭");
                return;
            }

            var root = new GameObject("SC01_Blockout");
            Undo.RegisterCreatedObjectUndo(root, "创建 SC01 客厅白模");
            BuildRoom(root.transform);
            BuildProps(root.transform);
            BuildDirector(root.transform);
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
        }

        private static void BuildRoom(Transform room)
        {
            Box(room, "Floor", new Vector3(0f, -0.05f, 2f), new Vector3(6f, 0.1f, 4f));
            Box(room, "BackWall", new Vector3(0f, 1.4f, 4f), new Vector3(6f, 2.8f, 0.1f));
            Box(room, "LeftWall", new Vector3(-3f, 1.4f, 2f), new Vector3(0.1f, 2.8f, 4f));
            Box(room, "RightWall", new Vector3(3f, 1.4f, 2f), new Vector3(0.1f, 2.8f, 4f));
            Box(room, "EntryWallLeft", new Vector3(-1.325f, 1.4f, 0f), new Vector3(3.35f, 2.8f, 0.1f));
            Box(room, "EntryWallRight", new Vector3(2.125f, 1.4f, 0f), new Vector3(1.75f, 2.8f, 0.1f));
            Box(room, "EntryLintel", new Vector3(0.8f, 2.45f, 0f), new Vector3(0.9f, 0.7f, 0.1f));
            var door = Box(room, "Door", new Vector3(1.25f, 1.05f, 0.45f), new Vector3(0.9f, 2.1f, 0.04f));
            door.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box(room, "WindowMarker", new Vector3(2.93f, 1.55f, 1.8f), new Vector3(0.04f, 1.2f, 1.6f));
        }

        private static void BuildProps(Transform room)
        {
            Box(room, "ShoeCabinet", new Vector3(0.2f, 0.45f, 0.5f), new Vector3(1f, 0.9f, 0.35f));
            Box(room, "Sofa", new Vector3(0f, 0.425f, 3.2f), new Vector3(2f, 0.85f, 0.8f));
            Box(room, "CoffeeTable", new Vector3(1.4f, 0.225f, 2.2f), new Vector3(1.2f, 0.45f, 0.6f));
            Box(room, "Fridge", new Vector3(-0.6f, 0.9f, 3.6f), new Vector3(0.6f, 1.8f, 0.6f));

            var actor = Primitive(room, PrimitiveType.Capsule, "ActorA", new Vector3(0.8f, 0.86f, 1.25f),
                new Vector3(0.46f, 0.86f, 0.46f));
            Mark(actor, "actorA", SemanticMarker.Kind.ActorPrimary);

            var envelope = Box(room, "Envelope", new Vector3(1.5f, 0.524f, 2.15f), new Vector3(0.01f, 0.148f, 0.21f));
            var marker = Mark(envelope, "envelope", SemanticMarker.Kind.KeyProp);
            marker.criticalFaceNormal = Vector3.left;
            marker.criticalFaceSize = new Vector2(0.21f, 0.148f);
            marker.criticalFaceMustStayHidden = true;

            Mark(Primitive(room, PrimitiveType.Cylinder, "Tea", new Vector3(1.1f, 0.495f, 2.3f),
                new Vector3(0.08f, 0.045f, 0.08f)), "tea", SemanticMarker.Kind.Prop);
            Mark(Box(room, "Slipper", new Vector3(0.55f, 0.05f, 0.45f), new Vector3(0.085f, 0.045f, 0.24f)),
                "slipper", SemanticMarker.Kind.Prop);
        }

        private static void BuildDirector(Transform room)
        {
            var director = new GameObject("Director");
            director.transform.SetParent(room, false);
            var library = director.AddComponent<ShotLibrary>();
            var camera = director.AddComponent<Camera>();
            library.ShotCamera = camera;
            camera.fieldOfView = 40f;
            camera.aspect = ShotCameraState.DefaultAspect;
            camera.nearClipPlane = 0.05f;
            director.transform.position = new Vector3(2.35f, 0.55f, 1.2f);
            director.transform.LookAt(new Vector3(1.5f, 0.52f, 2.15f));

            var visibility = director.AddComponent<VisibilityConstraint>();
            visibility.ruleName = "信封正面不可见";
            visibility.targetMarkerId = "envelope";
        }

        private static SemanticMarker Mark(GameObject obj, string id, SemanticMarker.Kind kind)
        {
            var marker = obj.AddComponent<SemanticMarker>();
            marker.markerId = id;
            marker.displayName = obj.name;
            marker.kind = kind;
            return marker;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size) =>
            Primitive(parent, PrimitiveType.Cube, name, position, size);

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name,
            Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            return obj;
        }
    }
}
#endif
