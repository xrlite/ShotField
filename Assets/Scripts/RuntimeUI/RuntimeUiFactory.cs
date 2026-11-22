using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    public static class RuntimeUiFactory
    {
        public static GameObject CreatePanel(Transform parent, string name, Color bg, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2? size = null, Color? bg = null, UnityAction onClick = null)
        {
            var btnSize = size ?? new Vector2(100f, 45f);
            var go = CreatePanel(parent, name, bg ?? new Color(0.24f, 0.26f, 0.30f, 0.95f), pos, btnSize);
            var btn = go.AddComponent<Button>();
            var lbl = CreateLabel(go.transform, "Label", label, 12, Vector2.zero, btnSize, Color.white);
            lbl.raycastTarget = false;
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Text CreateLabel(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2? size = null, Color? color = null, Font font = null, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color ?? Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            txt.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size ?? new Vector2(280f, 30f);
            return txt;
        }

        public static void SetStatus(Text target, string message)
        {
            if (target != null) target.text = message;
        }
    }
}
