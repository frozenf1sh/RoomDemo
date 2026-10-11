// =============================================================================
// 文件：AtelierPaintingPresenter.cs
// 职责：空白亚麻上逐件落下静物，按玩家笔迹预览，并以短笔触收齐该物件的颜色。
// 边界：不提交命令；手势是临时输入，正式完成只消费 GameState 的中性落笔事实。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>四个静物色层共用实体画布；画中水只在后续解释之后持续流动。</summary>
    public sealed class AtelierPaintingPresenter : MonoBehaviour
    {
        public Renderer Surface;
        public Vector2 WaterUV, LampUV, WindowUV;
        public Vector2 WaterRadius = new Vector2(.09f, .032f);
        private GameSession session;
        private MaterialPropertyBlock properties;
        private readonly float[] layers = new float[4];
        private readonly Vector4[] dabs = new Vector4[32];
        private static readonly FactId[] SketchFacts = { AtelierRules.CupSketched, AtelierRules.BrushSketched, AtelierRules.LampSketched, AtelierRules.WindowSketched };
        private int motif = -1, dabCount, taps;
        private float waterProgress, lampProgress, openProgress, clock, travel;
        private Vector2 last;
        private bool drawing;
        /// <summary>当前临时选中的静物，不是领域事实。</summary>
        public int SelectedMotif => motif;
        /// <summary>落笔是否已经完成，直接查询唯一世界事实。</summary>
        public bool IsSketched(int index) => index >= 0 && session.State.HasFact(SketchFacts[index]);

        /// <summary>绑定并恢复已经落下的色层；重开没有残留笔迹。</summary>
        public void Bind(GameSession game)
        {
            session = game; properties = new MaterialPropertyBlock();
            for (int i = 0; i < 4; i++) layers[i] = IsSketched(i) ? 1 : 0;
            waterProgress = game.State.HasFact(StudioRules.PictureFlow) ? 1 : 0;
            lampProgress = game.State.HasFact(StudioRules.PictureLit) ? 1 : 0;
            openProgress = game.State.HasFact(StudioRules.PictureOpen) ? 1 : 0;
            clock = 0; SelectMotif(-1); Apply();
        }
        /// <summary>选择眼前的真实物件；更换物件会丢弃未确认的试笔。</summary>
        public void SelectMotif(int index)
        {
            motif = index; dabCount = taps = 0; travel = 0; drawing = false;
        }
        /// <summary>从画布真实射线落下一笔；没有命中画布不能调用此入口。</summary>
        public void BeginStroke(Vector2 uv)
        {
            if (motif < 0 || IsSketched(motif)) return;
            drawing = true; last = uv; taps++; AddDab(uv);
        }
        /// <summary>连续笔迹以有限采样传给材质，既支持拖笔，也支持逐次点笔。</summary>
        public void ContinueStroke(Vector2 uv)
        {
            if (!drawing || Vector2.Distance(last, uv) < .009f) return;
            travel += Vector2.Distance(last, uv); last = uv; AddDab(uv);
        }
        /// <summary>结束真实手势；三个点笔或一条短笔迹才构成明确作画意图。</summary>
        /// <returns>输入充分时返回 true，调用者仍须把正常配方交给领域验证。</returns>
        public bool EndStroke()
        {
            drawing = false;
            return motif >= 0 && !IsSketched(motif) && (taps >= 3 || travel >= .055f);
        }
        private void AddDab(Vector2 uv)
        {
            dabs[dabCount % dabs.Length] = new Vector4(uv.x, uv.y, .042f, 1);
            dabCount++;
        }
        private void Update()
        {
            if (session == null) return;
            clock += Time.deltaTime;
            for (int i = 0; i < 4; i++)
                layers[i] = Mathf.MoveTowards(layers[i], IsSketched(i) ? 1 : 0, Time.deltaTime / (i == 3 ? 4.2f : 2.8f));
            waterProgress = Mathf.MoveTowards(waterProgress, session.State.HasFact(StudioRules.PictureFlow) ? 1 : 0, Time.deltaTime / 2.4f);
            lampProgress = Mathf.MoveTowards(lampProgress, session.State.HasFact(StudioRules.PictureLit) ? 1 : 0, Time.deltaTime / 3.2f);
            openProgress = Mathf.MoveTowards(openProgress, session.State.HasFact(StudioRules.PictureOpen) ? 1 : 0, Time.deltaTime / 3.8f);
            Apply();
        }
        private void Apply()
        {
            Surface.GetPropertyBlock(properties);
            properties.SetVector("_WaterRegion", new Vector4(WaterUV.x, WaterUV.y, WaterRadius.x, WaterRadius.y));
            properties.SetVector("_Sketch", new Vector4(layers[0], layers[1], layers[2], layers[3]));
            properties.SetFloat("_SelectedMotif", motif);
            properties.SetFloat("_DabCount", Mathf.Min(dabCount, dabs.Length));
            properties.SetVectorArray("_Dabs", dabs);
            properties.SetFloat("_WaterPaint", waterProgress);
            properties.SetVector("_LampRegion", new Vector4(LampUV.x, LampUV.y, .055f, .09f));
            properties.SetFloat("_LampPaint", lampProgress);
            properties.SetFloat("_OpenPaint", openProgress);
            properties.SetFloat("_PaintTime", clock);
            Surface.SetPropertyBlock(properties);
        }
    }
}
