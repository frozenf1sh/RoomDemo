// =============================================================================
// 文件：PlantPresenter.cs
// 职责：区分生长的尺度变化与水生化的材质、叶片和运动性质变化。
// 边界：只缓存表现目标；不从 Transform 读回领域，也不推导水与植物的规则。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>独立植物表现，复用共享材质并用属性块逐渐湿润。</summary>
    public sealed class PlantPresenter : WorldPresenter
    {
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject aquaticVisual;
        [SerializeField] private AudioSource growAudio;
        [SerializeField] private AudioSource aquaticAudio;
        private Vector3 initialScale, initialPosition, targetScale, targetPosition, aquaticScale;
        private Renderer[] leaves;
        private Color[] normalColors;
        private float[] normalGloss;
        private Transform[] aquaticLeaves;
        private Quaternion[] aquaticRotations;
        private MaterialPropertyBlock block;
        private bool shownGrown, shownAquatic;
        private float aquaticProgress;
        /// <summary>叶片视觉根，禁止用它判断世界是否已生长。</summary>
        public Transform Visual => visual;
        /// <summary>水生附加叶片，独立于基本植物模型。</summary>
        public GameObject AquaticVisual => aquaticVisual;
        /// <summary>已经展现的水生转变进度，不是领域事实。</summary>
        public float AquaticProgress => aquaticProgress;
        /// <summary>保持原始灰盒配置入口，仅注入表现资源。</summary>
        public void Configure(Transform value, GameObject kelp, AudioSource audio)
        { visual = value; aquaticVisual = kelp; growAudio = audio; }
        /// <summary>为水生变化绑定单独的空间声音。</summary>
        public void ConfigureAquaticAudio(AudioSource audio) { aquaticAudio = audio; }
        /// <summary>缓存初始姿态、共享材质参数与附加叶片旋转。</summary>
        private void Awake()
        {
            initialScale = visual.localScale; initialPosition = visual.localPosition; aquaticScale = aquaticVisual.transform.localScale;
            leaves = visual.GetComponentsInChildren<Renderer>(true);
            normalColors = new Color[leaves.Length]; normalGloss = new float[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                // 读取共享材质即可，不通过 Renderer.material 生成副本。
                normalColors[i] = leaves[i].sharedMaterial.color; normalGloss[i] = leaves[i].sharedMaterial.GetFloat("_Smoothness");
            }
            aquaticLeaves = new Transform[aquaticVisual.transform.childCount]; aquaticRotations = new Quaternion[aquaticLeaves.Length];
            for (int i = 0; i < aquaticLeaves.Length; i++) { aquaticLeaves[i] = aquaticVisual.transform.GetChild(i); aquaticRotations[i] = aquaticLeaves[i].localRotation; }
            block = new MaterialPropertyBlock();
        }
        /// <summary>两个事实各自启动对应变化，不判断任何前置组合。</summary>
        protected override void OnFact(FactId fact)
        {
            if (fact == DemoRules.PlantGrown) { SetVisual(true, false, true); growAudio.Play(); }
            if (fact == DemoRules.PlantAquatic) PlayAquaticTransition();
        }
        /// <summary>演出导演或调试入口启动水生形态，不改变会话。</summary>
        public void PlayAquaticTransition()
        {
            SetVisual(true, true, true);
            aquaticAudio.Play();
        }
        /// <summary>具体视觉层状态，供预览或领域状态恢复共用。</summary>
        public void SetVisual(bool grown, bool aquatic, bool animate)
        {
            shownGrown = grown; shownAquatic = aquatic;
            targetScale = Vector3.Scale(initialScale, aquatic ? new Vector3(1.50f, 2.35f, 1.4f) : grown ? Vector3.one * 1.85f : Vector3.one);
            targetPosition = initialPosition + (grown ? new Vector3(-.27f, 0, 0) : Vector3.zero);
            aquaticVisual.SetActive(aquatic);
            if (!animate)
            {
                // 预设和重开立即到位，取消旧形态目标与一次性声音。
                aquaticProgress = aquatic ? 1 : 0; visual.localScale = targetScale; visual.localPosition = targetPosition;
                growAudio.Stop(); aquaticAudio.Stop();
                for (int i = 0; i < aquaticLeaves.Length; i++) aquaticLeaves[i].localRotation = aquaticRotations[i];
            }
            ApplySurface();
        }
        /// <summary>只读取既有事实，恢复不等待任何演出回调。</summary>
        public override void ApplyState(GameState state, bool animate)
        { SetVisual(state.HasFact(DemoRules.PlantGrown), state.HasFact(DemoRules.PlantAquatic), animate); }
        /// <summary>推进形态和湿润参数，水生叶片轻摆与原来的风摆相互独立。</summary>
        private void Update()
        {
            visual.localScale = Vector3.MoveTowards(visual.localScale, targetScale, Time.deltaTime * (shownAquatic ? .6f : 1.5f));
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, targetPosition, Time.deltaTime * .35f);
            aquaticProgress = Mathf.MoveTowards(aquaticProgress, shownAquatic ? 1 : 0, Time.deltaTime / 2.8f);
            if (shownAquatic)
                for (int i = 0; i < aquaticLeaves.Length; i++) aquaticLeaves[i].localRotation = aquaticRotations[i] * Quaternion.Euler(Mathf.Sin(Time.time * 1.1f + i) * aquaticProgress * 8, 0, Mathf.Cos(Time.time + i) * aquaticProgress * 5);
            ApplySurface();
        }
        /// <summary>附加叶片缓慢展开，普通叶片从干绿变为湿润青绿。</summary>
        private void ApplySurface()
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
