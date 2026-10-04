// =============================================================================
// 文件：InspectionView.cs
// 职责：将有限检视状态映射到封面、书页等显示层，管理弯曲纸页的短暂过渡。
// 边界：只消费应用层检视状态；翻页进度不决定观察许可，不产生领域事实。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>少量状态的近景视觉层；翻页采用网格弯曲，旧技术样板仍可使用空转页节点。</summary>
    public sealed class InspectionView : MonoBehaviour
    {
        [SerializeField] private string[] stateIds;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Transform turningPage;
        [SerializeField] private float turnDuration = .62f;
        private PaperBendVisual bend;
        private float turn = 1;

        /// <summary>当前是否还在翻页，仅属于视觉状态，不影响 InspectionModel 的动作许可。</summary>
        public bool IsTurning => turn < 1;

        /// <summary>由 Prefab 安装器绑定有限状态与相应显示层。</summary>
        /// <param name="states">与应用层检视定义一致的稳定状态标识。</param>
        /// <param name="visuals">封面或书页的独立显示层，与 states 逐项对应。</param>
        /// <param name="page">临时翻动纸页的父节点；技术样板可传没有网格的空节点。</param>
        public void Configure(string[] states, GameObject[] visuals, Transform page)
        {
            stateIds = states;
            pages = visuals;
            turningPage = page;
        }

        private void Awake()
        {
            // 一次取得可选弯曲组件；每帧不会查找、创建网格或复制材质。
            bend = turningPage.GetComponentInChildren<PaperBendVisual>(true);
        }

        /// <summary>应用已有检视状态；恢复直接显示最终页，交互切页才播放短暂纸张弯曲。</summary>
        /// <param name="state">应用层确认后的有限状态。</param>
        /// <param name="animate">false 用于初始化、恢复与测试预设。</param>
        public void ApplyState(string state, bool animate)
        {
            for (int i = 0; i < pages.Length; i++)
                pages[i].SetActive(stateIds[i] == state);
            turn = animate ? 0 : 1;
            turningPage.gameObject.SetActive(animate);
            turningPage.localRotation = Quaternion.identity;
            if (bend != null) bend.ApplyProgress(turn);
        }

        private void Update()
        {
            if (turn >= 1) return;
            turn = Mathf.Min(1, turn + Time.unscaledDeltaTime / turnDuration);
            // 纸页沿网格横向卷曲并越过书脊；Transform 本身没有僵硬的九十度翻转。
            if (bend != null) bend.ApplyProgress(Mathf.SmoothStep(0, 1, turn));
            if (turn >= 1) turningPage.gameObject.SetActive(false);
        }
    }
}
