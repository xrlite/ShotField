using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 三层拱门隧道穿梭遮罩（炭黑/灰蓝/暖金）：配合 StudioFadeTransition 在 1:20 沙盘 ⇄ 1:1 片场切换时
    /// 呈现“穿过镜场拱门走进故事”的格列佛隧道缩放动效。纯 RectTransform 缩放，零 GC、90fps 安全。
    /// </summary>
    public sealed class ArchTunnelOverlay : MonoBehaviour
    {
        private static readonly Color[] ArchColors =
        {
            new Color(0.11f, 0.13f, 0.15f, 0.92f),
            new Color(0.43f, 0.46f, 0.50f, 0.88f),
            new Color(0.79f, 0.66f, 0.38f, 0.95f)
        };

        private readonly RectTransform[] _arches = new RectTransform[3];
        private CanvasGroup _group;
        private Canvas _canvas;
        private Texture2D _ringTex;
        private Sprite _ringSprite;

        public void Initialize(Transform parent, float distance)
        {
            transform.SetParent(parent, false);
            transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.15f, distance));
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * 0.001f;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10000;
            canvas.enabled = false;   // 未播放时空 Canvas 不提交
            _canvas = canvas;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            BuildArchSprite();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Arch_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = _ringSprite;
                img.color = ArchColors[i];
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(360f - i * 65f, 420f - i * 70f);
                _arches[i] = rt;
            }
        }

        public void SetProgress(float t, bool entering1to1)
        {
            if (_group == null) return;
            float fade = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            _group.alpha = fade;
            if (_canvas != null) _canvas.enabled = fade > 0.001f;

            for (int i = 0; i < _arches.Length; i++)
            {
                if (_arches[i] == null) continue;
                float staggered = Mathf.Clamp01((t - i * 0.12f) / 0.76f);
                float scale = entering1to1
                    ? Mathf.Lerp(0.30f, 2.80f, staggered)
                    : Mathf.Lerp(2.40f, 0.30f, staggered);
                _arches[i].localScale = Vector3.one * scale;
            }
        }

        private void BuildArchSprite()
        {
            const int S = 128;
            _ringTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - S * 0.5f) / (S * 0.42f);
                    float ny = (y - S * 0.45f) / (S * 0.42f);
                    float dist = ny >= 0f
                        ? Mathf.Sqrt(nx * nx + ny * ny)
                        : Mathf.Max(Mathf.Abs(nx), -ny > 0.9f ? 2f : 0f);
                    bool inArch = (ny >= 0f && dist >= 0.76f && dist <= 0.96f) ||
                                  (ny < 0f && ny > -0.85f && Mathf.Abs(nx) >= 0.76f && Mathf.Abs(nx) <= 0.96f);
                    px[y * S + x] = inArch ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            _ringTex.SetPixels32(px);
            _ringTex.Apply();
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            if (_ringSprite != null) Destroy(_ringSprite);
            if (_ringTex != null) Destroy(_ringTex);
        }
    }
}
