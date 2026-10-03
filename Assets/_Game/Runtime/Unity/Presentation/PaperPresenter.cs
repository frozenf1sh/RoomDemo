// =============================================================================
// 文件：PaperPresenter.cs
// 职责：表现纸张从画框后滑到可调查位置的过程。
// 边界：移动完整交互根对象，保持视觉与 Collider 一致。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>表现纸张从画框后滑到可调查位置的过程。</summary>
    public sealed class PaperPresenter : WorldPresenter
    {
        [SerializeField] private Transform sheet;
        [SerializeField] private Vector3 revealedPosition;
        [SerializeField] private Vector3 revealedRotation;
        private Vector3 hiddenPosition;
        private Quaternion hiddenRotation;
        private Vector3 target;
        private Quaternion targetRotation;
        /// <summary>带有视觉和交互结构的纸张根对象。</summary>
        public Transform Sheet => sheet;
        /// <summary>注入完整纸张根对象与显露后的最终姿态。</summary>
        public void Configure(Transform paper, Vector3 position, Vector3 rotation) { sheet = paper; revealedPosition = position; revealedRotation = rotation; }
        /// <summary>缓存纸张被遮挡时的位置与旋转。</summary>
        private void Awake() { hiddenPosition = sheet.localPosition; hiddenRotation = sheet.localRotation; }
        /// <summary>仅接收纸张显露事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.PaperRevealed) ApplyState(Session.State, true); }
        /// <summary>从事实确定目标位置，恢复时直接归位或显露。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 移动的是带 Logic 与 Visual 的根对象，使点击位置和视觉同步。
            bool revealed = state.HasFact(DemoRules.PaperRevealed); target = revealed ? revealedPosition : hiddenPosition; targetRotation = revealed ? Quaternion.Euler(revealedRotation) : hiddenRotation;
            if (!animate) { sheet.localPosition = target; sheet.localRotation = targetRotation; }
        }
        /// <summary>移动纸张根对象，让 Collider 与 Visual 一起滑动。</summary>
        private void Update() { sheet.localPosition = Vector3.MoveTowards(sheet.localPosition, target, Time.deltaTime * .7f); sheet.localRotation = Quaternion.RotateTowards(sheet.localRotation, targetRotation, Time.deltaTime * 65); }
    }
}
