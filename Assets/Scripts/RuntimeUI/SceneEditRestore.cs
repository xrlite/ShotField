using System;
using System.Collections.Generic;
using System.IO;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 快照保存/恢复链路（M1 第 5、6 步）。保存序列化演员站位朝向与本轮新增道具；
    /// 恢复时同样走 TryPlace，不可行走的落点显式上报，不静默丢弃。
    /// </summary>
    public sealed partial class DesktopStudioEditView
    {
        /// <summary>手动重放存档：读盘 → 恢复演员 → 恢复道具，结果写进面板消息。</summary>
        public void RestoreFromDisk() => RestoreFromDisk(true);

        /// <summary>reportMissing=false 用于启动自动恢复：无存档时保持安静，不刷错误消息。</summary>
        public void RestoreFromDisk(bool reportMissing)
        {
            var dto = SceneEditStateStore.Load(out var error);
            if (dto == null)
            {
                if (reportMissing) message = "读取存档失败: " + error;
                return;
            }
            var failures = new List<string>();
            RestoreActors(dto, failures);
            RestoreProps(dto);
            restoredUtc = dto.savedUtc;
            message = failures.Count == 0
                ? $"已恢复 {dto.savedUtc}"
                : $"已恢复 {dto.savedUtc}（{string.Join("；", failures)}）";
        }

        private void SaveSnapshot()
        {
            var dto = new SceneEditStateDto();
            var actorList = new List<SceneActorDto>();
            foreach (var nav in Actors())
                actorList.Add(new SceneActorDto(nav.name, nav.transform.position, nav.transform.rotation));
            dto.actors = actorList.ToArray();
            var propList = new List<ScenePropDto>();
            foreach (var pair in placedProps)
                if (pair.Value != null) propList.Add(new ScenePropDto(pair.Key, pair.Value.transform.position));
            dto.props = propList.ToArray();
            try
            {
                savePath = SceneEditStateStore.Save(dto);
                message = $"已保存: 演员 {dto.actors.Length} / 道具 {dto.props.Length}";
            }
            catch (Exception ex)
            {
                message = "保存失败: " + ex.Message;
            }
        }

        private void RestoreActors(SceneEditStateDto dto, List<string> failures)
        {
            var all = FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include);
            foreach (var saved in dto.actors)
            {
                var nav = FindActor(all, saved.id);
                if (nav == null) { failures.Add($"未找到演员 {saved.id}"); continue; }
                // 失活待添加的演员不参与本次快照恢复，静默跳过，不报缺失。
                if (!nav.gameObject.activeInHierarchy) continue;
                if (nav.agent == null) { failures.Add($"{saved.id} 无 NavMeshAgent"); continue; }
                if (nav.TryPlace(saved.position, SampleRadius, out _))
                    nav.transform.rotation = Quaternion.Euler(saved.eulerAngles);
                else failures.Add($"{saved.id} 落点不可行走，未恢复");
            }
        }

        private void RestoreProps(SceneEditStateDto dto)
        {
            foreach (var pair in placedProps)
                if (pair.Value != null) Destroy(pair.Value);
            placedProps.Clear();
            foreach (var saved in dto.props)
            {
                var entry = PropLibrary.Find(saved.propId);
                if (entry == null) continue;
                var instance = Spawn(entry, saved.position);
                if (instance != null) placedProps.Add(new KeyValuePair<string, GameObject>(entry.id, instance));
            }
        }

        private ActorNavigator FindActor(IEnumerable<ActorNavigator> actors, string id)
        {
            foreach (var nav in actors) if (nav != null && nav.name == id) return nav;
            return null;
        }

        private bool NearMarker(Rect map, ActorNavigator nav, Vector2 mouse)
        {
            return Vector2.Distance(WorldToMap(nav.transform.position, map), mouse) <= MarkerHitPx;
        }

        private Color ChipColor(ActorNavigator nav)
        {
            var name = nav.name;
            foreach (var key in PrevisPalette.Keys)
                if (name.Contains(key)) return PrevisPalette.ColorOf(key);
            return Color.gray;
        }

        private void PlacePendingProp(Vector3 worldPos)
        {
            var entry = PropLibrary.Find(pendingPropId);
            if (entry == null) { message = "道具条目不存在: " + pendingPropId; return; }
            var instance = Spawn(entry, worldPos);
            if (instance == null) return;
            placedProps.Add(new KeyValuePair<string, GameObject>(entry.id, instance));
            message = $"已放置: {entry.displayName}";
            pendingPropId = null;
        }

        private GameObject Spawn(PropEntry entry, Vector3 worldPos)
        {
            if (browser == null) browser = FindAnyObjectByType<PropBrowserUI>();
            if (browser != null) return browser.SpawnProp(entry.id, worldPos);
            var prefab = LoadFallback(entry);
            if (prefab == null) { message = $"未绑定且 Resources 兜底无资产: {entry.id}"; return null; }
            var instance = Instantiate(prefab, worldPos, Quaternion.identity);
            instance.name = entry.id + "_placed";
            PropBrowserUI.EnsureGrabReady(instance, entry.id);
            message = "未绑定 PropBrowserUI，走 Resources 兜底: " + entry.displayName;
            return instance;
        }

        private GameObject LoadFallback(PropEntry entry)
        {
            if (string.IsNullOrEmpty(entry.prefabPath)) return null;
            var name = Path.GetFileNameWithoutExtension(entry.prefabPath);
            return Resources.Load<GameObject>("Props/" + name) ?? Resources.Load<GameObject>(name);
        }
    }

    /// <summary>启动自动恢复：场景载入后若存档存在，重放最近一次编辑快照。</summary>
    public sealed class SceneEditRestore : MonoBehaviour
    {
        void Start()
        {
            var view = GetComponent<DesktopStudioEditView>();
            if (view != null) view.RestoreFromDisk(reportMissing: false);
        }
    }
}
