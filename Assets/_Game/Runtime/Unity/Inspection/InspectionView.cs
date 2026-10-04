// =============================================================================
// 文件：InspectionView.cs
// 职责：按有限页状态切换近景视觉，并播放简单翻页过渡。
// 边界：不判断线索产出；页旋转和显示只消费临时检视状态。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>可复用的少量状态视觉层；一个状态只显示对应的那层。</summary>
    public sealed class InspectionView : MonoBehaviour
    {
        [SerializeField] private string[] stateIds;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Transform turningPage;
        private float turn = 1;
        public void Configure(string[] states, GameObject[] visuals, Transform page) { stateIds = states; pages = visuals; turningPage = page; }
        public void ApplyState(string state, bool animate)
        {
            // 切页不重建模型；第一页和封面可以由不同的显示层表达。
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(stateIds[i] == state);
            turn = animate ? 0 : 1; turningPage.gameObject.SetActive(animate);
        }
        private void Update()
        {
            if (turn >= 1) return;
            turn = Mathf.Min(1, turn + Time.unscaledDeltaTime / .32f);
            turningPage.localRotation = Quaternion.Euler(0, Mathf.Lerp(0, -170, Mathf.SmoothStep(0, 1, turn)), 0);
            if (turn >= 1) turningPage.gameObject.SetActive(false);
        }
    }
}
