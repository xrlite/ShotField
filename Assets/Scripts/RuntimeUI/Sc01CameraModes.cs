using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    public sealed class Sc01CameraModes : MonoBehaviour
    {
        [SerializeField] private Camera filmCamera;
        [SerializeField] private Transform handle;
        [SerializeField] private RectTransform panel;
        private XRGrabInteractable grab;
        public int Preset { get; private set; } = -1;

        private void Awake()
        {
            grab = handle.GetComponent<XRGrabInteractable>();
            CreateButton("Free", -231, SelectFree);
            CreateButton("Entry", -77, () => SelectPreset(0));
            CreateButton("Letter", 77, () => SelectPreset(1));
            CreateButton("Reaction", 231, () => SelectPreset(2));
            SelectFree();
        }

        private void LateUpdate()
        {
            if (Preset == -1 && grab && grab.isSelected)
                filmCamera.transform.SetPositionAndRotation(handle.position, handle.rotation);
        }

        public void SelectFree()
        {
            Preset = -1;
            handle.SetPositionAndRotation(filmCamera.transform.position, filmCamera.transform.rotation);
            handle.gameObject.SetActive(true);
        }

        public void SelectPreset(int index)
        {
            Vector3 position, target;
            float fov;
            switch (index)
            {
                case 0: position = new Vector3(-2.10f, 1.60f, 1.14f); target = new Vector3(.53f, 1.03f, 2.38f); fov = 57; break;
                case 1: position = new Vector3(-.48f, 1.4f, 1.36f); target = new Vector3(.95f, .59f, 2.25f); fov = 48; break;
                case 2: position = new Vector3(-.43f, 1.51f, 2.05f); target = new Vector3(1.18f, 1.45f, 3.05f); fov = 43; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            Preset = index;
            handle.gameObject.SetActive(false);
            filmCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            filmCamera.fieldOfView = fov;
        }

        private void CreateButton(string label, float x, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = panel.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(x, -40f);
            rect.sizeDelta = new Vector2(145, 56);
            go.GetComponent<Image>().color = new Color(.12f, .13f, .14f);
            go.GetComponent<Button>().onClick.AddListener(action);
            var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            caption.layer = go.layer;
            var bounds = (RectTransform)caption.transform;
            bounds.SetParent(rect, false); bounds.anchorMin = Vector2.zero; bounds.anchorMax = Vector2.one;
            bounds.offsetMin = bounds.offsetMax = Vector2.zero;
            var text = caption.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label; text.fontSize = 24; text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white; text.raycastTarget = false;
        }
    }
}
