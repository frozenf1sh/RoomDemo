// =============================================================================
// 文件：InspectionContent.cs
// 职责：集中定义样板、盆栽与书本的有限检视路径。
// 边界：动作数据与世界规则分开；观察产出仍由领域中的发现表验证。
// =============================================================================
namespace Game.Application
{
    /// <summary>两件主物体和一个技术立方体的具体状态表，不建立编辑器或继承树。</summary>
    public static class InspectionContent
    {
        /// <summary>根据稳定物体身份选择检视数据；不匹配配方、不产生事实。</summary>
        public static InspectionDefinition For(string id)
        {
            if (id == "book") return new InspectionDefinition("closed", new[] { "closed", "page1", "page2", "page3" }, new[] {
                new InspectionActionDefinition("cover", "打开封面", InspectionAction.Open, new[] { "closed" }, "page1"),
                new InspectionActionDefinition("next1", "翻到下一页", InspectionAction.Next, new[] { "page1" }, "page2"),
                new InspectionActionDefinition("next2", "再翻一页", InspectionAction.Next, new[] { "page2" }, "page3"),
                new InspectionActionDefinition("previous2", "翻回上一页", InspectionAction.Previous, new[] { "page2" }, "page1"),
                new InspectionActionDefinition("previous3", "翻回上一页", InspectionAction.Previous, new[] { "page3" }, "page2"),
                new InspectionActionDefinition("quote", "细读这句话", InspectionAction.Observe, new[] { "page2" }, observation: "toward_light"),
                new InspectionActionDefinition("horizon", "观察页边的地平线", InspectionAction.Observe, new[] { "page3" }, observation: "book_far"),
                new InspectionActionDefinition("close", "合上书", InspectionAction.Close, new[] { "page1", "page2", "page3" }, "closed") });
            if (id == "plant") return new InspectionDefinition("rest", new[] { "rest" }, new[] {
                new InspectionActionDefinition("sprout", "土边有一颗新芽", InspectionAction.Observe, new[] { "rest" }, observation: "sprout") });
            // 技术样板先验证点击与状态切换，后续主物体复用完全相同的输入控制。
            return new InspectionDefinition("closed", new[] { "closed", "open" }, new[] {
                new InspectionActionDefinition("open", "打开检视样板", InspectionAction.Open, new[] { "closed" }, "open"),
                new InspectionActionDefinition("close", "合上检视样板", InspectionAction.Close, new[] { "open" }, "closed") });
        }
    }
}
