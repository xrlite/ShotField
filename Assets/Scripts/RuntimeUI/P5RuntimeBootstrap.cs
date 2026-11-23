using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// P5 运行时的零配置装配入口。
    ///
    /// 解决的问题：P5 新增的四个组件都需要在场景里挂载并连线，但当前环境
    /// Unity 编译管线阻塞，无法开编辑器拖引用。本类在 Play 开始时按需补齐，
    /// 使框架可以被直接观察与验证，而不是停在"代码写了但没挂"的状态。
    ///
    /// 为什么用 <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 而不是场景挂载：
    /// 1. 只在运行时生效，**完全不写场景**，不会产生 .unity 变更与合并冲突；
    /// 2. 幂等：场景里已有该组件时跳过，人工调过的参数不会被覆盖；
    /// 3. 换场景/换宿主对象都不需要重新装配。
    ///
    /// 装配位置选在 TabletopDirector 所在对象下，因为它已经是沙盘/片场的既有中枢，
    /// 新组件挂在它旁边符合"按数据归属挂载"的既有习惯，不新建平行根节点。
    /// </summary>
    public static class P5RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var director = Object.FindAnyObjectByType<TabletopDirector>();
            if (director == null)
            {
                Debug.LogWarning("[P5RuntimeBootstrap] 场景里没有 TabletopDirector，" +
                                 "P5 组件未装配（沙盘/片场功能不可用）。");
                return;
            }

            var host = director.gameObject;

            var budget = Ensure<TabletopActorBudget>(host);
            if (budget.tabletop == null) budget.tabletop = director;

            Ensure<BlockPanelRegistry>(host);
            Ensure<ViewHeightCalibration>(host);
            Ensure<StudioFadeTransition>(host);

            Debug.Log("[P5RuntimeBootstrap] 已装配：ActorBudget / BlockPanelRegistry / " +
                      "ViewHeightCalibration / StudioFadeTransition。");
        }

        /// <summary>没有就挂一个，有就返回现有的；不覆盖任何已填字段。</summary>
        private static T Ensure<T>(GameObject host) where T : Component
        {
            var existing = host.GetComponent<T>();
            if (existing != null) return existing;
            return host.AddComponent<T>();
        }
    }
}
