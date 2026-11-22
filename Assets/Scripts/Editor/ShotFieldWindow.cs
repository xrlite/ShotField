#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ShotField.Core;
using ShotField.Constraints;

namespace ShotField.EditorTools
{
    /// <summary>
    /// 镜头工作台（编辑器内）。
    /// 这是 PC 端最小闭环的第一版：建镜头、继承、应用、跑约束检查。
    /// 不依赖 VR、不依赖 PICO SDK、不依赖模拟器。
    /// </summary>
    public class ShotFieldWindow : EditorWindow
    {
        private ShotLibrary library;
        private readonly ShotListPanel shotList = new ShotListPanel();
        private readonly ShotCheckPanel shotChecks = new ShotCheckPanel();
        private string newShotIntent = "";
        private bool inheritFromPrevious = true;
        private int selectedIndex = -1;

        [MenuItem("ShotField/Windows/Shot Workbench")]
        [MenuItem("镜场 ShotField/镜头工作台")]
        public static void Open()
        {
            var w = GetWindow<ShotFieldWindow>("镜场 · 镜头工作台");
            w.minSize = new Vector2(560f, 520f);
            w.Show();
        }

        private void OnEnable()
        {
            RescanLibrary();
        }

        private void RescanLibrary()
        {
            library = Object.FindAnyObjectByType<ShotLibrary>();
            if (library != null && library.ShotCamera == null)
            {
                var main = Camera.main;
                if (main != null) library.ShotCamera = main;
            }
        }

        private void OnGUI()
        {
            DrawHeader();

            if (library == null)
            {
                EditorGUILayout.HelpBox(
                    "场景里还没有 ShotLibrary。\n\n" +
                    "1. 新建一个空物体，命名 ShotLibrary\n" +
                    "2. 挂上 ShotLibrary 组件，把主相机拖进 ShotCamera 字段",
                    MessageType.Warning);
                if (GUILayout.Button("在场景中自动创建")) AutoCreate();
                return;
            }

            DrawCreateSection();
            ShotPassportPanel.Draw(library, CurrentSelected(), FindObjects());
            var activeCamera = library.ShotCamera != null ? library.ShotCamera : Camera.main;
            using (new EditorGUI.DisabledScope(activeCamera == null))
                if (GUILayout.Button("导出当前画面的白模 PNG")) ClayFrameExport.Save(activeCamera);
            EditorGUILayout.Space(6f);
            selectedIndex = shotList.Draw(library, selectedIndex);
            EditorGUILayout.Space(6f);
            shotChecks.Draw(library, CurrentSelected(), FindObjects());
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("镜头意图的约束与验收层", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("PC 端最小闭环 v0.1 —— 建镜头 / 继承 / 约束检查", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新扫描场景", GUILayout.Width(120f))) RescanLibrary();
                if (library != null)
                {
                    EditorGUILayout.LabelField(
                        string.Format("镜头数：{0}　标记对象：{1}　约束规则：{2}",
                            library.Count,
                            SemanticMarker.All().Length,
                            FindObjects().Length),
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.Space(4f);
        }

        private void DrawCreateSection()
        {
            EditorGUILayout.LabelField("新建镜头", EditorStyles.boldLabel);

            newShotIntent = EditorGUILayout.TextField("导演意图", newShotIntent);
            inheritFromPrevious = EditorGUILayout.Toggle("继承上一镜的场景（只换机位）", inheritFromPrevious);

            var camera = library.ShotCamera != null ? library.ShotCamera : Camera.main;
            if (camera != null)
            {
                int current = Mathf.Approximately(camera.aspect, ShotCameraState.DefaultAspect) ? 0
                    : Mathf.Approximately(camera.aspect, ShotCameraState.PortraitAspect) ? 1 : -1;
                int selected = EditorGUILayout.Popup("机位画幅", current, new[] { "16:9 横屏", "9:16 竖屏" });
                if (selected >= 0 && selected != current)
                {
                    Undo.RecordObject(camera, "修改机位画幅");
                    camera.aspect = selected == 1 ? ShotCameraState.PortraitAspect : ShotCameraState.DefaultAspect;
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从当前场景新建镜头", GUILayout.Height(24f)))
                {
                    Undo.RegisterCompleteObjectUndo(library, "创建镜头");
                    library.CreateShot(inheritFromPrevious ? SceneCaptureMode.InheritLatestOrCapture : SceneCaptureMode.Current, newShotIntent);
                    selectedIndex = library.Count - 1;
                    newShotIntent = "";
                    EditorUtility.SetDirty(library);
                }

                if (GUILayout.Button("把当前机位写回选中镜头", GUILayout.Height(24f)))
                {
                    var shot = CurrentSelected();
                    if (shot != null)
                    {
                        Undo.RegisterCompleteObjectUndo(library, "写回镜头机位");
                        library.UpdateCameraOf(shot);
                        EditorUtility.SetDirty(library);
                    }
                }
            }
        }

        private ShotConstraint[] FindObjects()
        {
            return Object.FindObjectsByType<ShotConstraint>(FindObjectsInactive.Exclude);
        }

        private ShotData CurrentSelected()
        {
            if (library == null) return null;
            if (selectedIndex < 0 || selectedIndex >= library.Count) return null;
            return library.Shots[selectedIndex];
        }

        private void AutoCreate()
        {
            var go = new GameObject("ShotLibrary");
            library = go.AddComponent<ShotLibrary>();
            var cam = Camera.main;
            if (cam != null) library.ShotCamera = cam;
            Undo.RegisterCreatedObjectUndo(go, "创建 ShotLibrary");
            Selection.activeGameObject = go;
        }


    }
}
#endif
