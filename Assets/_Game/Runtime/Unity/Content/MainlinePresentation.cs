// =============================================================================
// 文件：MainlinePresentation.cs
// 职责：把主线配方的字幕、声音、微粒和导演入口集中在一个内容表中。
// 边界：这是表现内容，不参与领域配方判定；新增解释只需追加数据。
// =============================================================================
using System;
namespace Game.Unity
{
    /// <summary>一条解释的可选表现信息，稳定配方 ID 与中文文本分离。</summary>
    [Serializable]
    public sealed class RecipePresentation
    {
        public string RecipeId, Subtitle, AudioCue, VfxCue, SequenceId;
        public RecipePresentation(string recipe, string subtitle, string audio, string vfx, string sequence = "")
        { RecipeId=recipe; Subtitle=subtitle; AudioCue=audio; VfxCue=vfx; SequenceId=sequence; }
    }
    /// <summary>主线表现表；所有解释字幕都在这里，不散落在 Presenter 的条件分支中。</summary>
    public static class MainlinePresentation
    {
        /// <summary>生成独立内容数组，避免编辑工具修改共享实例。</summary>
        public static RecipePresentation[] Create() => new[] {
            new RecipePresentation("recipe.insight.growth", "", "concept_form", "inspiration"),
            new RecipePresentation("recipe.plant.growth", "枝叶开始向光伸去。", "interpretation_soft", "ink"),
            new RecipePresentation("recipe.window.far", "窗外，是远方。", "interpretation_soft", "ink"),
            new RecipePresentation("recipe.painting.sea", "画里有海。", "interpretation_soft", "ink"),
            new RecipePresentation("recipe.insight.starry", "", "concept_form", "inspiration"),
            new RecipePresentation("recipe.ceiling.starry", "屋顶是星空。", "starry_open", "ink", "ceiling_starry"),
            new RecipePresentation("recipe.insight.forest", "", "concept_form", "inspiration"),
            new RecipePresentation("recipe.room.forest", "墙后有森林。", "forest_reveal", "ink", "forest_world")
        };
    }
}
