// =============================================================================
// 文件：WindowPresenter.cs
// 职责：根据事实选择近景、远方或海洋，并表现海景展开。
// 边界：只读 window.far 与 window.ocean，不自行推导海洋反应。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>根据事实选择近景、远方或海洋，并表现海景展开。</summary>
    public sealed class WindowPresenter : WorldPresenter
    {
        [SerializeField] private GameObject nearBackground;
        [SerializeField] private GameObject farBackground;
        [SerializeField] private GameObject oceanBackground;
        private float reveal;
        private bool ocean;
        /// <summary>远方视觉层，仅供表现与场景验证。</summary>
        public GameObject FarBackground => farBackground;
        /// <summary>海洋视觉层，仅供表现与场景验证。</summary>
        public GameObject OceanBackground => oceanBackground;
        /// <summary>注入近景、远景与海景三个独立视觉层。</summary>
        public void Configure(GameObject near, GameObject far, GameObject sea) { nearBackground = near; farBackground = far; oceanBackground = sea; }
        /// <summary>接收远方或海洋事实，重新选择视觉层。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WindowFar || fact == DemoRules.WindowOcean) ApplyState(Session.State, true); }
        /// <summary>根据已成立事实选择层级，恢复时海景直接完全展开。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 直接读两个独立事实，海景在表现层覆盖远景。
            bool far = state.HasFact(DemoRules.WindowFar); ocean = state.HasFact(DemoRules.WindowOcean);
            // 激活状态只是表现，不能作为领域条件读回。
            nearBackground.SetActive(!far && !ocean); farBackground.SetActive(far && !ocean); oceanBackground.SetActive(ocean);
            // 恢复时完全展开海景；初始状态保留近乎零的视觉缩放。
            if (!animate) reveal = ocean ? 1 : 0;
            oceanBackground.transform.localScale = new Vector3(1, Mathf.Max(.001f, reveal), 1);
        }
        /// <summary>仅推进海景展开动画，不读取时间来触发事实。</summary>
        private void Update()
        {
            if (!ocean || reveal >= 1) return;
            reveal = Mathf.MoveTowards(reveal, 1, Time.deltaTime / 2.5f);
            oceanBackground.transform.localScale = new Vector3(1, Mathf.Max(.001f, reveal), 1);
        }
    }
}
