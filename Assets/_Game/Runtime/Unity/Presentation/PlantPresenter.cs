// =============================================================================
// 文件：PlantPresenter.cs
// 职责：将生长和水生事实映射到艺术植物的分枝显现、叶片舒展与梦境形态。
// 边界：只消费领域事实；不从模型轮廓判断规则，也不修改会话或其他 Presenter。
// =============================================================================
using Game.Domain;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>植物事实的表现适配器；主场景使用独立株形，技术灰盒保留简单回退表现。</summary>
    public sealed class PlantPresenter : WorldPresenter
    {
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject aquaticVisual;
        [SerializeField] private AudioSource growAudio;
        [SerializeField] private AudioSource aquaticAudio;
        [SerializeField] private ArtistPlantVisual artistVisual;
        private Vector3 initialScale, initialPosition, targetScale, targetPosition, aquaticScale;
        private Renderer[] leaves;
        private Color[] normalColors;
        private float[] normalGloss;
        private Transform[] aquaticLeaves;
        private Quaternion[] aquaticRotations;
        private MaterialPropertyBlock block;
        private bool shownGrown, shownAquatic;
        private float aquaticProgress;

        /// <summary>视觉根的姿态不是游戏事实，成熟植物不会在主场景整体放大。</summary>
        public Transform Visual => visual;
        /// <summary>具有独立长叶轮廓的梦境植物层。</summary>
        public GameObject AquaticVisual => aquaticVisual;
        /// <summary>当前已展现的梦境形态进度，不是领域状态。</summary>
        public float AquaticProgress => artistVisual != null ? artistVisual.DreamProgress : aquaticProgress;
        /// <summary>枝叶的视觉展开进度，替代用根节点缩放推断演出完成。</summary>
        public float GrowthProgress => artistVisual != null ? artistVisual.GrowthProgress : shownGrown ? 1 : 0;
        /// <summary>成熟枝叶层，用于美术调试与恢复检查。</summary>
        public GameObject GrownVisual => artistVisual != null ? artistVisual.GrownLayer : visual.gameObject;

        /// <summary>保持灰盒生成器的资源注入入口，不负责添加玩法数据。</summary>
        public void Configure(Transform value, GameObject kelp, AudioSource audio)
        {
            visual = value;
            aquaticVisual = kelp;
            growAudio = audio;
        }
        /// <summary>增量美术工具注入专用株形控制器，不改变实体、Collider 或 ID。</summary>
        public void ConfigureArtistVisual(ArtistPlantVisual value) { artistVisual = value; }
        /// <summary>为水生性质变化绑定独立空间声音。</summary>
        public void ConfigureAquaticAudio(AudioSource audio) { aquaticAudio = audio; }

        /// <summary>缓存灰盒回退所需姿态；美术株形由自己的网格和属性块管理。</summary>
        private void Awake()
        {
            initialScale = visual.localScale;
            initialPosition = visual.localPosition;
            aquaticScale = aquaticVisual.transform.localScale;
            if (artistVisual != null) return;
            leaves = visual.GetComponentsInChildren<Renderer>(true);
            normalColors = new Color[leaves.Length];
            normalGloss = new float[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                // 仅技术灰盒读取共享材质；正式植物不生成 Renderer.material 副本。
                normalColors[i] = leaves[i].sharedMaterial.color;
                normalGloss[i] = leaves[i].sharedMaterial.GetFloat("_Smoothness");
            }
            aquaticLeaves = new Transform[aquaticVisual.transform.childCount];
            aquaticRotations = new Quaternion[aquaticLeaves.Length];
            for (int i = 0; i < aquaticLeaves.Length; i++)
            {
                aquaticLeaves[i] = aquaticVisual.transform.GetChild(i);
                aquaticRotations[i] = aquaticLeaves[i].localRotation;
            }
            block = new MaterialPropertyBlock();
        }

        /// <summary>各事实只启动对应的视觉目标，不在这里计算反应条件。</summary>
        protected override void OnFact(FactId fact)
        {
            if (fact == ExplorationRules.PlantStartsGrowing)
            {
                SetStarting(true);
                growAudio.Play();
            }
            if (fact == DemoRules.PlantGrown || fact == ExplorationRules.PlantOvergrown)
            {
                bool first = !shownGrown;
                SetVisual(true, false, true);
                if (first) growAudio.Play();
            }
            if (fact == DemoRules.PlantAquatic) PlayAquaticTransition();
        }

        /// <summary>导演与调试入口播放梦境叶层，不改变领域会话。</summary>
        public void PlayAquaticTransition()
        {
            SetVisual(true, true, true);
            aquaticAudio.Play();
        }

        /// <summary>设置明确株形；恢复模式立即归位并取消旧音频和粒子。</summary>
        /// <param name="grown">是否显示已成熟的枝条。</param>
        /// <param name="aquatic">是否替换为梦境长叶轮廓。</param>
        /// <param name="animate">是否逐枝显现；不会改变事实已经成立的时间。</param>
        public void SetVisual(bool grown, bool aquatic, bool animate)
        {
            shownGrown = grown;
            shownAquatic = aquatic;
            if (artistVisual != null)
            {
                // 形态来自独立 Mesh 与显现参数，整株根节点始终保持人工构图的尺度。
                visual.localScale = initialScale;
                visual.localPosition = initialPosition;
                artistVisual.SetTarget(grown ? 1 : 0, aquatic, animate);
                if (!animate) StopTransitionAudio();
                return;
            }
            targetScale = Vector3.Scale(initialScale, aquatic ? new Vector3(1.50f, 2.35f, 1.4f) : grown ? Vector3.one * 1.85f : Vector3.one);
            targetPosition = initialPosition + (grown ? new Vector3(-.27f, 0, 0) : Vector3.zero);
            aquaticVisual.SetActive(aquatic);
            if (!animate)
            {
                aquaticProgress = aquatic ? 1 : 0;
                visual.localScale = targetScale;
                visual.localPosition = targetPosition;
                StopTransitionAudio();
                for (int i = 0; i < aquaticLeaves.Length; i++) aquaticLeaves[i].localRotation = aquaticRotations[i];
            }
            ApplyGreyboxSurface();
        }

        /// <summary>只读取植物事实，重开或预设切换直接恢复正确株形。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            bool mature = state.HasFact(DemoRules.PlantGrown) || state.HasFact(ExplorationRules.PlantOvergrown);
            SetVisual(mature, state.HasFact(DemoRules.PlantAquatic), animate);
            if (!mature && state.HasFact(ExplorationRules.PlantStartsGrowing)) SetStarting(animate);
        }

        /// <summary>当天先显现少量新枝；次日的成熟事实决定完整枝叶层。</summary>
        private void SetStarting(bool animate)
        {
            SetVisual(false, false, animate);
            if (artistVisual != null)
            {
                artistVisual.SetTarget(.52f, false, animate);
                return;
            }
            targetScale = initialScale * 1.18f;
            if (!animate) visual.localScale = targetScale;
        }

        /// <summary>统一停止一次性声音，状态恢复不会重播上一轮演出。</summary>
        private void StopTransitionAudio()
        {
            growAudio.Stop();
            aquaticAudio.Stop();
        }

        /// <summary>技术灰盒才使用缩放回退；正式场景动画完全由专用枝叶视觉组件推进。</summary>
        private void Update()
        {
            if (artistVisual != null) return;
            visual.localScale = Vector3.MoveTowards(visual.localScale, targetScale, Time.deltaTime * (shownAquatic ? .6f : 1.5f));
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, targetPosition, Time.deltaTime * .35f);
            aquaticProgress = Mathf.MoveTowards(aquaticProgress, shownAquatic ? 1 : 0, Time.deltaTime / 2.8f);
            if (shownAquatic)
                for (int i = 0; i < aquaticLeaves.Length; i++)
                    aquaticLeaves[i].localRotation = aquaticRotations[i] * Quaternion.Euler(Mathf.Sin(Time.time * 1.1f + i) * aquaticProgress * 8, 0, Mathf.Cos(Time.time + i) * aquaticProgress * 5);
            ApplyGreyboxSurface();
        }

        /// <summary>保留独立灰盒的湿润附加层，方便技术生成器验证事实映射。</summary>
        private void ApplyGreyboxSurface()
        {
            aquaticVisual.transform.localScale = aquaticScale * Mathf.Max(.001f, aquaticProgress);
            for (int i = 0; i < leaves.Length; i++)
            {
                block.Clear();
                block.SetColor(ShaderIds.BaseColor, Color.Lerp(normalColors[i], new Color(.10f, .49f, .43f), aquaticProgress));
                block.SetFloat(ShaderIds.Smoothness, Mathf.Lerp(normalGloss[i], .88f, aquaticProgress));
                leaves[i].SetPropertyBlock(block);
            }
        }
    }
}
