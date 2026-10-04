// =============================================================================
// 文件：CampfireVisual.cs
// 职责：把木柴、柔软粒子火舌、余烬、烟和局部暖光组合成真实的点燃过程。
// 边界：只做火的表现；不添加事实、不决定何时生成、不读取游戏配方。
// 资源：粒子和灯预先绑定，帧循环不创建材质、粒子系统或临时数组。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>星空外景的具体篝火消费者，接收导演的点燃比例。</summary>
    public sealed class CampfireVisual : MonoBehaviour
    {
        [SerializeField] private Transform[] flames = new Transform[0];
        [SerializeField] private ParticleSystem[] fireParticles = new ParticleSystem[0];
        [SerializeField] private ParticleSystem[] emberParticles = new ParticleSystem[0];
        [SerializeField] private ParticleSystem[] smokeParticles = new ParticleSystem[0];
        [SerializeField] private Light glow;
        [SerializeField] private AudioSource crackle;
        private float ignition = 1;
        private float brightness = 5;
        private float soundVolume;

        /// <summary>当前点燃进度只是视觉值，不表示 campfire 世界事实。</summary>
        public float IgnitionProgress => ignition;

        /// <summary>保留独立灰盒生成入口；尖火焰不用于第五阶段正式美术。</summary>
        public void Configure(Transform[] fire, Light light, AudioSource sound)
        {
            flames = fire;
            glow = light;
            crackle = sound;
        }

        /// <summary>绑定正式粒子火、余烬、烟与空间音源，木柴保持真实静止比例。</summary>
        public void ConfigureParticles(ParticleSystem[] flame, ParticleSystem[] embers, ParticleSystem[] smoke,
            Light light, AudioSource sound)
        {
            fireParticles = flame;
            emberParticles = embers;
            smokeParticles = smoke;
            flames = new Transform[0];
            glow = light;
            crackle = sound;
        }

        private void Awake()
        {
            brightness = glow.intensity;
            soundVolume = crackle.volume;
        }

        private void OnEnable()
        {
            if (crackle != null && ignition > 0) crackle.Play();
            ApplyParticles();
        }

        /// <summary>点燃是粒子密度和光源建立，不把木柴及整团火从极小尺寸放大。</summary>
        public void SetIgnition(float progress)
        {
            ignition = Mathf.Clamp01(progress);
            ApplyParticles();
            if (crackle != null)
            {
                crackle.volume = soundVolume * Mathf.SmoothStep(0, 1, ignition);
                if (ignition > .02f && !crackle.isPlaying) crackle.Play();
                if (ignition == 0) crackle.Stop();
            }
        }

        private void ApplyParticles()
        {
            SetEmission(fireParticles, 44, ignition);
            SetEmission(emberParticles, 10, ignition);
            SetEmission(smokeParticles, 7, ignition);
        }

        private static void SetEmission(ParticleSystem[] systems, float rate, float progress)
        {
            foreach (var system in systems)
            {
                if (system == null) continue;
                var emission = system.emission;
                emission.rateOverTime = rate * progress;
                if (progress > .01f && !system.isPlaying) system.Play();
                if (progress == 0) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Update()
        {
            // 两个不同尺度的连续噪声避免“正弦灯”般机械闪烁，光照保持暖色局部关系。
            float flicker = .78f + Mathf.PerlinNoise(Time.time * 5.9f, .47f) * .30f
                + Mathf.PerlinNoise(.37f, Time.time * 13.2f) * .13f;
            glow.intensity = brightness * ignition * flicker;
            glow.color = Color.Lerp(new Color(1, .29f, .075f), new Color(1, .60f, .22f), flicker * .55f);
        }

        private void OnDisable()
        {
            if (crackle != null) crackle.Stop();
            SetEmission(fireParticles, 0, 0);
            SetEmission(emberParticles, 0, 0);
            SetEmission(smokeParticles, 0, 0);
        }
    }
}
