// 文件：ShaderIds.cs。职责：集中缓存主物体 Shader 属性；不包含任何领域规则。
using UnityEngine;
namespace Game.Unity
{
    /// <summary>统一属性入口，避免每帧查找字符串或重复创建属性块。</summary>
    public static class ShaderIds
    {
        public static readonly int Transition = Shader.PropertyToID("_Transition");
        public static readonly int Wetness = Shader.PropertyToID("_Wetness");
        public static readonly int Emission = Shader.PropertyToID("_Emission");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        // 属性 ID 只供表现使用；不能作为配方或 Reaction 条件。
    }
}
