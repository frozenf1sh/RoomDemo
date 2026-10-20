// 文件：AtelierFinaleOperations.cs
// 职责：验证最终刀面、两次屋面揭底与房间擦开的明确玩家命令。
// 边界：只保存有限表面路径与事实；不接触Unity、镜头、Shader或真实时钟。
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>万分之一局部表面坐标的一段有效接触，坐标与笔触使用相同精度。</summary>
    public readonly struct AtelierContactSegment
    {
        public int U0 { get; }
        public int V0 { get; }
        public int U1 { get; }
        public int V1 { get; }
        public AtelierContactSegment(int u0,int v0,int u1,int v1) { U0=u0;V0=v0;U1=u1;V1=v1; }
        internal bool InBounds => U0>=0&&U0<=10000&&V0>=0&&V0<=10000&&U1>=0&&U1<=10000&&V1>=0&&V1<=10000;
        internal double Length => Math.Sqrt((U1-U0)*(double)(U1-U0)+(V1-V0)*(double)(V1-V0));
    }
    /// <summary>四个有限操作；明确区分首次试刮与继续扩大，不做通用工具系统。</summary>
    public enum AtelierFinaleAction { SpreadCanvas, ProbeRoof, ContinueRoof, EraseRoom }
    /// <summary>供UI和世界输入共用的领域许可及原子命令。</summary>
    public static class AtelierFinaleOperations
    {
        /// <summary>只读返回当前动作被阻止的原因；检查当前笔数，不能沿用曾经开放过的按钮。</summary>
        public static string BlockedReason(GameState state,RuleSet rules,int revision,AtelierFinaleAction action)
        {
            if(!Enum.IsDefined(typeof(AtelierFinaleAction),action))return "这里没有这个工具操作。";
            if(!rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled)) return "当前会话不使用四阶段流程。";
            if(state.CanvasLocked||state.HasFact(AtelierRules.RoomErased)) return "已经确定了收束方式。";
            if(state.AtelierCanvas.Revision!=revision) return "画面已经更新，请重新操作。";
            if(!state.HasFact(AtelierFlowRules.Finished(3))||state.AtelierCanvas.Current.Id!=4) return "先完成前三层绘画。";
            if(state.AtelierCanvas.Current.Count<3) return "先完成本阶段至少三笔绘画。";
            if(!state.Knows(AtelierFlowRules.PaletteKnife)) return "先拿起调色刮刀。";
            if(action==AtelierFinaleAction.SpreadCanvas)
            {
                if(state.HasFact(AtelierFlowRules.WorldCommitted)) return "已经继续刮开了屋面。";
                if(!state.Knows(AtelierRules.Painting)||!state.HasFact(AtelierFlowRules.WetLearned)) return "先试过刀面，再回到画布。";
            }
            else
            {
                if(!state.Knows(StudioRules.Ceiling)||!state.HasFact(AtelierFlowRules.DryLearned)) return "先调查屋面，再选择刀刃。";
                if(action==AtelierFinaleAction.ProbeRoof&&state.HasFact(AtelierFlowRules.RoofProbed)) return "这一小片已经刮开了。";
                if(action==AtelierFinaleAction.ContinueRoof&&(!state.HasFact(AtelierFlowRules.RoofProbed)||!state.Knows(AtelierFlowRules.C("roof_night")))) return "先在细痕上试刮一小段。";
                if(action==AtelierFinaleAction.ContinueRoof&&state.HasFact(AtelierFlowRules.WorldCommitted)) return "屋面已经刮开了。";
                if(action==AtelierFinaleAction.EraseRoom&&(!state.HasFact(AtelierFlowRules.WorldCommitted)
                    ||!state.Knows(StudioRules.Sky)||!state.HasFact(AtelierRules.SharedMarkSeen)||!state.Knows(StudioRules.Room)))
                    return "先亲眼看过天空、接连的笔迹和房间。";
            }
            return null;
        }
        /// <summary>接触由适配器验证命中；领域复核有限路径、顺序、当前作品和终局互斥。</summary>
        /// <param name="segments">最多64段局部坐标的副本，拒绝越界、空路径和不足阈值。</param>
        /// <param name="assistedTaps">同一有效表面的已释放辅助短点，三次代替拖动。</param>
        public static AtelierCanvasResult Apply(GameState state,RuleSet rules,int revision,AtelierFinaleAction action,
            IReadOnlyList<AtelierContactSegment> segments,int assistedTaps=0)
        {
            string reason=BlockedReason(state,rules,revision,action);
            if(reason!=null) return new AtelierCanvasResult(false,reason);
            var path=segments?.ToArray();
            if(path==null||path.Length==0||path.Length>64||path.Any(x=>!x.InBounds)||assistedTaps<0||assistedTaps>3)
                return new AtelierCanvasResult(false,"请在目标表面留下一段有效接触。");
            if(assistedTaps<3&&path.Sum(x=>x.Length)<1000) return new AtelierCanvasResult(false,"沿表面再推一小段。");
            if(action==AtelierFinaleAction.SpreadCanvas)
            {
                // 收色必须经过当前作品实际有笔印的附近，空白角落不能充当有效收色。
                bool nearPaint=path.Any(p=>state.AtelierCanvas.Layers.SelectMany(l=>l.Strokes)
                    .Any(s=>Math.Abs(s.U-p.U1)<2800&&Math.Abs(s.V-p.V1)<2800));
                if(!nearPaint) return new AtelierCanvasResult(false,"请沿已有颜料推开一小段。");
                state.AtelierCanvas.SetSpread(path);
                return new AtelierCanvasResult(true,"",new DomainEvent[] {new AtelierCanvasChanged("spread",revision)});
            }
            var events=new List<DomainEvent>();
            void Fact(FactId id) { if(state.AddFact(id)) events.Add(new FactAdded(id)); }
            if(action==AtelierFinaleAction.ProbeRoof)
            {
                state.AtelierCanvas.SetRoofProbe(path);
                Fact(AtelierFlowRules.RoofProbed);
                var clue=AtelierFlowRules.C("roof_night");
                if(state.Discover(clue)) events.Add(new ClueDiscovered(clue));
            }
            if(action==AtelierFinaleAction.ContinueRoof)
            {
                var first=path[0];
                double distance=state.AtelierCanvas.RoofProbe.Min(old=>
                {
                    double dx=old.U1-old.U0,dy=old.V1-old.V0;
                    double t=Math.Max(0,Math.Min(1,((first.U0-old.U0)*dx+(first.V0-old.V0)*dy)/Math.Max(1,dx*dx+dy*dy)));
                    double x=first.U0-old.U0-dx*t,y=first.V0-old.V0-dy*t;
                    return Math.Sqrt(x*x+y*y);
                });
                // 新接触必须落在旧刀痕之外、仍靠近开口的边缘；重放第一次路径不能偷偷锁线。
                if(distance<600||distance>2200) return new AtelierCanvasResult(false,"沿新露出的边缘继续刮开。");
                state.ConsumeClue(AtelierFlowRules.C("roof_night"));
                Fact(AtelierFlowRules.WorldCommitted);
                Fact(AtelierRules.RoofErased);
            }
            if(action==AtelierFinaleAction.EraseRoom) Fact(AtelierRules.RoomErased);
            return new AtelierCanvasResult(true,"",events.ToArray());
        }
    }
}
