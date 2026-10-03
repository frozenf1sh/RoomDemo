// =============================================================================
// 文件：PlantPresenter.cs
// 职责：将生长、水生化事实呈现为形态、位置、颜色与湿润材质变化。
// 边界：缓存的目标值仅用于动画；不从 Transform 反推领域状态。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>将生长、水生化事实呈现为形态、位置、颜色与湿润材质变化。</summary>
    public sealed class PlantPresenter : WorldPresenter
    {
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject aquaticVisual;
        [SerializeField] private AudioSource growAudio;
        private Vector3 initialScale, initialPosition, targetScale, targetPosition;
        private Renderer[] leaves;
        private Material[] materials;
        private Color[] normalColors;
        private float[] normalGloss;
        private bool aquatic;
        /// <summary>植物叶片视觉根，不用于判断领域状态。</summary>
        public Transform Visual => visual;
        /// <summary>水生形态的额外视觉子层。</summary>
        public GameObject AquaticVisual => aquaticVisual;
        /// <summary>注入叶片、额外水生形态与生长声音。</summary>
        public void Configure(Transform value, GameObject kelp, AudioSource audio) { visual = value; aquaticVisual = kelp; growAudio = audio; }
        /// <summary>缓存初始尺寸与材质，并取得本实例拥有的材质副本。</summary>
        private void Awake()
        {
            // 这些初始值仅用于恢复表现，绝不判断植物是否已生长。
            initialScale = visual.localScale; initialPosition = visual.localPosition;
            leaves = visual.GetComponentsInChildren<Renderer>(true); materials = new Material[leaves.Length]; normalColors = new Color[leaves.Length]; normalGloss = new float[leaves.Length];
            // Renderer.material 克隆属于本实例，销毁组件时必须释放。
            for (int i = 0; i < leaves.Length; i++) { materials[i] = leaves[i].material; normalColors[i] = materials[i].color; normalGloss[i] = materials[i].GetFloat("_Smoothness"); }
        }
        /// <summary>接收生长或水生化事实，更新目标并播放一次生长音效。</summary>
        protected override void OnFact(FactId fact)
        {
            if (fact != DemoRules.PlantGrown && fact != DemoRules.PlantAquatic) return;
            ApplyState(Session.State, true); if (fact == DemoRules.PlantGrown) growAudio.Play();
        }
        /// <summary>从事实选择尺寸、位置和湿润形态；恢复时立即设置。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            aquatic = state.HasFact(DemoRules.PlantAquatic);
            bool grown = state.HasFact(DemoRules.PlantGrown);
            // 水生化与第一次变大使用不同形态，保证两种事实有不同反馈。
            targetScale = Vector3.Scale(initialScale, aquatic ? new Vector3(1.50f, 2.35f, 1.4f) : grown ? Vector3.one * 1.85f : Vector3.one);
            targetPosition = initialPosition + (grown ? new Vector3(-.27f, 0, 0) : Vector3.zero);
            aquaticVisual.SetActive(aquatic);
            // 恢复直接设置终态并停止一次性声音，不从头播放生长过渡。
            if (!animate)
            {
                visual.localScale = targetScale; visual.localPosition = targetPosition; growAudio.Stop();
                for (int i = 0; i < materials.Length; i++) { materials[i].color = aquatic ? new Color(.07f, .63f, .62f) : normalColors[i]; materials[i].SetFloat("_Smoothness", aquatic ? .88f : normalGloss[i]); }
            }
        }
        /// <summary>向缓存的视觉目标插值，不判断任何世界反应。</summary>
        private void Update()
        {
            // 每帧只趋近已选定的目标，禁止在 Update 中推导领域反应。
            visual.localScale = Vector3.MoveTowards(visual.localScale, targetScale, Time.deltaTime * 1.5f);
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, targetPosition, Time.deltaTime * .35f);
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = Color.Lerp(materials[i].color, aquatic ? new Color(.07f, .63f, .62f) : normalColors[i], Time.deltaTime * 1.5f);
                materials[i].SetFloat("_Smoothness", Mathf.Lerp(materials[i].GetFloat("_Smoothness"), aquatic ? .88f : normalGloss[i], Time.deltaTime * 1.5f));
            }
        }
        /// <summary>释放本实例克隆的材质，并解除领域事件订阅。</summary>
        protected override void OnDestroy() { base.OnDestroy(); if (materials != null) foreach (var material in materials) Destroy(material); }
    }
}
