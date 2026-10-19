// 文件：AtelierOpeningCopy.cs
// 职责：集中保存本轮经用户要求重写的第一人称便签文字。
// 边界：只有阅读文案，不定义配方、发放观察或决定开场输入许可。
namespace Game.Unity.ArtRebuild
{
    /// <summary>实体便签与阅读面板共用同一文本，避免保留旧静物作画目标。</summary>
    public static class AtelierOpeningCopy
    {
        /// <summary>主角因交稿而受挫，并提醒自己寻找房间和儿时涂鸦中的想法。</summary>
        public const string NoteBody = "快交稿了，我还是一点灵感都没有。\n\n先在房间里找找想法吧。\n\n窗台上还放着我小时候的那幅涂鸦。";
    }
}
