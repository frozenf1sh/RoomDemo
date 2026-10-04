// =============================================================================
// 文件：VisualThemeController.cs
// 职责：从进入演出时的实际光照平滑过渡到主题，并能归还原来的表现参数。
// 边界：只改变渲染与声音；具体导演负责借用光照控制权，不查询或添加世界事实。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    public sealed class VisualThemeController : MonoBehaviour
    {
        [SerializeField] private Light sun, fill, window, sea;
        [SerializeField] private AudioSource ambience;
        private Color initialAmbient, initialFog, initialSun, initialFill;
        private float initialDensity, sunIntensity, fillIntensity, windowIntensity, seaIntensity;
        private bool initialFogEnabled;
        private FogMode initialFogMode;
        private VisualThemeProfile target;
        private Quaternion initialSunRotation;
        private static readonly int StyleBlend = Shader.PropertyToID("_WorldStyleBlend");
        public void Configure(Light daylight, Light indoor, Light natural, Light ocean, AudioSource sound)
        { sun = daylight; fill = indoor; window = natural; sea = ocean; ambience = sound; }
        /// <summary>进入演出时才缓存，让远方已发生的光照也能作为正确起点。</summary>
        public void Capture()
        {
            initialAmbient = RenderSettings.ambientLight; initialFog = RenderSettings.fogColor; initialDensity = RenderSettings.fogDensity;
            initialFogEnabled = RenderSettings.fog; initialFogMode = RenderSettings.fogMode;
            initialSun = sun.color; initialFill = fill.color; sunIntensity = sun.intensity; fillIntensity = fill.intensity; windowIntensity = window.intensity; seaIntensity = sea.intensity;
            initialSunRotation = sun.transform.rotation;
            target = null;
        }
        /// <summary>只插值既有数据，不每帧创建材质、查找对象或计算世界反应。</summary>
        public void Transition(VisualThemeProfile profile, float progress)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            if (target != profile) { target = profile; ambience.clip = profile.Ambience; ambience.Play(); }
            RenderSettings.ambientLight = Color.Lerp(initialAmbient, profile.Ambient, t);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Color.Lerp(initialFog, profile.Fog, t); RenderSettings.fogDensity = Mathf.Lerp(initialDensity, profile.FogDensity, t);
            sun.color = Color.Lerp(initialSun, profile.Sun, t); sun.intensity = Mathf.Lerp(sunIntensity, profile.SunIntensity, t);
            sun.transform.rotation = Quaternion.Slerp(initialSunRotation, Quaternion.Euler(profile.SunEuler), t);
            fill.color = Color.Lerp(initialFill, profile.Fill, t); fill.intensity = Mathf.Lerp(fillIntensity, profile.FillIntensity, t);
            window.intensity = Mathf.Lerp(windowIntensity, profile.WindowIntensity, t); sea.intensity = Mathf.Lerp(seaIntensity, profile.SeaIntensity, t);
            ambience.volume = .23f * t; Shader.SetGlobalFloat(StyleBlend, profile.StyleBlend * t);
        }
        /// <summary>归还借用的光照，之后既有房间 Presenter 可以恢复当前事实对应的视觉。</summary>
        public void ResetTheme()
        {
            if (target == null) return;
            RenderSettings.ambientLight = initialAmbient; RenderSettings.fogColor = initialFog; RenderSettings.fogDensity = initialDensity;
            RenderSettings.fog = initialFogEnabled; RenderSettings.fogMode = initialFogMode;
            // 卸载时灯光或音源可能已经销毁，仍须安全释放全局主题参数。
            if (sun != null) { sun.color = initialSun; sun.intensity = sunIntensity; sun.transform.rotation = initialSunRotation; }
            if (fill != null) { fill.color = initialFill; fill.intensity = fillIntensity; }
            if (window != null) window.intensity = windowIntensity;
            if (sea != null) sea.intensity = seaIntensity;
            if (ambience != null) ambience.Stop();
            Shader.SetGlobalFloat(StyleBlend, 0); target = null;
        }
    }
}
