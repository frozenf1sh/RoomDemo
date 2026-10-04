// =============================================================================
// 文件：InspirationVisual.cs
// 职责：以轻微墨光、稀疏纸纤维光点、勾线与墨迹扩散表现一次细节发现。
// 边界：纯表现组件；不授予线索，不消费碎片，观察成功由控制器提交会话后通知。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>书中关键短语的局部视觉反馈；发现不是物品掉落，光点数量与亮度均受限。</summary>
    public sealed class InspirationVisual : MonoBehaviour
    {
        [SerializeField] private TextMesh words;
        [SerializeField] private Renderer inkBloom;
        [SerializeField] private Transform underline;
        [SerializeField] private ParticleSystem fibers;
        private MaterialPropertyBlock block;
        private Color originalInk;
        private Vector3 underlineScale;
        private float hover, targetHover, reveal = -1;
        private static readonly int Glow = Shader.PropertyToID("_Glow");
        private static readonly int Reveal = Shader.PropertyToID("_InkReveal");

        /// <summary>把特定短语的文字、墨迹与纸纤维效果显式绑到同一个热点。</summary>
        public void Configure(TextMesh text, Renderer bloom, Transform line, ParticleSystem motes)
        {
            words = text;
            inkBloom = bloom;
            underline = line;
            fibers = motes;
        }

        private void Awake()
        {
            originalInk = words.color;
            underlineScale = underline.localScale;
            block = new MaterialPropertyBlock();
            underline.gameObject.SetActive(false);
        }

        /// <summary>鼠标经过才出现墨光；离开后柔和退去，不常驻黄色描边。</summary>
        public void SetHover(bool value)
        {
            targetHover = value ? 1 : 0;
            if (value && !fibers.isPlaying) fibers.Play();
            if (!value) fibers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>领域确认细节观察成功后，勾过短语并扩散少量墨迹。</summary>
        public void MarkObserved()
        {
            // 复制体只记录一次局部动画；退出检视会销毁该表现状态，世界事实仍由 GameState 保存。
            reveal = 0;
            underline.gameObject.SetActive(true);
            fibers.Emit(8);
        }

        private void Update()
        {
            hover = Mathf.MoveTowards(hover, targetHover, Time.unscaledDeltaTime * 3.5f);
            float pulse = .84f + .16f * Mathf.Sin(Time.unscaledTime * 3.2f);
            words.color = Color.Lerp(originalInk, new Color(.72f, .49f, .16f), hover * .70f * pulse);
            if (reveal >= 0)
            {
                reveal = Mathf.Min(1, reveal + Time.unscaledDeltaTime / 1.15f);
                underline.localScale = new Vector3(underlineScale.x * Mathf.SmoothStep(0, 1, reveal), underlineScale.y, underlineScale.z);
            }
            // 稀疏扩散采用局部 PropertyBlock；不会每帧创建材质或影响其他纸页。
            inkBloom.GetPropertyBlock(block);
            block.SetFloat(Glow, hover * pulse);
            block.SetFloat(Reveal, reveal < 0 ? 0 : reveal);
            inkBloom.SetPropertyBlock(block);
        }
    }
}
