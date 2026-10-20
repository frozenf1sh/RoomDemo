// 文件：AtelierPaintingSnapshot.cs
// 职责：定义画布只读展示合同与手势请求。边界：不拥有世界事实或纹理。
using System;
using UnityEngine;
namespace Game.Unity.UI.FourStage
{
    /// <summary>明确的工具身份，许可由适配器提供。</summary>
    public enum AtelierToolMode { Brush, Knife }
    /// <summary>不可变展示值，构造时复制，UI不能回写账本。</summary>
    public sealed class AtelierPaintingSnapshot
    {
        /// <summary>会话世代。</summary>
        public string SessionEpoch { get; }
        /// <summary>稳定笔刷ID。</summary>
        public string BrushId { get; }
        /// <summary>作品版本。</summary>
        public int CanvasRevision { get; }
        /// <summary>层序号。</summary>
        public int LayerId { get; }
        /// <summary>原始图样，由外部持有。</summary>
        public Texture PreviewTexture { get; }
        /// <summary>作品纹理，UI不得释放。</summary>
        public Texture CanvasTexture { get; }
        /// <summary>剩余量。</summary>
        public int RemainingPaint { get; }
        /// <summary>已确认笔数。</summary>
        public int CommittedStrokeCount { get; }
        /// <summary>计划笔数。</summary>
        public int PlannedStrokeCount { get; }
        /// <summary>合法成本下界。</summary>
        public int MinCost { get; }
        /// <summary>合法成本上界。</summary>
        public int MaxCost { get; }
        /// <summary>推荐成本。</summary>
        public int SelectedCost { get; }
        /// <summary>推荐角度。</summary>
        public int SelectedAngle { get; }
        /// <summary>撤销许可。</summary>
        public bool CanUndo { get; }
        /// <summary>收层许可。</summary>
        public bool CanFinish { get; }
        /// <summary>收层缺项。</summary>
        public string FinishBlockedReason { get; }
        /// <summary>当前工具。</summary>
        public AtelierToolMode ToolMode { get; }
        /// <summary>刀面许可。</summary>
        public bool CanUseKnife { get; }
        /// <summary>返回许可。</summary>
        public bool CanReturn { get; }
        /// <summary>提交中状态。</summary>
        public bool IsSubmitting { get; }
        /// <summary>包含开场、暂停、笔记和机位的显示许可。</summary>
        public bool IsVisible { get; }
        /// <summary>复制展示值，不转移纹理所有权。</summary>
        public AtelierPaintingSnapshot(string epoch, int revision, int layer, string brush, Texture preview,
            Texture canvas, int remaining, int count, int planned, int min, int max, int cost, int angle,
            bool undo, bool finish, string reason, bool visible = true, bool canReturn = true,
            AtelierToolMode tool = AtelierToolMode.Brush, bool knife = false, bool submitting = false)
        {
            SessionEpoch = epoch; CanvasRevision = revision; LayerId = layer; BrushId = brush;
            PreviewTexture = preview; CanvasTexture = canvas; RemainingPaint = remaining;
            CommittedStrokeCount = count; PlannedStrokeCount = planned; MinCost = min; MaxCost = max;
            SelectedCost = cost; SelectedAngle = angle; CanUndo = undo; CanFinish = finish;
            FinishBlockedReason = reason; IsVisible = visible; CanReturn = canReturn;
            ToolMode = tool; CanUseKnife = knife; IsSubmitting = submitting;
        }
    }
    /// <summary>按下时捕获参数，松开只补入UV。</summary>
    public sealed class AtelierStrokeIntent
    {
        /// <summary>按下时的不可变快照。</summary>
        public AtelierPaintingSnapshot Snapshot { get; }
        /// <summary>每次手势唯一身份。</summary>
        public string StrokeId { get; } = Guid.NewGuid().ToString("N");
        /// <summary>锁定成本。</summary>
        public int Cost { get; }
        /// <summary>锁定角度。</summary>
        public int Angle { get; }
        /// <summary>不创建第二份世界事实。</summary>
        public AtelierStrokeIntent(AtelierPaintingSnapshot snapshot, int cost, int angle)
        { Snapshot = snapshot; Cost = cost; Angle = angle; }
    }
    /// <summary>真实适配与编辑器预览共用的专用端口。</summary>
    public interface IAtelierPaintingPort
    {
        /// <summary>读取当前展示值。</summary>
        AtelierPaintingSnapshot Read();
        /// <summary>屏幕点到实际表面，失败不允许落笔。</summary>
        bool TryMap(Vector2 screen, out Vector2 uv);
        /// <summary>临时预览不改变作品。</summary>
        void Preview(Vector2 uv, bool visible, int cost, int angle);
        /// <summary>返回中文拒绝原因，空字符串表示接受。</summary>
        string RequestCommitStroke(AtelierStrokeIntent intent, Vector2 uv);
        /// <summary>请求精确撤销。</summary>
        string RequestUndo(AtelierPaintingSnapshot snapshot);
        /// <summary>应用层复查收层条件。</summary>
        string RequestFinishLayer(AtelierPaintingSnapshot snapshot);
        /// <summary>离开近景不清作品。</summary>
        void RequestReturn();
        /// <summary>尚未实现的工具必须明确拒绝。</summary>
        string RequestToolMode(AtelierToolMode mode);
    }
}
