// 文件：AtelierCanvasDocument.cs
// 职责：保存四层有限笔印、精确颜料账本与作品版本。
// 边界：由GameState拥有；不含Unity类型、纹理、鼠标状态或磁盘存档。
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>已确认的一枚原始图样，坐标以万分之一画布和整度记录。</summary>
    public sealed class AtelierStroke
    {
        /// <summary>一次落笔的稳定身份；重发不会重复扣量。</summary>
        public string Id { get; }
        /// <summary>所属层，范围为一至四。</summary>
        public int Layer { get; }
        /// <summary>画布横坐标，万分之一为单位。</summary>
        public int U { get; }
        /// <summary>画布纵坐标，万分之一为单位。</summary>
        public int V { get; }
        /// <summary>逆时针整度旋转，范围为零至三百五十九。</summary>
        public int Angle { get; }
        /// <summary>此笔消耗的整数颜料量；显示大小由适配层标定。</summary>
        public int PaintUnits { get; }
        /// <summary>建立不可变请求；实际合法性由提交命令统一验证。</summary>
        public AtelierStroke(string id, int layer, int u, int v, int angle, int units)
        {
            Id = id;
            Layer = layer;
            U = u;
            V = v;
            Angle = angle;
            PaintUnits = units;
        }
        /// <summary>重发只有内容完全一致才视作原请求，不能借相同ID修改作品。</summary>
        internal bool Matches(AtelierStroke other) => Id == other.Id && Layer == other.Layer && U == other.U && V == other.V && Angle == other.Angle && PaintUnits == other.PaintUnits;
    }
    /// <summary>对外只读的一层记录，私有列表不暴露写引用。</summary>
    public sealed class AtelierCanvasLayer
    {
        private readonly List<AtelierStroke> strokes = new List<AtelierStroke>();
        /// <summary>该层的稳定序号。</summary>
        public int Id { get; }
        /// <summary>未提前收笔时的计划笔数。</summary>
        public int PlannedStrokes { get; }
        /// <summary>主动完成后不可再撤销或修改。</summary>
        public bool Sealed { get; internal set; }
        /// <summary>提前完成时明确放弃的余量。</summary>
        public int DiscardedUnits { get; internal set; }
        /// <summary>已接受的笔印数量。</summary>
        public int Count => strokes.Count;
        /// <summary>已落到作品中的颜料量。</summary>
        public int CommittedUnits => strokes.Sum(x => x.PaintUnits);
        /// <summary>余额与已用、放弃量共同守恒为一百。</summary>
        public int RemainingPaint => 100 - CommittedUnits - DiscardedUnits;
        /// <summary>当前层笔触只读视图，不向UI暴露可写集合。</summary>
        public IReadOnlyList<AtelierStroke> Strokes => strokes.AsReadOnly();
        /// <summary>合法区间保证剩余计划笔数仍可在12至24单位内精确用尽。</summary>
        public int MinCost => RemainingPaint == 0 || Sealed ? 0 : Math.Max(12, RemainingPaint - (PlannedStrokes - Count - 1) * 24);
        /// <summary>当前允许的最大消耗，保留后续每笔至少十二单位。</summary>
        public int MaxCost => RemainingPaint == 0 || Sealed ? 0 : Math.Min(24, RemainingPaint - (PlannedStrokes - Count - 1) * 12);
        internal AtelierCanvasLayer(int id, int planned) { Id = id; PlannedStrokes = planned; }
        internal void Add(AtelierStroke stroke) => strokes.Add(stroke);
        internal void Undo() => strokes.RemoveAt(strokes.Count - 1);
    }
    /// <summary>唯一作品账本；每次修改使旧预览失效，离开近景不会丢失。</summary>
    public sealed class AtelierCanvasDocument
    {
        private readonly AtelierCanvasLayer[] layers = {
            new AtelierCanvasLayer(1,6), new AtelierCanvasLayer(2,5), new AtelierCanvasLayer(3,5), new AtelierCanvasLayer(4,6) };
        /// <summary>全部封存与未封存层，按原始图样的叠放顺序排列。</summary>
        public IReadOnlyList<AtelierCanvasLayer> Layers => Array.AsReadOnly(layers);
        /// <summary>第一个未封存层；全部完成时仍提供最后一层供只读显示。</summary>
        public AtelierCanvasLayer Current => layers.FirstOrDefault(x => !x.Sealed) ?? layers[3];
        /// <summary>每次作品修改递增，过期预览必须重新建立。</summary>
        public int Revision { get; internal set; }
        /// <summary>最后一次刀面收色对应版本，后续批次的明确动作才可设置。</summary>
        public int SpreadRevision { get; internal set; } = -1;
        internal AtelierStroke Find(string id) => layers.SelectMany(x => x.Strokes).FirstOrDefault(x => x.Id == id);
        internal void Changed()
        {
            Revision++;
            SpreadRevision = -1;
        }
    }
    /// <summary>同一次用例的结果和事件，拒绝时账本与世界均不改变。</summary>
    public sealed class AtelierCanvasResult
    {
        /// <summary>本请求是否被接受；拒绝不会改变账本。</summary>
        public bool Accepted { get; }
        /// <summary>相同身份与内容的请求已经成功应用，此次不重复发布。</summary>
        public bool AlreadyApplied { get; }
        /// <summary>拒绝时可展示的中文说明。</summary>
        public string Reason { get; }
        /// <summary>同一命令产生的只读事件副本。</summary>
        public IReadOnlyList<DomainEvent> Events { get; }
        /// <summary>复制事件数组，调用者保留的原数组不能事后改变发布内容。</summary>
        public AtelierCanvasResult(bool accepted, string reason, DomainEvent[] events = null, bool repeated = false)
        {
            Accepted = accepted;
            Reason = reason;
            AlreadyApplied = repeated;
            Events = Array.AsReadOnly(events == null ? Array.Empty<DomainEvent>() : (DomainEvent[])events.Clone());
        }
    }
    /// <summary>账本已经原子更新，消费者随后读取当前作品，不从事件重建另一份状态。</summary>
    public sealed class AtelierCanvasChanged : DomainEvent
    {
        /// <summary>提交、撤销或收层的稳定操作标识。</summary>
        public string Operation { get; }
        /// <summary>该次命令结束后的作品版本。</summary>
        public int Revision { get; }
        /// <summary>描述已完成修改，不携带另一份可写作品。</summary>
        public AtelierCanvasChanged(string operation, int revision)
        {
            Operation = operation;
            Revision = revision;
        }
    }
    /// <summary>本游戏专用的有限命令，不扩展为通用绘画框架。</summary>
    public static class AtelierCanvasOperations
    {
        private static string Guard(GameState state, RuleSet rules, int revision)
        {
            if (!rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled)) return "当前会话不使用四阶段画布。";
            if (state.CanvasLocked || state.HasFact(AtelierFlowRules.WorldCommitted)) return "已经确定了收束方式。";
            if (state.AtelierCanvas.Revision != revision) return "画面已经更新，请重新落笔。";
            return null;
        }
        /// <summary>校验后一次写入笔触与扣量，不接受超界中心、未知笔刷或过期预览。</summary>
        public static AtelierCanvasResult Commit(GameState state, RuleSet rules, int revision, AtelierStroke stroke)
        {
            if (stroke == null || string.IsNullOrWhiteSpace(stroke.Id)) return new AtelierCanvasResult(false, "缺少笔触身份。");
            var old = state.AtelierCanvas.Find(stroke.Id);
            if (old != null) return new AtelierCanvasResult(old.Matches(stroke), old.Matches(stroke) ? "" : "笔触身份已经使用。", repeated: old.Matches(stroke));
            string reason = Guard(state, rules, revision);
            if (reason != null) return new AtelierCanvasResult(false, reason);
            var layer = state.AtelierCanvas.Current;
            if (stroke.Layer != layer.Id || !state.Knows(AtelierFlowRules.Brush(layer.Id))) return new AtelierCanvasResult(false, "先在笔记中形成这一层笔刷。");
            if (stroke.U < 0 || stroke.U > 10000 || stroke.V < 0 || stroke.V > 10000 || stroke.Angle < 0 || stroke.Angle >= 360) return new AtelierCanvasResult(false, "请把笔印放在画布内。");
            if (layer.RemainingPaint == 0 || layer.Sealed || stroke.PaintUnits < layer.MinCost || stroke.PaintUnits > layer.MaxCost) return new AtelierCanvasResult(false, "颜料或笔印大小已经变化。");
            layer.Add(stroke);
            state.AtelierCanvas.Changed();
            return new AtelierCanvasResult(true, "", new DomainEvent[] { new AtelierCanvasChanged("commit", state.AtelierCanvas.Revision) });
        }
        /// <summary>精确退还当前层最后一笔；封存层和先前层不能撤销。</summary>
        public static AtelierCanvasResult Undo(GameState state, RuleSet rules, int revision)
        {
            string reason = Guard(state, rules, revision);
            if (reason != null) return new AtelierCanvasResult(false, reason);
            var layer = state.AtelierCanvas.Current;
            if (layer.Sealed || layer.Count == 0) return new AtelierCanvasResult(false, "这一层没有可撤销的笔印。");
            layer.Undo();
            state.AtelierCanvas.Changed();
            return new AtelierCanvasResult(true, "", new DomainEvent[] { new AtelierCanvasChanged("undo", state.AtelierCanvas.Revision) });
        }
        /// <summary>只读返回收层被阻止的原因；包括笔数、工具经验与当前作品版本。</summary>
        public static string FinishBlockedReason(GameState state, RuleSet rules, int revision)
        {
            string reason = Guard(state, rules, revision);
            if (reason != null) return reason;
            var layer = state.AtelierCanvas.Current;
            if (layer.Sealed || layer.Count < 3) return "再落" + Math.Max(0,3-layer.Count) + "笔后可完成。";
            if (layer.Id == 3 && (!state.HasFact(AtelierFlowRules.DryLearned) || !state.HasFact(AtelierFlowRules.WetLearned))) return "先在颜料板试试刀刃和刀面。";
            if (layer.Id == 4 && state.AtelierCanvas.SpreadRevision != revision) return "先用刀面收一次颜色。";
            return null;
        }
        /// <summary>与只读许可共用同一校验，再原子封存当前层；失败不丢弃余量。</summary>
        public static AtelierCanvasResult Finish(GameState state, RuleSet rules, int revision)
        {
            string reason = FinishBlockedReason(state,rules,revision);
            if (reason != null) return new AtelierCanvasResult(false, reason);
            var layer = state.AtelierCanvas.Current;
            layer.DiscardedUnits = layer.RemainingPaint;
            layer.Sealed = true;
            state.AtelierCanvas.Changed();
            var fact = layer.Id == 4 ? AtelierRules.CanvasDelivered : AtelierFlowRules.Finished(layer.Id);
            state.AddFact(fact);
            return new AtelierCanvasResult(true, "", new DomainEvent[] { new FactAdded(fact), new AtelierCanvasChanged("finish",state.AtelierCanvas.Revision) });
        }
    }
}
