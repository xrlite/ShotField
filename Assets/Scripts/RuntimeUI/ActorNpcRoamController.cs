using UnityEngine;
using UnityEngine.AI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘 NPC 自由演控制器：开启后，非当前手动选中的演员会在 NavMesh 安全区内自主漫步、
    /// 转向与短暂停留，配合 ActorLocomotionAnimator 播自然步态，让沙盘像游戏一样有生气。
    /// </summary>
    public sealed class ActorNpcRoamController : MonoBehaviour
    {
        public bool freeRoamEnabled = true;
        public float minWaitSeconds = 2.5f;
        public float maxWaitSeconds = 5.5f;
        public float roamRadius = 2.2f;

        private float _nextTick;
        private TabletopDirector _tabletop;
        private ActorPossessionController _possession;

        private void Awake()
        {
            _tabletop = FindAnyObjectByType<TabletopDirector>();
            _possession = FindAnyObjectByType<ActorPossessionController>();
            _nextTick = Time.time + 1.5f;
        }

        public bool ToggleFreeRoam()
        {
            freeRoamEnabled = !freeRoamEnabled;
            Debug.Log($"[NpcRoam] freeRoamEnabled={freeRoamEnabled}");
            return freeRoamEnabled;
        }

        private void Update()
        {
            if (!freeRoamEnabled || Time.time < _nextTick) return;
            _nextTick = Time.time + Random.Range(minWaitSeconds, maxWaitSeconds);

            var all = ActorSelection.All;
            if (all == null || all.Count == 0) return;

            var selected = ActorSelection.Current;
            var possessed = _possession != null && _possession.IsPossessing ? _possession.possessedActor : null;

            for (int i = 0; i < all.Count; i++)
            {
                var nav = all[i];
                if (nav == null || !nav.isActiveAndEnabled || !nav.gameObject.activeInHierarchy) continue;
                if (ReferenceEquals(nav, selected) || nav.gameObject == possessed) continue;
                if (nav.IsMoving || nav.agent == null || !nav.agent.isOnNavMesh) continue;

                Vector3 offset = new Vector3(Random.Range(-roamRadius, roamRadius), 0f, Random.Range(-roamRadius, roamRadius));
                Vector3 candidate = nav.transform.position + offset;
                if (NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    nav.MoveTo(hit.position, false);
                    break;
                }
            }
        }
    }
}
