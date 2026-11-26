using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 空间大屏五步导演工作台：与手表快捷菜单职责分离，专注精密设置与全流程串联。
    /// 5 个标签页：剧本建景 / 沙盘排演 / 1:1 演绎 / 剪辑导轨 / 导出成片。
    /// </summary>
    public sealed partial class DirectorDockPanel : MonoBehaviour
    {
        private static readonly Color BgColor = new Color(0.09f, 0.10f, 0.13f, 0.94f);
        private static readonly Color TabNormal = new Color(0.20f, 0.23f, 0.28f, 0.95f);
        private static readonly Color TabActive = new Color(0.79f, 0.66f, 0.38f, 1f);
        private static readonly string[] TabNames = { "1.剧本建景", "2.沙盘排演", "3.1:1演绎", "4.剪辑导轨", "5.导出成片" };

        private GameObject _panel;
        private readonly GameObject[] _pages = new GameObject[5];
        private readonly Image[] _tabImgs = new Image[5];
        private TMP_Text _status;
        private int _activeTab = -1;

        public bool IsOpen => _panel != null && _panel.activeInHierarchy;

        public static DirectorDockPanel Create(RectTransform parent)
        {
            var dock = parent.gameObject.AddComponent<DirectorDockPanel>();
            dock.Build(parent);
            return dock;
        }

        public void Toggle()
        {
            if (_panel == null) return;
            bool next = !_panel.activeSelf;
            _panel.SetActive(next);
            if (next && _activeTab < 0) SelectTab(0);
        }

        public void OpenTab(int index)
        {
            if (_panel == null) return;
            _panel.SetActive(true);
            SelectTab(index);
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void Build(RectTransform parent)
        {
            _panel = RuntimeUiFactory.CreatePanel(parent, "DirectorDockPanel", BgColor,
                new Vector2(0f, 212f), new Vector2(640f, 316f));
            SpatialGlass.Apply(_panel.GetComponent<Image>(), BgColor, 24f);
            _panel.AddComponent<SpatialUiMotion>();

            var root = _panel.transform;
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var btn = RuntimeUiFactory.CreateTmpButton(root, "Tab_" + i, TabNames[i],
                    new Vector2(-248f + i * 124f, 132f), new Vector2(118f, 34f),
                    TabNormal, () => SelectTab(idx));
                _tabImgs[i] = btn.GetComponent<Image>();
                var page = RuntimeUiFactory.CreatePanel(root, "Page_" + i, new Color(0f, 0f, 0f, 0f),
                    new Vector2(0f, -6f), new Vector2(604f, 224f));
                page.GetComponent<Image>().raycastTarget = false;
                _pages[i] = page;
            }

            _status = RuntimeUiFactory.CreateTmpLabel(root, "DockStatus", "就绪 · 选择上方步骤开始",
                14, new Vector2(0f, -138f), new Vector2(600f, 26f), new Color(0.86f, 0.82f, 0.72f));

            BuildPage1ScriptAndScene(_pages[0].transform);
            BuildPage2TabletopRoam(_pages[1].transform);
            BuildPage3Performance(_pages[2].transform);
            BuildPage4Timeline(_pages[3].transform);
            BuildPage5Export(_pages[4].transform);

            SpatialUiMotion.AttachButtonMotion(_panel);
            _panel.SetActive(false);
        }

        private void SelectTab(int index)
        {
            _activeTab = Mathf.Clamp(index, 0, 4);
            for (int i = 0; i < 5; i++)
            {
                if (_pages[i] != null) _pages[i].SetActive(i == _activeTab);
                if (_tabImgs[i] != null) _tabImgs[i].color = i == _activeTab ? TabActive : TabNormal;
            }
            SetStatus($"当前阶段：{TabNames[_activeTab]}");
        }

        public void SetStatus(string msg)
        {
            if (_status != null) _status.text = msg;
        }
    }
}
