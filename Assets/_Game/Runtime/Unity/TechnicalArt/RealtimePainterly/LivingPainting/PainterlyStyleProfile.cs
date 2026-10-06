// 文件：PainterlyStyleProfile.cs
// 职责：定义现实、触碰、梦境、终章四级表现权重。
// 边界：只混合已冻结的基础绘画结果，不改变算法，不保存游戏事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>四级共享同一套笔触算法；等级只决定绘画参与的比例。</summary>
    [CreateAssetMenu(menuName = "游戏/技术美术/绘画风格档位")]
    public sealed class PainterlyStyleProfile : ScriptableObject
    {
        public string DisplayName;
        [Range(0, 1)] public float PainterlyIntensity;

        /// <summary>应用表现权重。连续演出使用摄像机入口插值，避免突变和历史重置。</summary>
        /// <param name="camera">具有绘画基础渲染入口的摄像机。</param>
        public void Apply(PainterlyCamera camera)
        {
            // 算法参数保持冻结值，四档不成为四种材质或四种画风。
            if (camera != null) camera.SetPainterlyIntensity(PainterlyIntensity);
        }
    }
}
