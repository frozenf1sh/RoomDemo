// =============================================================================
// 文件：WindPresenter.cs
// 职责：表现风声、植物摆动与装饰纸张轻微飘动。
// 边界：这里只动画已成立的风事实；风的触发条件在领域规则中。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>表现风声、植物摆动与装饰纸张轻微飘动。</summary>
    public sealed class WindPresenter : WorldPresenter
    {
        [SerializeField] private Transform plantVisual;
        [SerializeField] private Transform[] papers;
        [SerializeField] private AudioSource windAudio;
        private Vector3[] paperOrigins;
        private Quaternion plantRotation;
        private Quaternion[] paperRotations;
        private bool activeWind;
        private float originalVolume, duck = 1, targetDuck = 1;
        /// <summary>已应用的风表现开关，不是独立世界状态。</summary>
        public bool IsWindVisible => activeWind;
        /// <summary>风循环音源，便于验证播放状态。</summary>
        public AudioSource WindAudio => windAudio;
        /// <summary>注入植物视觉、装饰纸张与风声音源。</summary>
        public void Configure(Transform plant, Transform[] sheets, AudioSource audio) { plantVisual = plant; papers = sheets; windAudio = audio; }
        /// <summary>缓存原始姿态，使关闭风与重开游戏能够恢复。</summary>
        private void Awake()
        {
            // 记录旋转和纸张姿态，风停止或 Restart 时恢复原貌。
            originalVolume = windAudio.volume;
            plantRotation = plantVisual.localRotation;
            paperOrigins = new Vector3[papers.Length]; paperRotations = new Quaternion[papers.Length];
            for (int i = 0; i < papers.Length; i++) { paperOrigins[i] = papers[i].localPosition; paperRotations[i] = papers[i].localRotation; }
        }
        /// <summary>演出短暂降低风声以凸显新声源，不影响风的领域事实。</summary>
        public void SetAudioDuck(float level, bool immediate = false)
        {
            targetDuck = Mathf.Clamp01(level);
            if (immediate) { duck = targetDuck; windAudio.volume = originalVolume * duck; }
        }
        /// <summary>仅接收风进入事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WindEntering) ApplyState(Session.State, true); }
        /// <summary>根据风事实开关音频；恢复时先归位所有摆动对象。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 缓存已收到的风事实用于动画，不检查植物加窗户的规则条件。
            activeWind = state.HasFact(DemoRules.WindEntering);
            if (activeWind && !windAudio.isPlaying) windAudio.Play();
            if (!activeWind) windAudio.Stop();
            if (!animate)
            {
                SetAudioDuck(1, true);
                plantVisual.localRotation = plantRotation;
                for (int i = 0; i < papers.Length; i++) { papers[i].localPosition = paperOrigins[i]; papers[i].localRotation = paperRotations[i]; }
            }
        }
        /// <summary>用正弦函数表现轻摆与飘动，不执行世界规则。</summary>
        private void Update()
        {
            duck = Mathf.MoveTowards(duck, targetDuck, Time.deltaTime * 2); windAudio.volume = originalVolume * duck;
            if (!activeWind) return;
            float t = Time.time;
            plantVisual.localRotation = plantRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * 2.5f) * 5);
            for (int i = 0; i < papers.Length; i++)
            {
                // 各装饰纸张使用不同相位，保持轻微飘动而无需真实物理。
                float phase = t * 2.2f + i * 1.7f;
                papers[i].localPosition = paperOrigins[i] + new Vector3(Mathf.Sin(phase) * .07f, .05f + Mathf.Sin(phase + .4f) * .04f, 0);
                papers[i].localRotation = paperRotations[i] * Quaternion.Euler(Mathf.Sin(phase) * 8f, 0, Mathf.Cos(phase) * 5f);
            }
        }
    }
}
