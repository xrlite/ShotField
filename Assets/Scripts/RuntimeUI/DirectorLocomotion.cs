using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 导演走位配置。只做配置与开关，不自己搬相机：位移由 XRI 的 Provider 执行，
    /// 这里负责把它们调成舒适默认（snap turn 45°、平移 2.0 m/s，连续移动可关）。
    /// 舒适性依据见 01_产品文档 蓝图 §四「记路径 ≠ 强制移动导演头部」。
    /// </summary>
    public class DirectorLocomotion : MonoBehaviour
    {
        public const float DefaultMoveSpeed = 2.0f;
        public const float DefaultTurnAngle = 45f;

        [SerializeField] private bool allowContinuousMove = true;

        private SnapTurnProvider _snapTurn;
        private ContinuousMoveProvider _continuousMove;

        public bool ContinuousMoveAllowed { get { return allowContinuousMove; } }
        public bool HasSnapTurn { get { return _snapTurn != null; } }
        public bool HasContinuousMove { get { return _continuousMove != null; } }

        public float TurnAngle
        {
            get { return _snapTurn != null ? _snapTurn.turnAmount : 0f; }
        }

        public float MoveSpeed
        {
            get { return _continuousMove != null ? _continuousMove.moveSpeed : 0f; }
        }

        private void Awake()
        {
            Bind();
        }

        /// <summary>找齐 Provider 并套用默认值；装配器与测试都可以显式调用。</summary>
        public void Bind()
        {
            _snapTurn = GetComponentInChildren<SnapTurnProvider>(true);
            _continuousMove = GetComponentInChildren<ContinuousMoveProvider>(true);

            if (_snapTurn != null) _snapTurn.turnAmount = DefaultTurnAngle;
            if (_continuousMove != null) _continuousMove.moveSpeed = DefaultMoveSpeed;

            SetContinuousMove(allowContinuousMove);
        }

        /// <summary>连续平移是可选项：关掉后只剩 snap turn + 平移传送，减少晕动。</summary>
        public void SetContinuousMove(bool allowed)
        {
            allowContinuousMove = allowed;
            if (_continuousMove != null) _continuousMove.enabled = allowed;
        }
    }
}
