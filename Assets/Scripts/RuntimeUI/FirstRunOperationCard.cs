using ShotField.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 3 步真实交互轻引导：悬浮在沙盘上方 1.15m（+12° 舒适视区，不挡中心沙盘），
    /// 跟随实际动作自动打钩推进，右上角常驻「跳过引导」，完成后自动收起。
    /// </summary>
    public sealed class FirstRunOperationCard : MonoBehaviour
    {
        private const string SeenKey = "ShotField.FirstRunCardSeen.v1";
        private static readonly Color Glass = new Color(0.96f, 0.93f, 0.86f, 0.68f);
        private static readonly Color Gold = new Color(0.45f, 0.32f, 0.13f, 1f);
        private static readonly Color DoneColor = new Color(0.16f, 0.45f, 0.24f, 1f);
        private static readonly Color DimColor = new Color(0.16f, 0.15f, 0.14f, 1f);

        private readonly TMP_Text[] _stepLabels = new TMP_Text[3];
        private readonly bool[] _done = new bool[3];
        private readonly string[] _stepTexts =
        {
            "1. 沙盘排走位：拖动 ActorA 调整站位",
            "2. 一键进片场：点下方「进入 1:1 片场」",
            "3. 演一镜/拍一镜：左手掌心点「上场演这镜」"
        };

        private Transform _head;
        private Transform _actorA;
        private Vector3 _actorStartPos;
        private bool _hasActorStart;
        private TabletopDirector _tabletop;
        private ActorPossessionController _possession;
        private ShotLibrary _shotLib;
        private int _initialShotCount;
        private float _completeTimer = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EditorCameraFallback.Ensure();
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            if (FindAnyObjectByType<FirstRunOperationCard>(FindObjectsInactive.Include) != null) return;
            if (Camera.main == null) return;

            var go = new GameObject("FirstRunOperationCard",
                typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            go.AddComponent<FirstRunOperationCard>().Build(Camera.main);
        }

        private void Build(Camera cam)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            _head = cam.transform;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(560f, 220f);
            rt.localScale = Vector3.one * 0.001f;

            var glass = RuntimeUiFactory.CreatePanel(rt, "Glass", Glass, Vector2.zero, rt.sizeDelta);
            SpatialGlass.Apply(glass.GetComponent<Image>(), Glass, 32f);
            glass.GetComponent<Image>().raycastTarget = false;
            RuntimeUiFactory.CreateTmpLabel(rt, "Title", "镜场 - 快速上手 (3 步)", 22,
                new Vector2(-60f, 76f), new Vector2(380f, 34f), Gold, FontStyle.Bold);

            CreateSkipButton(rt, new Vector2(205f, 76f), new Vector2(110f, 32f));

            for (int i = 0; i < 3; i++)
            {
                _stepLabels[i] = RuntimeUiFactory.CreateTmpLabel(rt, "Step_" + i, "[ ] " + _stepTexts[i],
                    17, new Vector2(0f, 22f - i * 42f), new Vector2(510f, 34f), DimColor);
            }

            _shotLib = FindAnyObjectByType<ShotLibrary>();
            _initialShotCount = _shotLib != null ? _shotLib.Count : 0;
            PlaceAboveTabletop();
        }

        private void CreateSkipButton(RectTransform root, Vector2 pos, Vector2 size)
        {
            var go = RuntimeUiFactory.CreatePanel(root, "SkipButton", new Color(0.24f, 0.27f, 0.32f, 0.88f), pos, size);
            SpatialGlass.Apply(go.GetComponent<Image>(), new Color(0.24f, 0.27f, 0.32f, 0.88f), 20f);
            var btn = go.AddComponent<Button>();
            RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", "跳过引导", 14, Vector2.zero, size, Color.white);
            btn.onClick.AddListener(OnSkipClicked);
            go.AddComponent<UguiHoverEffect>();
        }

        private void OnSkipClicked()
        {
            Debug.Log("[UI-CLICK] FirstRunOperationCard.SkipButton clicked");
            CompleteAndClose();
        }

        private void Update()
        {
            if (_head == null || _head.position.y < 0.30f)
            {
                var cam = Camera.main;
                if (cam != null) _head = cam.transform;
            }
            if (_tabletop == null) _tabletop = FindAnyObjectByType<TabletopDirector>();
            // Unity 重载了 ==，但 ?? 走真正的 null 判断：GetComponent 拿到的
            // "伪 null" 会被当成非空，随后 .alpha 每帧抛 MissingComponentException。
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            bool yieldToAi = XrAiConnectionView.IsAnyPanelOpen || WelcomeHubOverlay.IsOpen;
            // 引导只在沙盘态出现：1:1 首帧要让开茶几与信封，不能横在拍摄视野正中。
            bool tabletopMode = _tabletop == null ||
                                _tabletop.currentMode == TabletopDirector.ScaleMode.TabletopMini;
            bool visible = !yieldToAi && tabletopMode;
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
            if (_completeTimer >= 0f)
            {
                _completeTimer += Time.deltaTime;
                if (_completeTimer >= 1.5f) CompleteAndClose();
                return;
            }

            CheckStep1ActorMoved();
            CheckStep2Entered1to1();
            CheckStep3Action();

            if (_done[0] && _done[1] && _done[2]) _completeTimer = 0f;
            PlaceAboveTabletop();
        }

        private void CheckStep1ActorMoved()
        {
            if (_done[0]) return;
            if (_actorA == null)
            {
                var go = GameObject.Find("ActorA");
                if (go != null) { _actorA = go.transform; _actorStartPos = _actorA.position; _hasActorStart = true; }
            }
            if (_hasActorStart && _actorA != null && Vector3.Distance(_actorA.position, _actorStartPos) > 0.08f)
                MarkDone(0);
        }

        private void CheckStep2Entered1to1()
        {
            if (_done[1]) return;
            if (_tabletop == null) _tabletop = FindAnyObjectByType<TabletopDirector>();
            if (_tabletop != null && _tabletop.currentMode == TabletopDirector.ScaleMode.Immersive1to1 && _done[0])
                MarkDone(1);
        }

        private void CheckStep3Action()
        {
            if (_done[2]) return;
            if (_possession == null) _possession = FindAnyObjectByType<ActorPossessionController>();
            if (_shotLib == null) _shotLib = FindAnyObjectByType<ShotLibrary>();
            int curShots = _shotLib != null ? _shotLib.Count : 0;
            if ((_possession != null && _possession.IsRecording) || curShots > _initialShotCount)
                MarkDone(2);
        }

        private void MarkDone(int index)
        {
            _done[index] = true;
            if (_stepLabels[index] != null)
            {
                _stepLabels[index].text = "[v] " + _stepTexts[index];
                _stepLabels[index].color = DoneColor;
            }
        }

        private void PlaceAboveTabletop()
        {
            if (_head == null) return;
            Vector3 fwd = new Vector3(_head.forward.x, 0f, _head.forward.z);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 target = _head.position + fwd.normalized * 1.35f + Vector3.up * 0.34f;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 6f);
            Vector3 look = transform.position - _head.position;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private void CompleteAndClose()
        {
            Debug.Log("[UI-CLICK] FirstRunOperationCard.CompleteAndClose");
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            Destroy(gameObject);
        }
    }
}
