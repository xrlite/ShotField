using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 掌心菜单运行时接线器。解决的是一个纯装配缺陷：
    /// <see cref="PalmMenuController"/> 的 8 个序列化引用在场景里全是 fileID 0，
    /// 而 InstantiateButton 在 buttonPrefab 或 buttonRoot 为空时直接返回 null，
    /// 于是菜单永远不渲染 —— 代码是对的，只是没接上。
    ///
    /// 为什么用运行时装配而不是改场景：
    /// 1. 不触碰 .unity / .prefab YAML，不引入场景污染（本项目已因抓图改画布吃过一次亏）；
    /// 2. 构建机不需要开编辑器，纯 .cs 即可生效；
    /// 3. 幂等 —— 引用已有值时一律不覆盖，后续在 Inspector 里手工接的线不会被冲掉。
    ///
    /// 本类晚于 PalmMenuController.Awake 执行，因此在手柄引用补齐后调用
    /// PalmMenuController.RebindHaptics，确保触感使用最终绑定的左右手柄。
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class PalmMenuAutoBinder : MonoBehaviour
    {
        private const string LeftControllerName = "XRController_Left";
        private const string RightControllerName = "XRController_Right";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PalmMenuAutoBinder>() != null) return;

            // 场景里 PalmMenuController 是零实例时，Bind() 会直接 return，
            // 菜单永远不渲染——「上场演这镜」也就永远点不到（讲稿第三幕的唯一入口）。
            if (FindAnyObjectByType<PalmMenuController>(FindObjectsInactive.Include) == null)
            {
                var menuGo = new GameObject("[PalmMenu]");
                menuGo.AddComponent<PalmMenuController>();
            }

            var go = new GameObject("[PalmMenuAutoBinder]");
            go.AddComponent<PalmMenuAutoBinder>();
        }

        private void Awake() => Bind();

        /// <summary>幂等接线：只填空引用，已有值一律保留。</summary>
        public void Bind()
        {
            var controller = FindAnyObjectByType<PalmMenuController>();
            if (controller == null || !BindVisuals(controller)) return;

            if (controller.menuAnchor == null)
                controller.menuAnchor = FindTransform(LeftControllerName);
            if (controller.headAnchor == null)
                controller.headAnchor = FindHeadTransform();
            if (controller.selectRay == null)
                controller.selectRay = FindRightRay();
            if (controller.leftController == null)
                controller.leftController = FindHaptics(LeftControllerName);
            if (controller.rightController == null)
                controller.rightController = FindHaptics(RightControllerName);
            controller.RebindHaptics(controller.leftController, controller.rightController);

            if (controller.menuAnchor == null || controller.headAnchor == null)
            {
                Debug.LogWarning("[PalmMenuAutoBinder] 未找到左手控制器或头部锚点，" +
                                 "菜单会停在原地不跟随；请确认场景里有 XROrigin 与 XRController_Left。");
            }
        }

        private static bool BindVisuals(PalmMenuController controller)
        {
            if (controller.menuCanvas != null && controller.buttonRoot != null && controller.buttonPrefab != null)
                return true;
            var visuals = PalmMenuUiFactory.Build(controller.transform);
            if (!visuals.IsValid)
            {
                Debug.LogWarning("[PalmMenuAutoBinder] 菜单视觉构建失败，菜单不会渲染。");
                return false;
            }
            controller.menuCanvas = visuals.Canvas;
            controller.buttonRoot = visuals.ButtonRoot;
            controller.buttonPrefab = visuals.ButtonPrefab;
            controller.statusLabel = visuals.StatusLabel;
            return true;
        }

        private static Transform FindTransform(string name)
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            foreach (var t in all)
                if (t.name == name) return t;
            return null;
        }

        /// <summary>头部锚点：优先主相机，其次场景里名为 XR_Headset 的节点。</summary>
        private static Transform FindHeadTransform()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;
            return FindTransform("XR_Headset");
        }

        private static XRRayInteractor FindRightRay()
        {
            var rays = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude);
            foreach (var ray in rays)
                if (IsUnder(ray.transform, RightControllerName)) return ray;
            return rays.Length > 0 ? rays[0] : null;
        }

        private static HapticImpulsePlayer FindHaptics(string controllerName)
        {
            var players = FindObjectsByType<HapticImpulsePlayer>(FindObjectsInactive.Exclude);
            foreach (var p in players)
                if (IsUnder(p.transform, controllerName)) return p;
            return null;
        }

        /// <summary>沿父链判断节点是否挂在某个控制器下。</summary>
        private static bool IsUnder(Transform node, string ancestorName)
        {
            while (node != null)
            {
                if (node.name == ancestorName) return true;
                node = node.parent;
            }
            return false;
        }
    }
}
