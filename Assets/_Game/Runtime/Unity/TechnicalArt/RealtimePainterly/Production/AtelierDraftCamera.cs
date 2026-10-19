// 文件：AtelierDraftCamera.cs
// 职责：保存主画室局部底稿的纯视觉参数，与实验按钮和游戏事实解耦。
// 边界：外部装配器传入阶段；本组件不查询领域、不提交命令、不切换质量管线。
using UnityEngine;
namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>同一主机位和近景使用世界锚定的底稿；时间只影响插值。</summary>
    public sealed class AtelierDraftCamera : MonoBehaviour
    {
        [Range(0,1)] public float LeftDraft;
        [Range(0,1)] public float WindowDraft;
        [Range(0,1)] public float Density = .98f;
        [Range(0,1)] public float Ghost;
        private float leftTarget, windowTarget, openingAge = 100;
        /// <summary>只接收已经成立的阶段，第三和第四阶段均恢复全景。</summary>
        public void RestoreStage(int stage, bool immediate)
        {
            leftTarget = stage == 1 ? 1 : 0;
            windowTarget = stage > 0 && stage <= 2 ? 1 : 0;
            if (immediate) { LeftDraft = leftTarget; WindowDraft = windowTarget; }
        }
        /// <summary>读完并合上便条后，才开始一次轻重影和稳定底稿过渡。</summary>
        public void PlayOpening() { openingAge = 0; RestoreStage(1,false); }
        /// <summary>跳过只结束视觉，不能补发或推进阶段事实。</summary>
        public void SkipOpening() { openingAge = 100; Ghost = 0; LeftDraft = leftTarget; WindowDraft = windowTarget; }
        private void Update()
        {
            openingAge += Time.deltaTime;
            Ghost = openingAge < .32f ? Mathf.Sin(openingAge / .32f * Mathf.PI) * .42f : 0;
            LeftDraft = Mathf.MoveTowards(LeftDraft,leftTarget,Time.deltaTime/.95f);
            WindowDraft = Mathf.MoveTowards(WindowDraft,windowTarget,Time.deltaTime/.95f);
        }
    }
}
