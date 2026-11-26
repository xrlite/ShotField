using System.Collections.Generic;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 沙盘模式下的 1:1 舞台可见性。只记录并恢复本类改过的根节点，
    /// 不删除对象，也不覆盖场景预设原本的显隐状态。
    /// </summary>
    public partial class TabletopDirector
    {
        private static readonly string[] StageRootNames =
        {
            "STUDIO_Studio", "STUDIO_Truss", "STUDIO_SetWall", "STUDIO_Lighting_v05",
            "LGT_STU_Bounce", "SC01_FreeCameraHandle", "SC01_StageTape", "STUDIO_OpenEdge", "sofa_1"
        };

        private readonly Dictionary<GameObject, bool> _stageRootStates = new Dictionary<GameObject, bool>();

        private void SetStageRootsActive(bool active)
        {
            if (!active)
            {
                CacheStageRootStates();
                foreach (var pair in _stageRootStates) pair.Key.SetActive(false);
                return;
            }

            foreach (var pair in _stageRootStates)
                if (pair.Key != null) pair.Key.SetActive(pair.Value);
            _stageRootStates.Clear();
        }

        private void CacheStageRootStates()
        {
            if (_stageRootStates.Count > 0) return;
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(StageRootNames, root.name) < 0) continue;
                if (root == sceneRoot1to1) continue;
                _stageRootStates[root] = root.activeSelf;
            }
        }
    }
}
