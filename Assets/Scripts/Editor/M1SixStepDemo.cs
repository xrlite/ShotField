#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ShotField.Core;
using ShotField.RuntimeUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.EditorTools
{
    /// <summary>
    /// M1 六步连续演示驱动器：只调真实 UI/场景 API，不用 OS 鼠标合成。
    /// 运行前先进入 Play Mode；本类通过 <see cref="ShotFieldRecorder"/> 自动录制 Game View。
    /// </summary>
    public static class M1SixStepDemo
    {
        private const string ScenePath = BuildAndroid.ScenePath;
        private const string SaveEnv = "SHOTFIELD_SAVE_DIR";
        private static string _saveDir;
        private static int _step;
        private static double _nextAt;
        private static ActorNavigator _actor;
        private static PropBrowserUI _browser;
        private static DesktopStudioEditView _editView;
        private static GameObject _prop;
        private static Vector3 _actorSaved;
        private static Vector3 _propSaved;
        private static RecorderController _recorder;
        private static string _outputPath;

        [MenuItem("ShotField/Demo/Run M1 Six Step")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[M1-DEMO] 已在 Play Mode，请先退出。");
                return;
            }

            _saveDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "05_验证记录", "2026-11", "M1六步存档_20261124"));
            Directory.CreateDirectory(_saveDir);
            Environment.SetEnvironmentVariable(SaveEnv, _saveDir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            DevToolPanels.LegacyDesktopVisible = true;
            EditorApplication.isPlaying = true;
            _step = 0;
            _nextAt = EditorApplication.timeSinceStartup + 2.0;
            EditorApplication.update += Tick;
            Debug.Log("[M1-DEMO] START：已请求 Play Mode，随后启动 Recorder。");
        }

        private static void StartRecorder()
        {
            _outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..",
                "05_验证记录", "2026-11", "20261124_M1六步操作_v02.mp4"));
            _recorder = ShotFieldRecorder.Start(_outputPath);
            Debug.Log("[M1-DEMO] RECORDER=" + _outputPath);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Finish(false, "Play Mode 被提前停止");
                return;
            }
            if (EditorApplication.timeSinceStartup < _nextAt) return;
            try
            {
                ExecuteStep();
            }
            catch (Exception ex)
            {
                Finish(false, ex.ToString());
            }
        }

        private static void ExecuteStep()
        {
            switch (_step)
            {
                case 0:
                    StartRecorder();
                    _browser = UnityEngine.Object.FindAnyObjectByType<PropBrowserUI>();
                    _editView = UnityEngine.Object.FindAnyObjectByType<DesktopStudioEditView>();
                    if (_browser == null || _editView == null)
                        throw new InvalidOperationException("场景缺 PropBrowserUI 或 DesktopStudioEditView");
                    Debug.Log("[M1-DEMO] 1/6 已打开主场景 " + ScenePath);
                    break;

                case 1:
                    var spawn = GameObject.Find("Spawn_actorYellow");
                    var button = spawn != null ? spawn.GetComponent<Button>() : null;
                    if (button != null) button.onClick.Invoke();
                    else _browser.ActivateActor("actorYellow");
                    _actor = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                        .FirstOrDefault(n => n.name == "PrevisCast_C_Yellow");
                    if (_actor == null || !_actor.gameObject.activeInHierarchy)
                        throw new InvalidOperationException("演员放行失败");
                    ActorSelection.Select(_actor);
                    Debug.Log("[M1-DEMO] 2/6 已从资源列表加入演员");
                    break;

                case 2:
                    if (!_actor.MoveTo(new Vector3(0.8f, 0f, 0.6f), false))
                        throw new InvalidOperationException("NavMesh 走位失败");
                    Debug.Log("[M1-DEMO] 3/6 演员正在走到导航落点");
                    break;

                case 3:
                    if (!_editView.PlacePropNow("envelope", new Vector3(0.85f, 0f, 2.42f)))
                        throw new InvalidOperationException("信封 prefab 未绑定或未登记");
                    _prop = UnityEngine.Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Exclude)
                        .Select(m => m.gameObject)
                        .FirstOrDefault(go => go.name.StartsWith("envelope"));
                    if (_prop == null) throw new InvalidOperationException("找不到已放置的信封");
                    _prop.transform.position = new Vector3(1.15f, 0f, 2.30f);
                    Debug.Log("[M1-DEMO] 4/6 已添加并移动信封");
                    break;

                case 4:
                    _actorSaved = _actor.transform.position;
                    _propSaved = _prop.transform.position;
                    _editView.SaveSnapshotNow();
                    Debug.Log("[M1-DEMO] 5/6 已保存场景快照");
                    break;

                case 5:
                    _actor.transform.position = new Vector3(-1.0f, 0.05f, 1.4f);
                    _prop.transform.position = new Vector3(-0.5f, 0f, 2.8f);
                    _editView.RestoreFromDisk();
                    var restoredProp = UnityEngine.Object.FindObjectsByType<SemanticMarker>(FindObjectsInactive.Exclude)
                        .Select(m => m.gameObject)
                        .FirstOrDefault(go => go.name.StartsWith("envelope"));
                    bool actorOk = Vector3.Distance(_actor.transform.position, _actorSaved) < 0.001f;
                    bool propOk = restoredProp != null &&
                                  Vector3.Distance(restoredProp.transform.position, _propSaved) < 0.001f;
                    Finish(actorOk && propOk,
                        $"actorDelta={Vector3.Distance(_actor.transform.position, _actorSaved):F4} propDelta={(restoredProp != null ? Vector3.Distance(restoredProp.transform.position, _propSaved) : -1f):F4}");
                    return;
            }

            _step++;
            _nextAt = EditorApplication.timeSinceStartup + (_step == 3 ? 7.0 : 3.0);
        }

        private static void Finish(bool pass, string detail)
        {
            EditorApplication.update -= Tick;
            ShotFieldRecorder.Stop(_recorder);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Environment.SetEnvironmentVariable(SaveEnv, null);
            Debug.Log($"[M1-DEMO] {(pass ? "PASS" : "FAIL")} {detail}");
            Debug.Log($"M1-SIXSTEP-DEMO={(pass ? "PASS" : "FAIL")} (6/6)");
            Debug.Log("[M1-DEMO] MP4=" + _outputPath);
        }
    }
}
#endif
