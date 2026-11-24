using ShotField.Core;
using TMPro;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>资产抽屉内的五幕选择段；只负责剧本导航，不参与资产导入。</summary>
    public sealed class AssetDrawerScriptSection : MonoBehaviour
    {
        private ScriptSelectionPanel _panel;
        private FiveActsProductionPipeline _pipeline;
        private TMP_Text _status;
        private float _width;
        public int CurrentActIndex { get; private set; }

        public void Bind(GameObject root, ScriptSelectionPanel panel, float width)
        {
            _panel = panel; _width = width;
            _status = RuntimeUiFactory.CreateTmpLabel(root.transform, "ActStatus", "",
                13, new Vector2(0f, 29f), new Vector2(width - 20f, 24f));
            for (int i = 0; i < 5; i++)
            {
                int act = i;
                float aw = (width - 40f) / 5f;
                RuntimeUiFactory.CreateTmpButton(root.transform, "Act" + (i + 1), $"第{ToChinese(i + 1)}幕",
                    new Vector2((i - 2f) * (aw + 10f), -2f), new Vector2(aw, 28f), onClick: () => SelectAct(act));
            }
            RuntimeUiFactory.CreateTmpButton(root.transform, "PreviousAct", "上一幕",
                new Vector2(-width / 4f, -34f), new Vector2(120f, 24f), onClick: PreviousAct);
            RuntimeUiFactory.CreateTmpButton(root.transform, "NextAct", "下一幕",
                new Vector2(width / 4f, -34f), new Vector2(120f, 24f), onClick: NextAct);
            Refresh();
        }

        public void SelectAct(int index)
        {
            if (_panel != null)
            {
                _panel.SelectAct(index);
                CurrentActIndex = _panel.CurrentActIndex;
            }
            else
            {
                CurrentActIndex = Mathf.Clamp(index, 0, 4);
                if (_pipeline == null) _pipeline = new FiveActsProductionPipeline();
            }
            Refresh();
        }

        public void NextAct()
        {
            if (_panel != null) _panel.NextAct();
            else CurrentActIndex = (CurrentActIndex + 1) % 5;
            if (_panel != null) CurrentActIndex = _panel.CurrentActIndex;
            Refresh();
        }

        public void PreviousAct()
        {
            if (_panel != null) _panel.PreviousAct();
            else CurrentActIndex = (CurrentActIndex + 4) % 5;
            if (_panel != null) CurrentActIndex = _panel.CurrentActIndex;
            Refresh();
        }

        public void Refresh()
        {
            if (_status == null) return;
            if (_panel != null)
            {
                CurrentActIndex = _panel.CurrentActIndex;
                _status.text = "剧本面板已接入 | " + _panel.CurrentAct;
            }
            else
            {
                if (_pipeline == null) _pipeline = new FiveActsProductionPipeline();
                var desc = _pipeline.GetActDescriptor((ProductionAct)CurrentActIndex);
                _status.text = "五幕预览 | " + desc.actTitle.Replace('·', '/');
            }
        }

        private static string ToChinese(int n) => n == 1 ? "一" : n == 2 ? "二" :
            n == 3 ? "三" : n == 4 ? "四" : "五";
    }
}
