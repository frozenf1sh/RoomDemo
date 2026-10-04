// =============================================================================
// 文件：WindowPresenter.cs
// 职责：表现闭塞近景、远方和海景，让原野连续被海平线取代。
// 边界：只设置视觉层与 Shader 目标；不通过时间或动画产生 window.ocean。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>窗外三个层级与海景的连续覆盖，不用缩放窗口制造过渡。</summary>
    public sealed class WindowPresenter : WorldPresenter
    {
        [SerializeField] private GameObject nearBackground;
        [SerializeField] private GameObject farBackground;
        [SerializeField] private GameObject oceanBackground;
        [SerializeField] private AudioSource oceanAudio;
        private Renderer surface;
        private MaterialPropertyBlock block;
        private float reveal, target;
        /// <summary>远方背景层，供场景绑定与表现验证使用。</summary>
        public GameObject FarBackground => farBackground;
        /// <summary>海景覆盖层，始终保持真实窗口比例。</summary>
        public GameObject OceanBackground => oceanBackground;
        /// <summary>已经展现的海景覆盖进度，不是领域状态。</summary>
        public float OceanProgress => reveal;
        /// <summary>注入近、远、海三个视觉层。</summary>
        public void Configure(GameObject near, GameObject far, GameObject sea)
        { nearBackground = near; farBackground = far; oceanBackground = sea; }
        /// <summary>注入来自窗外的声音，引导玩家自行转头。</summary>
        public void ConfigureOceanAudio(AudioSource audio) { oceanAudio = audio; }
        /// <summary>初始化缓存海景表面和属性块，不每帧搜索层级。</summary>
        private void Awake()
        {
            surface = oceanBackground.transform.Find("OceanSky").GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }
        /// <summary>远方与海景有独立入口；导演只延迟海景节拍。</summary>
        protected override void OnFact(FactId fact)
        {
            if (fact == DemoRules.WindowFar) ShowFar();
            if (fact == DemoRules.WindowOcean) PlayOceanTransition();
        }
        /// <summary>打开远方视野；增加自然光由灯光表现负责。</summary>
        public void ShowFar()
        {
            nearBackground.SetActive(false); farBackground.SetActive(true); oceanBackground.SetActive(false);
            reveal = target = 0; SetParameters();
        }
        /// <summary>从同一原野开始连续变海，声音先提示窗外有异常。</summary>
        public void PlayOceanTransition()
        {
            nearBackground.SetActive(false); farBackground.SetActive(true); oceanBackground.SetActive(true);
            target = 1; oceanAudio.Play();
        }
        /// <summary>立即恢复视觉终态；跳转不重播一次性窗外音效。</summary>
        public void SetFinalVisual(bool far, bool ocean)
        {
            target = reveal = ocean ? 1 : 0;
            nearBackground.SetActive(!far && !ocean); farBackground.SetActive(far && !ocean); oceanBackground.SetActive(ocean);
            oceanBackground.transform.localScale = Vector3.one; oceanAudio.Stop(); SetParameters();
        }
        /// <summary>事实恢复与视觉预览共用参数路径，恢复模式取消旧过渡。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            if (!animate) SetFinalVisual(state.HasFact(DemoRules.WindowFar), state.HasFact(DemoRules.WindowOcean));
            else if (state.HasFact(DemoRules.WindowOcean)) PlayOceanTransition();
            else if (state.HasFact(DemoRules.WindowFar)) ShowFar();
        }
        /// <summary>只推进已启动的海面覆盖，不查询或执行规则。</summary>
        private void Update()
        {
            // 目标值由事实事件或恢复入口设定；这一帧只推进已获许可的视觉过渡。
            reveal = Mathf.MoveTowards(reveal, target, Time.deltaTime / 3);
            if (reveal >= 1) farBackground.SetActive(false);
            SetParameters();
        }
        /// <summary>复用属性块，远方与海景使用相同画面坐标以免切换跳动。</summary>
        private void SetParameters()
        {
            block.Clear(); block.SetFloat(ShaderIds.Transition, reveal); block.SetFloat(ShaderIds.Wetness, 0);
            block.SetFloat(ShaderIds.Emission, reveal * .15f); surface.SetPropertyBlock(block);
        }
    }
}
