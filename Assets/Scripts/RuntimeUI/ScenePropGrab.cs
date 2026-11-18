using ShotField.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 可摆放道具的抓放封装。一次完整「抓住 → 放下」压一条历史，
    /// 对应小云雀/CozyClay 的「一次交互 = 一条 undo」口径。
    /// 必须与 XRGrabInteractable 同物体；Rigidbody 与非 trigger Collider 由装配器负责。
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ScenePropGrab : MonoBehaviour
    {
        /// <summary>全场共享一条历史：任何道具的移动都进同一个栈，不按物体分栈。</summary>
        public static readonly PropMoveHistory History = new PropMoveHistory(20);

        [SerializeField] private string propId;

        private XRGrabInteractable _grab;
        private bool _dragging;
        private PropPose _before;

        public string PropId { get { return propId; } }
        public bool IsDragging { get { return _dragging; } }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (string.IsNullOrEmpty(propId)) propId = gameObject.name;
        }

        private void OnEnable()
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null) return;
            _grab.selectEntered.AddListener(OnGrabStarted);
            _grab.selectExited.AddListener(OnGrabEnded);
        }

        private void OnDisable()
        {
            if (_grab == null) return;
            _grab.selectEntered.RemoveListener(OnGrabStarted);
            _grab.selectExited.RemoveListener(OnGrabEnded);
        }

        private void Update()
        {
            if (!_dragging) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) CancelDrag();
        }

        private void OnGrabStarted(SelectEnterEventArgs args)
        {
            _before = Capture();
            _dragging = true;
        }

        private void OnGrabEnded(SelectExitEventArgs args)
        {
            if (!_dragging) return;
            _dragging = false;
            var record = new PropMoveRecord(_before, Capture());
            if (!record.IsMeaningful()) return;
            History.Push(record);
        }

        /// <summary>
        /// 取消在途拖拽：把道具退回抓取前位姿，不产生历史记录。
        /// 若此时仍被手握住，XRI 会继续驱动它，最终位置仍以松手为准 —— 这里只做回退，不抢控制权。
        /// </summary>
        public void CancelDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            Apply(_before);
            NotifyDropped();
        }

        /// <summary>回滚到某条记录的前态（撤销用）。</summary>
        public void RevertTo(PropPose pose)
        {
            Apply(pose);
            NotifyDropped();
        }

        public PropPose Capture()
        {
            return new PropPose(propId, transform.position, transform.rotation);
        }

        private void Apply(PropPose pose)
        {
            transform.position = pose.position;
            transform.rotation = pose.rotation;
        }

        /// <summary>位置被程序改写后清掉物理速度，避免刚体带着旧速度继续漂。</summary>
        private void NotifyDropped()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
