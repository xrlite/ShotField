using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 首屏操作卡：首次进入给一张三步卡，明确"看沙盘 → 定机位 → 存镜头护照"。
    /// 运行时装配、零场景写入；TMP 走 RuntimeUiFactory 的中文 SDF；按确认后不再出现。
    /// P1 修订：水平 FOV 修正、贴脸切紧凑版、按钮深字 16px、提示与按钮留白 13px、
    /// 底部留白 ≥17px，按钮接进统一 hover 曲线。
    /// </summary>
    public sealed class FirstRunOperationCard : MonoBehaviour
    {
        private const string SeenKey = "ShotField.FirstRunCardSeen.v1";
        private const float Distance = 0.75f;
        private const float MinComfortDistance = 0.55f;
        private const float FillRatio = 0.82f;
        private const float ButtonHeight = 46f;
        private static readonly Vector2 FullSize = new Vector2(620f, 460f);
        private static readonly Vector2 CompactSize = new Vector2(440f, 280f);
        private static readonly Color Glass = new Color(0.11f, 0.12f, 0.15f, 0.94f);
        private static readonly Color Gold = new Color(0.79f, 0.66f, 0.38f, 1f);
        private static readonly Color Ink = new Color(0.10f, 0.11f, 0.14f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            if (Object.FindAnyObjectByType<FirstRunOperationCard>(FindObjectsInactive.Include) != null) return;

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[FirstRunOperationCard] 没有 Camera.main，首屏卡未生成。");
                return;
            }

            var go = new GameObject("FirstRunOperationCard",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.position = camera.transform.position;
            go.AddComponent<FirstRunOperationCard>().Build(camera);
        }

        private void Build(Camera cam)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            var head = cam.transform;
            var root = (RectTransform)transform;
            float dist = ResolveDistance(head);
            bool compact = dist < MinComfortDistance;
            Vector2 cardSize = compact ? CompactSize : FullSize;
            root.sizeDelta = cardSize;

            float vFov = cam.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * Mathf.Max(0.1f, cam.aspect));
            float maxW = 2f * dist * Mathf.Tan(hFov * 0.5f) * FillRatio;
            float maxH = 2f * dist * Mathf.Tan(vFov * 0.5f) * FillRatio;
            float scale = Mathf.Min(0.001f, maxW / cardSize.x, maxH / cardSize.y);

            root.localScale = Vector3.one * scale;
            transform.SetParent(head, false);
            root.localPosition = new Vector3(0f, 0f, dist);
            root.localRotation = Quaternion.identity;

            BuildContent(root, cardSize, compact);
            Debug.Log("[FirstRunOperationCard] fov=" + cam.fieldOfView.ToString("F0") +
                      " aspect=" + cam.aspect.ToString("F2") + " dist=" + dist.ToString("F2") +
                      " compact=" + compact + " scale=" + scale.ToString("F5"));
        }

        private static float ResolveDistance(Transform head)
        {
            float dist = Distance;
            if (Physics.Raycast(head.position, head.forward, out var hit, Distance + 0.2f,
                    ~0, QueryTriggerInteraction.Ignore))
                dist = Mathf.Clamp(hit.distance - 0.08f, 0.28f, Distance);
            return dist;
        }

        private void BuildContent(RectTransform root, Vector2 cardSize, bool compact)
        {
            RuntimeUiFactory.CreatePanel(root, "Glass", Glass, Vector2.zero, cardSize);
            RuntimeUiFactory.CreateTmpLabel(root, "Title", "镜场 - 先走这三步",
                compact ? 26 : 29, new Vector2(0f, compact ? 96f : 172f),
                new Vector2(cardSize.x - 60f, 48f), Gold, FontStyle.Bold);

            string[] rows = compact
                ? new[] { "1 看沙盘", "2 定机位", "3 存护照" }
                : new[]
                {
                    "1. 看沙盘：双手捏合缩放，拖动演员排走位",
                    "2. 定机位：用身体找角度，扳机记录运镜",
                    "3. 存护照：把镜头意图交给 Seedance 生成"
                };
            float[] ys = compact ? new[] { 44f, 2f, -40f } : new[] { 90f, 26f, -38f };
            for (int i = 0; i < rows.Length; i++)
                RuntimeUiFactory.CreateTmpLabel(root, "Step" + (i + 1), rows[i],
                    compact ? 24 : 23, new Vector2(0f, ys[i]),
                    new Vector2(cardSize.x - 64f, 46f), new Color(0.93f, 0.93f, 0.94f, 1f));

            if (!compact)
                RuntimeUiFactory.CreateTmpLabel(root, "Hint", "射线指向按钮，扣扳机确认", 16,
                    new Vector2(0f, -120f), new Vector2(500f, 28f),
                    new Color(0.72f, 0.74f, 0.78f, 1f));

            CreateConfirmButton(root, compact ? -100f : -174f, compact ? 160f : 190f);
        }

        private static void CreateConfirmButton(RectTransform root, float y, float width)
        {
            var size = new Vector2(width, ButtonHeight);
            var go = RuntimeUiFactory.CreatePanel(root, "ConfirmButton", Gold,
                new Vector2(0f, y), size);
            var button = go.AddComponent<Button>();
            RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", "知道了", 16,
                Vector2.zero, size, Ink, FontStyle.Bold);
            button.onClick.AddListener(() => Confirm(go));
            go.AddComponent<UguiHoverEffect>();
        }

        private static void Confirm(GameObject go)
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            Destroy(go);
        }
    }
}
