using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace ShotField.RuntimeUI
{
    /// <summary>独立 WorldSpace 积木卡：壳、抓取、边缘缩放、距离吸附与注册。</summary>
    public sealed class SpatialBlockCard : MonoBehaviour
    {
        public string CardId { get; private set; } = "";
        public string Title { get; private set; } = "";
        public Vector2 PixelSize { get; private set; } = new Vector2(620f, 420f);
        public Canvas Canvas { get; private set; }
        public RectTransform ContentRoot { get; private set; }
        public BlockPanelSlot Slot { get; private set; }
        public BlockPanelRegistry Registry { get; private set; }
        public Transform HeadAnchor { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsGrabbed { get; private set; }
        public float CurrentDistance { get; private set; } = 0.70f;
        public float MinDistance = 0.50f;
        public float MaxDistance = 3.00f;
        public float[] DistanceSteps = { 0.50f, 0.70f, 0.80f, 1.20f, 2.00f, 3.00f };
        private XRGrabInteractable _grab;
        public static SpatialBlockCard Create(Transform parent, string id, string title,
            Vector2 pixelSize, int order, BlockPanelRegistry registry, Transform headAnchor)
        {
            var go = new GameObject("Card_" + id, typeof(RectTransform), typeof(Canvas),
                typeof(Image), typeof(TrackedDeviceGraphicRaycaster), typeof(BoxCollider),
                typeof(Rigidbody), typeof(XRGrabInteractable), typeof(BlockPanelSlot),
                typeof(SpatialBlockCard));
            go.transform.SetParent(parent, false);
            var card = go.GetComponent<SpatialBlockCard>();
            card.CardId = id; card.Title = title; card.PixelSize = pixelSize;
            card.Registry = registry; card.HeadAnchor = headAnchor;
            card.Build(order);
            go.SetActive(false);
            return card;
        }
        private void Build(int order)
        {
            Canvas = GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.worldCamera = Camera.main;
            Canvas.sortingOrder = 3200 + order;
            var rt = (RectTransform)transform;
            rt.sizeDelta = PixelSize; rt.localScale = Vector3.one * 0.001f; rt.pivot = new Vector2(0.5f, 0.5f);
            var background = GetComponent<Image>();
            background.color = new Color(0.11f, 0.12f, 0.14f, 0.88f); background.raycastTarget = false;
            Slot = GetComponent<BlockPanelSlot>(); Slot.panelId = CardId; Slot.sizeMeters = PixelSize * 0.001f; Slot.order = order;
            var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var collider = GetComponent<BoxCollider>();
            collider.size = new Vector3(PixelSize.x, 60f, 20f); collider.center = new Vector3(0f, (PixelSize.y - 60f) * 0.5f, 10f);
            _grab = GetComponent<XRGrabInteractable>();
            _grab.movementType = XRGrabInteractable.MovementType.Instantaneous; _grab.throwOnDetach = false;
            _grab.trackPosition = true; _grab.trackRotation = true;
            _grab.selectEntered.AddListener(OnGrabEnter); _grab.selectExited.AddListener(OnGrabExit);
            BuildHeader(); BuildContent(); BuildScaleHandle();
        }
        private void BuildHeader()
        {
            var header = RuntimeUiFactory.CreatePanel(transform, "GripBand",
                new Color(0.16f, 0.18f, 0.20f, 0.95f),
                new Vector2(0f, (PixelSize.y - 60f) * 0.5f), new Vector2(PixelSize.x, 60f));
            header.GetComponent<Image>().raycastTarget = false;
            var title = RuntimeUiFactory.CreateTmpLabel(header.transform, "Title", Title, 28,
                new Vector2(-24f, 0f), new Vector2(PixelSize.x - 120f, 44f), Color.white);
            title.alignment = TextAlignmentOptions.Left;
        }
        private void BuildContent()
        {
            var go = new GameObject("Content", typeof(RectTransform)); go.transform.SetParent(transform, false);
            ContentRoot = (RectTransform)go.transform; ContentRoot.anchorMin = Vector2.zero; ContentRoot.anchorMax = Vector2.one;
            ContentRoot.offsetMin = new Vector2(16f, 16f); ContentRoot.offsetMax = new Vector2(-16f, -60f);
        }
        private void BuildScaleHandle()
        {
            var go = new GameObject("ScaleHandle", typeof(RectTransform), typeof(Image),
                typeof(BoxCollider), typeof(Rigidbody), typeof(XRGrabInteractable), typeof(CardScaleHandle));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(20f, -30f); rt.sizeDelta = new Vector2(40f, 120f);
            go.GetComponent<Image>().color = new Color(0.95f, 0.64f, 0.24f, 0.90f);
            var label = RuntimeUiFactory.CreateTmpLabel(go.transform, "Label", "缩放", 16,
                Vector2.zero, new Vector2(40f, 120f), new Color(0.11f, 0.12f, 0.14f));
            label.alignment = TextAlignmentOptions.Center;
            go.GetComponent<BoxCollider>().size = new Vector3(40f, 120f, 20f);
            var body = go.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var grab = go.GetComponent<XRGrabInteractable>(); grab.movementType = XRGrabInteractable.MovementType.Instantaneous; grab.throwOnDetach = false;
            go.GetComponent<CardScaleHandle>().Bind(this);
        }
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open; gameObject.SetActive(open);
            if (open) { if (Registry != null) Registry.Register(Slot); Layout(); }
            else if (Registry != null) Registry.Unregister(Slot);
        }
        public void Layout()
        {
            if (!IsOpen || Registry == null) return;
            Registry.Layout(); CurrentDistance = Registry.CurrentDistance;
        }
        public void SetDistance(float distance)
        {
            if (HeadAnchor == null) return;
            CurrentDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            Vector3 dir = transform.position - HeadAnchor.position;
            if (dir.sqrMagnitude < 0.0001f) dir = HeadAnchor.forward;
            transform.position = HeadAnchor.position + dir.normalized * CurrentDistance; FaceHead();
        }
        public void Snap()
        {
            if (DistanceSteps == null || DistanceSteps.Length == 0) return;
            float nearest = DistanceSteps[0]; float delta = Mathf.Abs(CurrentDistance - nearest);
            for (int i = 1; i < DistanceSteps.Length; i++)
            {
                float candidate = Mathf.Abs(CurrentDistance - DistanceSteps[i]);
                if (candidate < delta) { nearest = DistanceSteps[i]; delta = candidate; }
            }
            SetDistance(nearest);
        }
        public void RebindHead(Transform head)
        {
            HeadAnchor = head;
            if (IsOpen) Layout();
        }

        public void FaceHead()
        {
            if (HeadAnchor == null) return;
            Vector3 away = transform.position - HeadAnchor.position; away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
        private void OnGrabEnter(SelectEnterEventArgs args) => IsGrabbed = true;
        private void OnGrabExit(SelectExitEventArgs args)
        {
            IsGrabbed = false;
            CurrentDistance = HeadAnchor != null ? Vector3.Distance(HeadAnchor.position, transform.position) : CurrentDistance;
            Snap();
        }
        private void OnDestroy()
        {
            if (_grab != null) { _grab.selectEntered.RemoveListener(OnGrabEnter); _grab.selectExited.RemoveListener(OnGrabExit); }
            if (Registry != null) Registry.Unregister(Slot);
        }
    }
}
