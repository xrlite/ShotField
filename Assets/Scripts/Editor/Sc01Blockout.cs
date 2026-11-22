#if UNITY_EDITOR
using ShotField.Constraints;
using ShotField.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShotField.EditorTools
{
    public static class Sc01Blockout
    {
        [MenuItem("ShotField/Blockout/Create SC01 Living Room Blockout")]
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

        /// <summary>无 GUI 版本：建白模并返回根节点。批处理调用方准备空场景并自行保存。</summary>
        internal static GameObject BuildIntoScene()
        {
            var root = new GameObject("SC01_Blockout");
            root.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
            BuildRoom(root.transform);
            BuildBlockoutProps(root.transform);
            BuildLighting(root.transform);
            BuildDirector(root.transform);
            return root;
        }

        [MenuItem("ShotField/Blockout/Rebuild SC01 Art Scene")]
        [MenuItem("镜场 ShotField/重建 SC01 美术场景")]
        public static void BuildAndSaveScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Sc01ArtAssetBuilder.BuildAll();
            var root = BuildIntoScene();
            // 先建工作台 Canvas 再装配 XRI：Canvas 射线组件要加在已存在的 Canvas 上
            SpatialWorkbenchBuilder.BuildWorkbench();
            SceneXriRigBuilder.BuildRig(root);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/SC01_Blockout.unity");
            Debug.Log("[SC01] 场景已更新并保存至 Assets/Scenes/SC01_Blockout.unity");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void BuildRoom(Transform room)
        {
            Box(room, "Floor", new Vector3(0f, -0.05f, 2f), new Vector3(6f, 0.1f, 4f));
            Box(room, "BackWall", new Vector3(0f, 1.4f, 4f), new Vector3(6f, 2.8f, 0.1f));
            Box(room, "LeftWall", new Vector3(-3f, 1.4f, 2f), new Vector3(0.1f, 2.8f, 4f));
            Box(room, "RightWall", new Vector3(3f, 1.4f, 2f), new Vector3(0.1f, 2.8f, 4f));
            // 门洞按 Blender 口径搬到左半区（门洞 X -2.65 .. -1.75，宽 0.9）。
            Box(room, "EntryWallLeft", new Vector3(-2.825f, 1.4f, 0f), new Vector3(0.35f, 2.8f, 0.1f));
            Box(room, "EntryWallRight", new Vector3(0.625f, 1.4f, 0f), new Vector3(4.75f, 2.8f, 0.1f));
            Box(room, "EntryLintel", new Vector3(-2.2f, 2.45f, 0f), new Vector3(0.9f, 0.7f, 0.1f));
            Box(room, "Ceiling", new Vector3(0f, 2.85f, 2f), new Vector3(6f, 0.1f, 4f));
            Box(room, "BackBaseboard", new Vector3(0f, 0.08f, 3.92f), new Vector3(5.8f, 0.16f, 0.05f));
            Box(room, "LeftBaseboard", new Vector3(-2.92f, 0.08f, 2f), new Vector3(0.05f, 0.16f, 3.8f));
            Box(room, "RightBaseboard", new Vector3(2.92f, 0.08f, 2f), new Vector3(0.05f, 0.16f, 3.8f));
            var door = Box(room, "Door", new Vector3(-1.75f, 1.05f, 0.45f), new Vector3(0.9f, 2.1f, 0.04f));
            door.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box(room, "WindowMarker", new Vector3(2.93f, 1.55f, 1.8f), new Vector3(0.04f, 1.2f, 1.6f));
            Box(room, "WindowFrameTop", new Vector3(2.89f, 2.18f, 1.8f), new Vector3(0.08f, 0.08f, 1.75f));
            Box(room, "WindowFrameBottom", new Vector3(2.89f, 0.92f, 1.8f), new Vector3(0.08f, 0.08f, 1.75f));
            Box(room, "WindowFrameFront", new Vector3(2.89f, 1.55f, 0.92f), new Vector3(0.08f, 1.34f, 0.08f));
            Box(room, "WindowFrameBack", new Vector3(2.89f, 1.55f, 2.68f), new Vector3(0.08f, 1.34f, 0.08f));
        }

        private static void BuildBlockoutProps(Transform room)
        {
            Sc01ArtSceneBuilder.BuildProps(room);
            // 冰箱在右墙后角（Blender 5.55/3.60 → Unity 2.55/3.60）。
            Box(room, "Fridge", new Vector3(2.55f, 0.9f, 3.6f), new Vector3(0.6f, 1.8f, 0.6f));
            // 半杯凉茶：放在茶几面上（Blender 1.10/2.30 → Unity -1.90/2.30）。
            float teaCenterY = Sc01ArtSceneBuilder.LastTableTopY + 0.045f;
            Mark(Primitive(room, PrimitiveType.Cylinder, "Tea", new Vector3(-1.9f, teaCenterY, 2.3f),
                new Vector3(0.08f, 0.045f, 0.08f)), "tea", SemanticMarker.Kind.Prop);
            // 女式单只拖鞋：玄关柜旁地面（Blender 0.62/0.52 → Unity -2.38/0.52）。
            Mark(Box(room, "Slipper", new Vector3(-2.38f, 0.028f, 0.52f), new Vector3(0.155f, 0.055f, 0.42f)),
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
            moonLight.intensity = 0.75f;
            moonLight.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(25f, -115f, 0f);

            // 2. 茶几上方局部暖光（点光源：暖部提橙，高光打在信封与茶几焦点）
            var lamp = new GameObject("TableWarmLight");
            lamp.transform.SetParent(room, false);
            lamp.transform.localPosition = new Vector3(1.15f, 2.35f, 1.45f);
            lamp.transform.LookAt(Sc01ArtSceneBuilder.EnvelopePosition);
            var lampLight = lamp.AddComponent<Light>();
            lampLight.type = LightType.Spot;
            lampLight.color = new Color(1.0f, 0.72f, 0.40f);
            lampLight.range = 4.0f;
            lampLight.spotAngle = 58f;
            lampLight.intensity = 3.2f;
            lampLight.shadows = LightShadows.Soft;
        }

        private static void BuildDirector(Transform room)
        {
            // 1. 头显观察机位（MainCamera：负责 VR 空间眼部渲染与 6DoF 跟踪）
            var hmd = new GameObject("XR_Headset");
            hmd.transform.SetParent(room, false);
            hmd.transform.position = new Vector3(2.7f, 1.48f, 0.35f);
            hmd.transform.LookAt(new Vector3(1.0f, 0.72f, 2.35f));
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
            FrameLetter(camera);
            camera.aspect = ShotCameraState.DefaultAspect;
            camera.nearClipPlane = 0.05f;

            var visibility = director.AddComponent<VisibilityConstraint>();
            visibility.ruleName = "信封正面不可见";
            visibility.targetMarkerId = "envelope";
        }

        private static void FrameLetter(Camera camera)
        {
            camera.fieldOfView = 65f;
            camera.transform.position = new Vector3(1.00f, 0.442f, 0.94f);
            camera.transform.LookAt(new Vector3(1.00f, 0.84f, 2.32f));
        }

        public static void SaveDirectorFraming()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SC01_Blockout.unity");
            FrameLetter(GameObject.Find("Director").GetComponent<Camera>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException(scene.path);
            Debug.Log("SC01-DIRECTOR-SAVED=" + scene.path);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static SemanticMarker Mark(GameObject obj, string id, SemanticMarker.Kind kind)
        {
            var m = obj.AddComponent<SemanticMarker>();
            m.markerId = id; m.displayName = obj.name; m.kind = kind;
            return m;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size) =>
            Primitive(parent, PrimitiveType.Cube, name, position, size);

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name,
            Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = size;
            if (name is "Envelope_Body" or "Envelope_Flap" or "Letter_Edge" or "WindowMarker")
                obj.GetComponent<Collider>().enabled = false;
            return obj;
        }
    }
}
#endif
