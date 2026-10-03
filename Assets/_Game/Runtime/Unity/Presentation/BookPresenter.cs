// =============================================================================
// 文件：BookPresenter.cs
// 职责：将书落下的事实呈现为位置、旋转与落地声音。
// 边界：动画使用插值，不以刚体碰撞决定是否成立 book.fallen。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>将书落下的事实呈现为位置、旋转与落地声音。</summary>
    public sealed class BookPresenter : WorldPresenter
    {
        [SerializeField] private Vector3 fallenPosition;
        [SerializeField] private AudioSource fallAudio;
        private Vector3 shelfPosition;
        private Quaternion shelfRotation;
        private float progress;
        private bool falling, hit;
        /// <summary>书落下后的表现终点，供场景配置与验证使用。</summary>
        public Vector3 FallenPosition => fallenPosition;
        /// <summary>注入落地位置与一次性落地声音。</summary>
        public void Configure(Vector3 position, AudioSource audio) { fallenPosition = position; fallAudio = audio; }
        /// <summary>缓存书架上的初始位置与旋转，用于重开恢复。</summary>
        private void Awake() { shelfPosition = transform.localPosition; shelfRotation = transform.localRotation; }
        /// <summary>仅接收书落下事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.BookFallen) ApplyState(Session.State, true); }
        /// <summary>设置落下动画目标或直接恢复到书架、地面。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 缓存是否展示下落；它不是第二份 book.fallen 业务状态。
            falling = state.HasFact(DemoRules.BookFallen);
            if (!animate) { progress = falling ? 1 : 0; hit = falling; fallAudio.Stop(); SetPose(); }
        }
        /// <summary>依据动画进度计算表现姿态，不读回游戏逻辑。</summary>
        private void SetPose()
        {
            // 平方进度近似加速下落，不引入物理规则或碰撞依赖。
            transform.localPosition = Vector3.Lerp(shelfPosition, fallenPosition, progress * progress);
            transform.localRotation = Quaternion.Slerp(shelfRotation, Quaternion.Euler(0, 18, 0), progress);
        }
        /// <summary>推进下落进度，并在视觉落地时播放一次声音。</summary>
        private void Update()
        {
            if (!falling || progress >= 1) return;
            progress = Mathf.Min(1, progress + Time.deltaTime / 1.25f); SetPose();
            // hit 只防止落地声音重复播放，不控制反应次数。
            if (progress >= 1 && !hit) { hit = true; fallAudio.Play(); }
        }
    }
}
