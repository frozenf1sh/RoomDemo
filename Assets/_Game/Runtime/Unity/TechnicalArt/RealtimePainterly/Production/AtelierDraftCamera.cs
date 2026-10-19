// 文件：AtelierDraftCamera.cs
// 职责：提供局部底稿、第一阶段外围遮挡与黑场失焦开场的纯视觉参数。
// 边界：不查询或写入领域事实；物件许可与开场输入锁由场景适配器管理。
using UnityEngine;
namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>通过世界表面的空间范围保留当前对象，近景移动不把遮罩钉在屏幕上。</summary>
    public sealed class AtelierDraftCamera : MonoBehaviour
    {
        [Range(0,1)] public float LeftDraft;
        [Range(0,1)] public float WindowDraft;
        [Range(0,1)] public float SurroundingsDraft;
        [Range(0,1)] public float Density = 1;
        [Range(0,1)] public float Ghost;
        public Renderer[] ClearObjects = System.Array.Empty<Renderer>();
        /// <summary>开场黑场量，仅影响世界颜色，Overlay旁白仍正常显示。</summary>
        public float OpeningDarkness { get; private set; }
        /// <summary>开场失焦量，归零后不继续执行多点采样。</summary>
        public float OpeningBlur { get; private set; }
        private float leftTarget, windowTarget, surroundingsTarget, openingAge = 100;

        /// <summary>第一阶段只保留当前交互物件；第二阶段解除外围与左侧，窗景继续遮挡。</summary>
        public void RestoreStage(int stage, bool immediate)
        {
            leftTarget = stage == 1 ? 1 : 0;
            windowTarget = stage > 0 && stage <= 2 ? 1 : 0;
            surroundingsTarget = stage == 1 ? 1 : 0;
            if (immediate)
            {
                LeftDraft = leftTarget;
                WindowDraft = windowTarget;
                SurroundingsDraft = surroundingsTarget;
            }
        }

        /// <summary>进场立即建立底稿和黑场，不依赖便条点击，也不授予物件。</summary>
        public void PlayOpening()
        {
            openingAge = 0;
            OpeningDarkness = 1;
            OpeningBlur = 1;
            RestoreStage(1,true);
        }

        private void Update()
        {
            openingAge += Time.deltaTime;
            OpeningDarkness = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,2.4f,openingAge));
            OpeningBlur = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,4.8f,openingAge));
            Ghost = openingAge < 1.1f ? Mathf.Sin(openingAge / 1.1f * Mathf.PI) * .18f : 0;
            LeftDraft = Mathf.MoveTowards(LeftDraft,leftTarget,Time.deltaTime/.95f);
            WindowDraft = Mathf.MoveTowards(WindowDraft,windowTarget,Time.deltaTime/.95f);
            SurroundingsDraft = Mathf.MoveTowards(SurroundingsDraft,surroundingsTarget,Time.deltaTime/.95f);
        }
    }
}
