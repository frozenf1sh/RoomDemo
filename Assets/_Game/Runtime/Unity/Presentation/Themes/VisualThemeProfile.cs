// =============================================================================
// 文件：VisualThemeProfile.cs
// 职责：以很薄的表现数据统一环境光、雾、局部光与氛围声音。
// 边界：主题不是世界事实，不参与配方；美术参数不得进入领域层。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>可由美术调参数的整体色彩脚本，没有规则、事件或编辑器框架。</summary>
    public sealed class VisualThemeProfile : ScriptableObject
    {
        public Color Ambient = new Color(.18f, .23f, .34f), Fog = new Color(.07f, .11f, .19f);
        public Color Sun = new Color(.55f, .66f, .89f), Fill = new Color(.92f, .56f, .31f);
        public float FogDensity = .006f, SunIntensity = .85f, FillIntensity = 1.1f, WindowIntensity = .15f, SeaIntensity;
        public float StyleBlend = 1;
        /// <summary>整体主题的主光方向；星空中月光与室内台灯形成冷暖层次。</summary>
        public Vector3 SunEuler = new Vector3(38, -32, 0);
        public AudioClip Ambience;
        // 后续绘画风格可配合 MaterialTransition 和全局风格权重，无需改领域。
    }
}
