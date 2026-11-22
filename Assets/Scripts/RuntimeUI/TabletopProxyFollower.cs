using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘代理跟随器：每具代理对应一位真实演员，LateUpdate 把世界位姿同步到沙盘代理。
    /// 坐标换算一律交给 TabletopDirector，本类不重写换算数学。
    /// </summary>
    public class TabletopProxyFollower : MonoBehaviour
    {
        [Header("绑定")]
        public Transform actor;
        public Transform proxy;
        public TabletopDirector director;

        /// <summary>由构建器赋值：绑定真实演员、沙盘代理与沙盘导演。</summary>
        public void Bind(Transform actorTransform, TabletopDirector tabletop)
        {
            actor = actorTransform;
            director = tabletop;
            proxy = transform;
        }

        private void LateUpdate()
        {
            if (!gameObject.activeInHierarchy || actor == null || proxy == null || director == null) return;
            director.SyncWorldToProxy(actor, proxy);
        }
    }
}
