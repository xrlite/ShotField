#if UNITY_EDITOR
using ShotField.Constraints;
using ShotField.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
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

            var root = BuildIntoScene();
            Undo.RegisterCreatedObjectUndo(root, "创建 SC01 客厅白模");
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 无 GUI 版本：建白模并返回根节点。批处理（-executeMethod）走这条路径，
        /// 调用方负责先准备空场景，并自行决定是否保存。
        /// </summary>
        internal static GameObject BuildIntoScene()
        {
            var root = new GameObject("SC01_Blockout");
            root.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
            BuildRoom(root.transform);
            BuildProps(root.transform);
            BuildLighting(root.transform);
            BuildDirector(root.transform);
            return root;
        }

        [MenuItem("镜场 ShotField/重建 SC01 美术场景")]
        public static void BuildAndSaveScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Sc01ArtAssetBuilder.BuildAll();
            BuildIntoScene();
            SpatialWorkbenchBuilder.BuildWorkbench();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/SC01_Blockout.unity");
            Debug.Log("[SC01] 场景已更新并保存至 Assets/Scenes/SC01_Blockout.unity");
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
            Sc01ArtSceneBuilder.BuildProps(room);
            Box(room, "Fridge", new Vector3(-0.6f, 0.9f, 3.6f), new Vector3(0.6f, 1.8f, 0.6f));
            Mark(Primitive(room, PrimitiveType.Cylinder, "Tea", new Vector3(-0.25f, 0.82f, 1.32f),
                new Vector3(0.08f, 0.045f, 0.08f)), "tea", SemanticMarker.Kind.Prop);
            Mark(Box(room, "Slipper", new Vector3(0.55f, 0.05f, 0.45f), new Vector3(0.085f, 0.045f, 0.24f)),
                "slipper", SemanticMarker.Kind.Prop);
        }

        private static void BuildLighting(Transform room)
        {
            // 1. 落地窗冷月光（方向光：暗部压青，建立悬疑夜戏大基调）
            var moon = new GameObject("WindowMoonlight");
            moon.transform.SetParent(room, false);
            var moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.68f, 0.82f, 1.0f);
            moonLight.intensity = 1.2f;
            moonLight.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(25f, -115f, 0f);

            // 2. 茶几上方局部暖光（点光源：暖部提橙，高光打在信封与茶几焦点）
            var lamp = new GameObject("TableWarmLight");
            lamp.transform.SetParent(room, false);
            lamp.transform.localPosition = new Vector3(1.2f, 1.25f, 2.2f);
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1.0f, 0.72f, 0.40f);
            lampLight.range = 3.2f;
            lampLight.intensity = 1.8f;
            lampLight.shadows = LightShadows.Soft;
        }

        private static void BuildDirector(Transform room)
        {
            // 1. 头显观察机位（MainCamera：负责 VR 空间眼部渲染与 6DoF 跟踪）
            var hmd = new GameObject("XR_Headset");
            hmd.transform.SetParent(room, false);
            hmd.transform.position = new Vector3(0.5f, 1.4f, 0.6f);
            hmd.transform.LookAt(Sc01ArtSceneBuilder.EnvelopePosition);
            var hmdCam = hmd.AddComponent<Camera>();
            hmdCam.tag = "MainCamera";
            hmdCam.nearClipPlane = 0.05f;
            hmdCam.farClipPlane = 100f;
            hmd.AddComponent<AudioListener>();

            var poseDriver = hmd.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            poseDriver.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
            poseDriver.positionInput = new UnityEngine.InputSystem.InputActionProperty(
                new UnityEngine.InputSystem.InputAction("Position", binding: "<XRHMD>/centerEyePosition"));
            poseDriver.rotationInput = new UnityEngine.InputSystem.InputActionProperty(
                new UnityEngine.InputSystem.InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation"));

            // 2. 导演电影机位（Director：片场机位，由工作台/护照驱动，离线渲染至监视器）
            var director = new GameObject("Director");
            director.transform.SetParent(room, false);
            var library = director.AddComponent<ShotLibrary>();
            var camera = director.AddComponent<Camera>();
            camera.enabled = false; // 严守单活动主相机，避免抢占 VR 视口
            library.ShotCamera = camera;
            camera.fieldOfView = 45f;
            camera.aspect = ShotCameraState.DefaultAspect;
            camera.nearClipPlane = 0.05f;
            director.transform.position = Sc01ArtSceneBuilder.EnvelopePosition + new Vector3(0.85f, -0.26f, -0.95f);
            director.transform.LookAt(Sc01ArtSceneBuilder.EnvelopePosition);

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
            // The envelope root owns its collider. Trim and the window marker are visual only.
            if (name is "Envelope_Body" or "Envelope_Flap" or "Letter_Edge" or "WindowMarker")
                obj.GetComponent<Collider>().enabled = false;
            return obj;
        }
    }
}
#endif
