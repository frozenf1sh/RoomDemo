// 文件：AtelierInteractionTarget.cs
// 职责：在独立逻辑子节点上绑定画室可见来源和有限动作类型。
// 边界：稳定身份沿用领域内容；碰撞只供指针拾取，不驱动物体物理或修改规则。
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>当前阶段已经具备真实表现的交互对象类型。</summary>
    public enum AtelierTargetKind { Painting, WashWater, Brush, Manuscript, StillLamp, Window, OilLamp, Matches, MatchStriker, Wick, Curtain, CurtainCord }

    /// <summary>可见模型与逻辑拾取分开，模型替换不改变身份。</summary>
    public sealed class AtelierInteractionTarget : MonoBehaviour
    {
        public AtelierTargetKind Kind;
        public string ObjectId;
        public string DisplayName;
    }
}
