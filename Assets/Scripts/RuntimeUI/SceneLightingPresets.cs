using System;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    public enum StudioLightingMood
    {
        NightMystery,
        MorningWarm,
        InterviewStark
    }

    /// <summary>
    /// 片场电影光影氛围运行时预设（场景优化）：
    /// 一键在 NightMystery / MorningWarm / InterviewStark 之间实时切换主辅光色温、强度与聚光角度。
    /// </summary>
    public class SceneLightingPresets : MonoBehaviour
    {
        public Light windowKeyLight;
        public Light tableSpotLight;
        public StudioLightingMood currentMood = StudioLightingMood.NightMystery;

        public event Action<StudioLightingMood> OnMoodChanged;

        void Awake() => AutoBindLights();

        public void AutoBindLights()
        {
            if (windowKeyLight == null)
            {
                var go = GameObject.Find("WindowMoonlight");
                if (go != null) windowKeyLight = go.GetComponent<Light>();
            }
            if (tableSpotLight == null)
            {
                var go = GameObject.Find("TableWarmLight");
                if (go != null) tableSpotLight = go.GetComponent<Light>();
            }
        }

        public void ApplyMood(StudioLightingMood mood)
        {
            AutoBindLights();
            currentMood = mood;

            switch (mood)
            {
                case StudioLightingMood.NightMystery:
                    SetLight(windowKeyLight, new Color(0.56f, 0.72f, 0.96f), 0.95f, 65f);
                    SetLight(tableSpotLight, new Color(1.00f, 0.78f, 0.52f), 2.80f, 55f);
                    break;
                case StudioLightingMood.MorningWarm:
                    SetLight(windowKeyLight, new Color(1.00f, 0.88f, 0.68f), 1.45f, 78f);
                    SetLight(tableSpotLight, new Color(1.00f, 0.92f, 0.80f), 1.40f, 75f);
                    break;
                case StudioLightingMood.InterviewStark:
                    SetLight(windowKeyLight, new Color(0.54f, 0.68f, 0.86f), 0.32f, 50f);
                    SetLight(tableSpotLight, new Color(1.00f, 0.88f, 0.74f), 4.10f, 42f);
                    break;
            }
            OnMoodChanged?.Invoke(mood);
        }

        public void CycleMood()
        {
            int count = Enum.GetValues(typeof(StudioLightingMood)).Length;
            ApplyMood((StudioLightingMood)(((int)currentMood + 1) % count));
        }

        private static void SetLight(Light lightComp, Color color, float intensity, float spotAngle)
        {
            if (lightComp == null) return;
            lightComp.color = color;
            lightComp.intensity = intensity;
            if (lightComp.type == LightType.Spot) lightComp.spotAngle = spotAngle;
        }
    }
}
