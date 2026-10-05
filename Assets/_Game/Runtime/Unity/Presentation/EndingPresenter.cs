// =============================================================================
// 文件：EndingPresenter.cs
// 职责：管理单房间的暖灰、远方、海光和涌现光照，并显示临时结束提示。
// 边界：颜色进度只是演出状态；完成与远方条件由领域产生，不写领域事实。
// =============================================================================
using Game.Domain;
using UnityEngine;
using UnityEngine.Rendering;
namespace Game.Unity
{
    /// <summary>此房间具体的光照与结束表现，供导演按节奏调用。</summary>
    public sealed class EndingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject endingPanel;
        [SerializeField] private Light roomLight;
        [SerializeField] private AudioSource ambience;
        [SerializeField] private Light warmFill;
        [SerializeField] private Light windowLight;
        [SerializeField] private Light seaLight;
        private bool lightingBorrowed;
        private bool suppressEnding;
        /// <summary>主线继续进行时保留氛围变化，收起历史样板结束面板。</summary>
        public void SuppressBenchmarkEnding(bool hide) { suppressEnding=hide; if(hide) endingPanel.SetActive(false); }
        /// <summary>具体世界导演借用整体光照，避免两个表现消费者每帧互相覆盖。</summary>
        public void SetLightingOverride(bool borrowed) { lightingBorrowed = borrowed; }
        private float far, sea, finale, targetFar, targetSea, targetFinale;
        /// <summary>临时结束面板，只在最终氛围节拍显示。</summary>
        public GameObject EndingPanel => endingPanel;
        /// <summary>最终氛围表现的进度，供调试查看。</summary>
        public float AtmosphereProgress => finale;
        /// <summary>注入旧版结束资源，保留灰盒生成器兼容入口。</summary>
        public void Configure(GameObject panel, Light light, AudioSource audio)
        { endingPanel = panel; roomLight = light; ambience = audio; }
        /// <summary>注入三个局部灯，所有灯只由本组件控制，避免互相覆盖。</summary>
        public void ConfigureLighting(Light fill, Light natural, Light painting)
        { warmFill = fill; windowLight = natural; seaLight = painting; }
        /// <summary>远方立即进入过渡；海与终态由演出导演延迟转发。</summary>
        protected override void OnFact(FactId fact)
        {
            if (fact == DemoRules.WindowFar) { targetFar = 1; }
            if (fact == DemoRules.PaintingSea) PlaySeaLighting();
            if (fact == DemoRules.Complete) PlayAtmosphere();
        }
        /// <summary>开始局部冷色侵入，不立即把全屋染成蓝色。</summary>
        public void PlaySeaLighting() { targetSea = 1; }
        /// <summary>最后一个节拍才扩大外部海光，并开启完成提示。</summary>
        public void PlayAtmosphere()
        {
            targetFinale = 1;
            endingPanel.SetActive(!suppressEnding);
            if (!ambience.isPlaying) ambience.Play();
        }
        /// <summary>调试只设置光照表现，不创建或改写领域状态。</summary>
        public void SetPresentation(bool farVisible, bool seaVisible, bool completeVisible)
        {
            targetFar = far = farVisible ? 1 : 0; targetSea = sea = seaVisible ? 1 : 0; targetFinale = finale = completeVisible ? 1 : 0;
            endingPanel.SetActive(completeVisible && !suppressEnding);
            if (completeVisible && !ambience.isPlaying) ambience.Play(); if (!completeVisible) ambience.Stop();
            SetLighting();
        }
        /// <summary>从领域终态直接恢复所有光照参数，不重播节拍。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            targetFar = state.HasFact(DemoRules.WindowFar) ? 1 : 0;
            targetSea = state.HasFact(DemoRules.PaintingSea) ? 1 : 0;
            targetFinale = state.HasFact(DemoRules.Complete) ? 1 : 0;
            endingPanel.SetActive(targetFinale > 0 && !suppressEnding);
            if (targetFinale > 0 && !ambience.isPlaying) ambience.Play();
            if (targetFinale == 0) ambience.Stop();
            if (!animate)
            {
                // 重开取消旧目标；终态预设不产生再次闪光或淡入。
                far = targetFar; sea = targetSea; finale = targetFinale;
                SetLighting();
            }
        }
        /// <summary>在三种局部影响之间插值，只推进已经设置的表现目标。</summary>
        private void Update()
        {
            far = Mathf.MoveTowards(far, targetFar, Time.deltaTime / 2);
            sea = Mathf.MoveTowards(sea, targetSea, Time.deltaTime / 4);
            finale = Mathf.MoveTowards(finale, targetFinale, Time.deltaTime / 3);
            SetLighting();
        }
        /// <summary>具体颜色脚本：低饱和暖灰 → 自然光 → 局部海光 → 开放海色。</summary>
        private void SetLighting()
        {
            if (lightingBorrowed) return;
            roomLight.color = Color.Lerp(new Color(1, .81f, .62f), new Color(.65f, .89f, .94f), finale);
            roomLight.intensity = .78f + far * .50f + finale * .35f;
            // 灰盒兼容模式允许尚未注入局部灯；正式美术场景由验证器要求全部绑定。
            if (warmFill != null) { warmFill.color = new Color(1, .74f, .52f); warmFill.intensity = 2.7f - finale * 1.1f; }
            if (windowLight != null) { windowLight.color = Color.Lerp(new Color(.78f, .84f, .82f), new Color(.36f, .80f, .92f), finale); windowLight.intensity = .20f + far * 1.3f + finale * 1.2f; }
            if (seaLight != null)
            {
                seaLight.intensity = sea * (.85f + finale * .55f);
                seaLight.color = new Color(.20f, .73f, .78f);
                // 很轻的水面反光只在海已经开始表现后出现，初始环境保持静止。
                seaLight.intensity *= 1 + sea * Mathf.Sin(Time.time * 1.3f) * .06f;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(new Color(.36f, .33f, .28f), new Color(.31f, .43f, .46f), Mathf.Clamp01(far * .30f + finale * .70f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Mathf.Lerp(.008f, .015f, finale);
            RenderSettings.fogColor = Color.Lerp(new Color(.30f, .28f, .25f), new Color(.25f, .43f, .46f), finale);
        }
    }
}
