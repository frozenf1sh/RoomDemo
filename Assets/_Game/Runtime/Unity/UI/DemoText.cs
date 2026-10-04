// =============================================================================
// 文件：DemoText.cs
// 职责：提供调试面板所需的中文事实与反应名称。
// 边界：仅翻译显示文字；字典键保留稳定内容 ID，不参与配方匹配或反应计算。
// =============================================================================
using System.Collections.Generic;
using Game.Domain;

namespace Game.Unity
{
    /// <summary>试玩版的中文显示词表。新增内容时在这里补充调试名称。</summary>
    public static class DemoText
    {
        // 世界事实已经由领域层产生；这里仅将标识符映射为人能读懂的名称。
        private static readonly Dictionary<string, string> Facts = new Dictionary<string, string>
        {
            ["plant.grown"] = "盆栽已生长",
            ["plant.starts_growing"] = "盆栽开始舒展",
            ["plant.overgrown"] = "盆栽次日繁茂",
            ["world.starry"] = "房间成为星空世界",
            ["book.fallen"] = "书已落下",
            ["book.inspectable"] = "书可以调查",
            ["window.far"] = "窗户通向远方",
            ["wind.entering"] = "风进入房间",
            ["paper.revealed"] = "纸张已显露",
            ["painting.sea"] = "画中出现海",
            ["water.present"] = "海水进入房间",
            ["plant.aquatic"] = "盆栽变为水生植物",
            ["window.ocean"] = "窗外变为海洋",
            ["vertical_slice.complete"] = "试玩切片完成"
        };

        // 反应名称与事实名称分开：前者表示发生的联系，后者表示已经成立的状态。
        private static readonly Dictionary<string, string> Reactions = new Dictionary<string, string>
        {
            ["growth-continues-next-day"] = "解释在次日继续生长",
            ["overgrowth-is-grown"] = "繁茂承接既有生长联系",
            ["plant-knocks-book"] = "盆栽碰落书本",
            ["book-becomes-inspectable"] = "书本开放调查",
            ["wind-enters-room"] = "旧解释引风入室",
            ["wind-reveals-paper"] = "风吹出纸张",
            ["painting-leaks-water"] = "画框渗出海水",
            ["plant-becomes-aquatic"] = "海水改变盆栽",
            ["window-becomes-ocean"] = "海水改变远方",
            ["room-remembers"] = "房间记住解释"
        };

        /// <summary>返回事实中文名；未登记的新事实保留其 ID，方便发现词表遗漏。</summary>
        /// <param name="id">领域事实标识符。</param>
        /// <returns>中文名称，或用于排查问题的原始标识符。</returns>
        public static string Fact(FactId id) => Facts.TryGetValue(id.Value, out var label) ? label : id.Value;

        /// <summary>返回自动反应中文名；查询不会触发或修改反应。</summary>
        /// <param name="id">领域反应标识符。</param>
        /// <returns>中文名称，未登记时返回原始标识符。</returns>
        public static string Reaction(string id) => Reactions.TryGetValue(id, out var label) ? label : id;
    }
}
