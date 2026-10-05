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
        /// <summary>对应领域配方的稳定身份；不得使用可翻译的显示名做关联。</summary>
        public string RecipeId;
        /// <summary>解释成功后短暂显示的一行中文；空值表示无需字幕。</summary>
        public string Subtitle;
        /// <summary>声音内容表中的 Cue ID；片段资源和音量由场景安装器绑定。</summary>
        public string AudioCue;
        /// <summary>可选微反馈标记；当前非空标记使用统一的少量光点表现。</summary>
        public string VfxCue;
        /// <summary>已有表现导演的入口 ID；空值不启动独立世界演出。</summary>
        public string SequenceId;

        /// <summary>建立一条配方的表现记录，不改变配方输入、输出或领域事实。</summary>
        /// <param name="recipe">稳定配方 ID。</param>
        /// <param name="subtitle">可选中文字幕。</param>
        /// <param name="audio">可选声音 Cue ID。</param>
        /// <param name="vfx">可选通用微反馈标记。</param>
        /// <param name="sequence">可选已注册的演出入口。</param>
        public RecipePresentation(string recipe, string subtitle, string audio, string vfx, string sequence = "")
        {
            RecipeId = recipe;
            Subtitle = subtitle;
            AudioCue = audio;
            VfxCue = vfx;
            SequenceId = sequence;
        }
    }
    /// <summary>主线表现表；所有解释字幕都在这里，不散落在 Presenter 的条件分支中。</summary>
    public static class MainlinePresentation
    {
        /// <summary>生成独立内容数组，避免编辑工具修改共享实例。</summary>
        public static RecipePresentation[] Create()
        {
            // 联想使用概念书写反馈，解释使用短字幕；具体世界演出只通过稳定入口绑定。
            // 每次生成新数组与条目，场景序列化不会改动另一局的内容记录。
            return new[] {
            new RecipePresentation("recipe.insight.growth", "", "concept_form", "inspiration"),
            new RecipePresentation("recipe.painting.growth", "画里有枝叶。", "interpretation_soft", "ink"),
            new RecipePresentation("recipe.painting.far", "画中是远方。", "interpretation_soft", "ink"),
            new RecipePresentation("recipe.painting.starry", "画中的夜空完成了。", "interpretation_soft", "ink"),
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
}
