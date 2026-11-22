using System;
using System.Collections.Generic;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 掌心菜单的快照存取：演员站位朝向 + 本轮新增道具，走 <see cref="SceneEditStateStore"/> 落盘/重放。
    /// 独立于 <see cref="PalmMenuState"/>：状态机只发命令，持久化与重放细节收敛在这里。
    /// 重放演员同样走 <see cref="ActorNavigator.TryPlace"/>，不可行走的落点不改状态。
    /// browser 每次调用传入，避免状态机晚绑定后服务里缓存空引用。
    /// </summary>
    public sealed class PalmMenuSnapshotService
    {
        private const float SampleRadius = 0.6f;

        /// <summary>组装快照并落盘；返回可读状态文案。</summary>
        public string Save(PropBrowserUI browser,
            IReadOnlyList<KeyValuePair<string, GameObject>> placedProps)
        {
            var dto = new SceneEditStateDto();
            var actorList = new List<SceneActorDto>();
            foreach (var nav in ActorSelection.All)
            {
                if (nav == null || !nav.gameObject.activeInHierarchy) continue;
                actorList.Add(new SceneActorDto(nav.name, nav.transform.position, nav.transform.rotation));
            }
            dto.actors = actorList.ToArray();

            var propList = new List<ScenePropDto>();
            foreach (var pair in placedProps)
                if (pair.Value != null) propList.Add(new ScenePropDto(pair.Key, pair.Value.transform.position));
            dto.props = propList.ToArray();

            try
            {
                SceneEditStateStore.Save(dto);
                return $"已保存: 演员 {dto.actors.Length} / 道具 {dto.props.Length}";
            }
            catch (Exception ex)
            {
                return "保存失败: " + ex.Message;
            }
        }

        /// <summary>读盘并重放；回收旧道具后按存档重放；返回可读状态文案。</summary>
        public string Reload(PropBrowserUI browser,
            List<KeyValuePair<string, GameObject>> placedProps)
        {
            var dto = SceneEditStateStore.Load(out var error);
            if (dto == null) return "读取存档失败: " + error;

            RestoreActors(dto);
            RestoreProps(browser, dto, placedProps);
            return $"已恢复 {dto.savedUtc}";
        }

        private void RestoreActors(SceneEditStateDto dto)
        {
            foreach (var saved in dto.actors)
            {
                var nav = FindActor(saved.id);
                if (nav == null) continue;
                if (nav.TryPlace(saved.position, SampleRadius, out _))
                    nav.transform.rotation = Quaternion.Euler(saved.eulerAngles);
            }
        }

        private void RestoreProps(PropBrowserUI browser, SceneEditStateDto dto,
            List<KeyValuePair<string, GameObject>> placedProps)
        {
            foreach (var pair in placedProps)
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            placedProps.Clear();

            if (browser == null) return;
            foreach (var saved in dto.props)
            {
                var instance = browser.SpawnProp(saved.propId, saved.position);
                if (instance != null)
                    placedProps.Add(new KeyValuePair<string, GameObject>(saved.propId, instance));
            }
        }

        private static ActorNavigator FindActor(string id)
        {
            foreach (var nav in ActorSelection.All)
                if (nav != null && nav.name == id) return nav;
            return null;
        }
    }
}
