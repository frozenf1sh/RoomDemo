// 文件：AtelierSkyOperations.cs
// 职责：确认天空解释后的两次主动点击；不创建第五层画作或颜料账本。
// 边界：云和星各一次，Shader形状和渐变由Unity读取事实后表现。
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>星先、云后；先点亮夜空再铺云，一次请求不会同时完成两个步骤。</summary>
    public static class AtelierSkyOperations
    {
        /// <summary>确认一次天空点击，拒绝未解释、重复或步骤不匹配的请求。</summary>
        /// <param name="state">当前世界；有效请求原子写入当前步骤事实。</param>
        /// <param name="rules">只接受已声明四阶段的内容规则。</param>
        /// <param name="stars">true为第一步星光，false为星光之后的云；不能跳步。</param>
        /// <returns>接受结果及本次事实事件；拒绝时世界不变。</returns>
        public static AtelierCanvasResult Click(GameState state,RuleSet rules,bool stars)
        {
            if(!rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled)||!state.HasFact(AtelierRules.RoomErased)
                ||!state.HasFact(AtelierFlowRules.SkyAsCanvas)||!state.Knows(StudioRules.Sky))
                return new AtelierCanvasResult(false,"先把天空和画布联系起来。");
            if(state.HasFact(AtelierFlowRules.SkyFinished)) return new AtelierCanvasResult(false,"这一笔已经画过了。");
            if(stars)
            {
                if(state.HasFact(AtelierFlowRules.SkyStarsPainted)) return new AtelierCanvasResult(false,"这一笔已经画过了。");
            }
            else
            {
                if(!state.HasFact(AtelierFlowRules.SkyStarsPainted)) return new AtelierCanvasResult(false,"先点一下天空，落下星光。");
                if(state.HasFact(AtelierFlowRules.SkyCloudPainted)) return new AtelierCanvasResult(false,"这一笔已经画过了。");
            }
            var fact=stars?AtelierFlowRules.SkyStarsPainted:AtelierFlowRules.SkyCloudPainted;
            var events=new List<DomainEvent>();
            // 有效点击只写一次当前步骤；最后一步的完成事实与同一命令一起发布。
            if(state.AddFact(fact)) events.Add(new FactAdded(fact));
            if(!stars&&state.AddFact(AtelierFlowRules.SkyFinished))events.Add(new FactAdded(AtelierFlowRules.SkyFinished));
            return new AtelierCanvasResult(true,"",events.ToArray());
        }
    }
}
