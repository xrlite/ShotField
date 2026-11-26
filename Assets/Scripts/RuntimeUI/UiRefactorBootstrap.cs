using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// UI 重构统一装配：把所有 XRSimpleInteractable 的 hover/press 反馈收敛到
    /// 唯一的 <see cref="SpatialHoverEffect"/>，替代此前"一屏一个曲线"的做法。
    ///
    /// 走 RuntimeInitializeOnLoadMethod 而不是改场景：
    /// 1. 零场景写入，不与 .unity 合并冲突；
    /// 2. 幂等，已挂的组件不覆盖调过的参数；
    /// 3. 换场景、换宿主都自动生效。
    /// </summary>
    public static class UiRefactorBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            var interactables = Object.FindObjectsByType<XRSimpleInteractable>(
                FindObjectsInactive.Include);

            int added = 0;
            for (int i = 0; i < interactables.Length; i++)
            {
                var host = interactables[i];
                if (host == null || host.GetComponent<SpatialHoverEffect>() != null) continue;
                // 同一个按钮可能同时带 uGUI Button 与 XRSimpleInteractable，
                // 且 Card/Capsule 已显式挂过 UguiHoverEffect。两套都写 localScale 会互相打架。
                if (host.GetComponent<UguiHoverEffect>() != null) continue;
                host.gameObject.AddComponent<SpatialHoverEffect>();
                added++;
            }

            // uGUI Button 走 EventSystem 指针事件，不吃 XRI 的 hover 事件。
            // 首屏操作卡的「知道了」就是 Button，只覆盖 XRI 会让它完全没有手感。
            var buttons = Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include);
            int uiAdded = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                var host = buttons[i];
                if (host == null || host.GetComponent<SpatialHoverEffect>() != null) continue;
                if (host.GetComponent<UguiHoverEffect>() != null) continue;
                host.gameObject.AddComponent<UguiHoverEffect>();
                uiAdded++;
            }

            Debug.Log("[UiRefactorBootstrap] XRSimpleInteractable=" + interactables.Length +
                      "，新挂统一 hover 反馈=" + added + "；uGUI Button=" + buttons.Length +
                      "，新挂=" + uiAdded + "（已存在的不覆盖）。");
        }
    }
}
