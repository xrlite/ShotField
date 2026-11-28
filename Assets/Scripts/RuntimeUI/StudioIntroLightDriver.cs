using System.Collections;
using UnityEngine;

namespace ShotField.RuntimeUI
{
    /// <summary>
    /// 开场「一束光先唤醒桌子」（A-2）的入口。
    /// 黑幕盖住首帧期间把主聚光灯压到 <see cref="DimIntensity"/>，退黑幕的同时用 0.9s 升回
    /// **运行时读到的**原强度——场景是 160、Editor 生成脚本里写的是 55，两个来源不一致，绝不硬编码。
    /// 灯光职责不塞进 StudioFadeTransition（它守 130 行预算，只管遮罩）。
    /// 失败安全：找不到 KeyLight_Top、Light 被禁用、或原强度本来就低于 0.1 → 静默跳过，不改任何东西。
    /// </summary>
    public static class StudioIntroLights
    {
        public const string KeyLightName = "KeyLight_Top";
        /// <summary>黑幕期间压到的强度。用 0.1 而不是 0：真机上万一遮罩没盖住也不至于全黑。</summary>
        public const float DimIntensity = 0.1f;
        /// <summary>渐亮时长（秒），落在需求给的 0.8–1.0 区间中值。</summary>
        public const float RiseSeconds = 0.9f;

        /// <summary>与 StudioFadeTransition.PlayIntro(holdSeconds) 同时调用，holdSeconds 必须一致。</summary>
        public static void Play(float holdSeconds)
        {
            var keyLight = FindKeyLight();
            if (keyLight == null) return;
            var driver = keyLight.GetComponent<StudioIntroLightDriver>();
            if (driver == null) driver = keyLight.gameObject.AddComponent<StudioIntroLightDriver>();
            if (driver != null) driver.Play(holdSeconds);
        }

        private static Light FindKeyLight()
        {
            var go = GameObject.Find(KeyLightName);
            if (go == null) return null;
            if (!go.activeInHierarchy) return null;
            var light = go.GetComponent<Light>();
            if (light == null || !light.enabled) return null;
            // 只压主聚光；万一这个名字被挂成方向光，压暗等于把整个大厅关灯，宁可跳过。
            if (light.type == LightType.Directional) return null;
            return light;
        }
    }

    /// <summary>
    /// 主聚光灯的开场压暗与渐亮。挂在灯自己身上，避免再养一个常驻协程宿主。
    /// 单独成文件（MonoBehaviour 类名必须与文件名一致）：AddComponent 才不会丢 MonoScript 映射。
    /// </summary>
    public sealed class StudioIntroLightDriver : MonoBehaviour
    {
        private Light _light;
        private float _baseIntensity;
        private Coroutine _routine;

        public void Play(float holdSeconds)
        {
            _light = GetComponent<Light>();
            if (_light == null || !_light.enabled) return;
            _baseIntensity = _light.intensity;
            if (_baseIntensity <= StudioIntroLights.DimIntensity) return;   // 本来就更暗，别把画面提亮
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(Mathf.Max(0f, holdSeconds)));
        }

        private IEnumerator Run(float hold)
        {
            _light.intensity = StudioIntroLights.DimIntensity;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            float rise = StudioIntroLights.RiseSeconds;
            for (float t = 0f; t < rise; t += Time.unscaledDeltaTime)
            {
                _light.intensity = Mathf.Lerp(StudioIntroLights.DimIntensity, _baseIntensity, Mathf.Clamp01(t / rise));
                yield return null;
            }
            _light.intensity = _baseIntensity;
            _routine = null;
        }

        /// <summary>被中途禁用/销毁时别把灯留在 0.1：先还原原强度再走。</summary>
        private void OnDisable()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
            if (_light != null) _light.intensity = _baseIntensity;
        }
    }
}
