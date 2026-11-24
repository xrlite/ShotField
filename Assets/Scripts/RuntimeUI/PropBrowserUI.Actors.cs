using System.Collections.Generic;
using System.Linq;
using ShotField.Core;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public partial class PropBrowserUI
    {
        // 六色演员条目：点击把场景内待激活的 PrevisCast 实例放行入场。
        private static readonly string[] ActorIds =
        {
            "actorRed", "actorBlue", "actorYellow",
            "actorGreen", "actorViolet", "actorMagenta"
        };

        private static readonly Dictionary<string, string> ActorSceneName = new Dictionary<string, string>
        {
            ["actorRed"] = "PrevisCast_A_Red",
            ["actorBlue"] = "PrevisCast_B_Blue",
            ["actorYellow"] = "PrevisCast_C_Yellow",
            ["actorGreen"] = "PrevisCast_D_Green",
            ["actorViolet"] = "PrevisCast_E_Violet",
            ["actorMagenta"] = "PrevisCast_F_Magenta"
        };

        private bool actorButtonsBuilt;

        private void Start() => BuildActorButtons();

        private void BuildActorButtons()
        {
            if (actorButtonsBuilt) return;
            actorButtonsBuilt = true;
            var panel = statusText != null ? statusText.transform.parent : transform;
            for (var i = 0; i < ActorIds.Length; i++)
            {
                var entry = PropLibrary.Find(ActorIds[i]);
                var label = entry != null ? entry.displayName : ActorIds[i];
                var x = i % 2 == 0 ? -48f : 48f;
                var y = -45f - (i / 2) * 58f;
                var btn = RuntimeUiFactory.CreateTmpButton(panel, $"Spawn_{ActorIds[i]}", label,
                    new Vector2(x, y), new Vector2(88f, 44f));
                var id = ActorIds[i];
                btn.onClick.AddListener(() => ActivateActor(id));
            }
        }

        public bool ActivateActor(string actorId)
        {
            if (!ActorSceneName.TryGetValue(actorId, out var sceneName)) return false;
            var nav = UnityEngine.Object.FindObjectsByType<ActorNavigator>(FindObjectsInactive.Include)
                .FirstOrDefault(n => n.name == sceneName);
            if (nav == null)
            {
                RuntimeUiFactory.SetStatusTmp(statusText, $"未找到演员: {sceneName}");
                return false;
            }
            if (!nav.gameObject.activeSelf) nav.gameObject.SetActive(true);
            RuntimeUiFactory.SetStatusTmp(statusText, $"已加入: {nav.name}");
            OnPropSpawned?.Invoke(nav.gameObject);
            return true;
        }
    }
}
