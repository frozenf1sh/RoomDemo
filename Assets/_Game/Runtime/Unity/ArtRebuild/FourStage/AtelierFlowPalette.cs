// 文件：AtelierFlowPalette.cs
// 职责：把真实板面接触转换成有限刀痕，达到覆盖阈值后提交刮开或推色动作。
// 边界：路径和刀具预览属于表现；唯一完成事实仍由GameSession规则产生。
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity.ArtRebuild
{
    /// <summary>推广已验收TA04的两种接触；每面最多64段，返回与暂停取消尚未结束的手势。</summary>
    public sealed class AtelierFlowPalette : MonoBehaviour
    {
        public AtelierFlowGameplay Flow;
        public Renderer Surface;
        public Collider SurfacePicking;
        public Transform RestKnife, PointerKnife;
        public Shader PaletteShader;
        private GameSession session;
        private Material original, owned;
        private MaterialPropertyBlock block;
        private readonly Vector4[] erase = new Vector4[64], spread = new Vector4[64];
        private readonly bool[] dryBins = new bool[40], wetBins = new bool[40];
        private readonly int[] tapCounts = new int[2];
        private int eraseCount, spreadCount;
        private bool held, moved, brushTried;
        private Vector2 previous, pressed;
        /// <summary>仅表示当前选择了工具，不授予刮开或推色经验。</summary>
        public bool Equipped { get; private set; }
        /// <summary>false为刀刃，true为刀面；切换不能完成任何接触。</summary>
        public bool FaceMode { get; private set; }
        /// <summary>试区横向覆盖率；相同位置重复划过不会增加。</summary>
        public float DryCoverage => dryBins.Count(x=>x)/40f;
        /// <summary>软色区横向覆盖率，与干色独立。</summary>
        public float WetCoverage => wetBins.Count(x=>x)/40f;
        private bool CanContact => Flow.CanUseStageThreeInput && Flow.IsPaletteFocused && Equipped
            && session.State.Knows(AtelierFlowRules.DryPalette) && session.State.Knows(AtelierFlowRules.PaletteKnife)
           ;

        /// <summary>克隆自有材质，重开不污染素材；已成立事实恢复为同一典型刀痕。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            original = Surface.sharedMaterial;
            owned = new Material(original) { shader=PaletteShader,name="主流程_自有刮刀表面" };
            Surface.sharedMaterial = owned;
            block = new MaterialPropertyBlock();
            if (session.State.HasFact(AtelierFlowRules.DryLearned))
            { erase[eraseCount++]=new Vector4(.10f,.5f,.48f,.5f); for(int n=0;n<40;n++) dryBins[n]=true; }
            if (session.State.HasFact(AtelierFlowRules.WetLearned))
            { spread[spreadCount++]=new Vector4(.64f,.5f,.94f,.5f); for(int n=0;n<40;n++) wetBins[n]=true; }
            CancelGesture();
            ApplySurface();
        }
        /// <summary>只在实际亲点刮刀以后选择；初次默认使用刀刃。</summary>
        public void Equip()
        {
            if (!Flow.CanUseStageThreeInput || !session.State.Knows(AtelierFlowRules.PaletteKnife)) return;
            if (!Equipped) FaceMode=false;
            Equipped=true;
            CancelGesture();
            Flow.Voice.Play("G_KNIFE_DRY");
        }
        /// <summary>切换刀面只取消旧手势，不把按住的拖动转成另一种完成动作。</summary>
        public void SelectMode(bool face)
        {
            if (!Flow.CanUseStageThreeInput || !Equipped) return;
            CancelGesture();
            FaceMode=face;
        }
        /// <summary>试取干色只留浅痕，不形成经验、词条或湿颜料。</summary>
        public void TryBrush()
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsPaletteFocused || !session.State.Knows(AtelierFlowRules.DryPalette)) return;
            brushTried=true;
            ApplySurface();
            Flow.ShowWorldObservation("颜料干了，笔尖只留下一道浅痕。试试板旁的刮刀吧。");
        }
        /// <summary>接收原始板面UV；外部UI与自动回放复用同一接触许可，不接受任意完成值。</summary>
        /// <param name="from">上一个有效接触点。</param>
        /// <param name="to">当前有效接触点。</param>
        /// <param name="face">当前刀面模式，必须与已选工具一致。</param>
        /// <param name="assistTap">一次已松开的短点按；沿固定试道逐段辅助，不按帧计次数。</param>
        /// <returns>是否在当前模式的实际试区留下刀痕。</returns>
        public bool Contact(Vector2 from,Vector2 to,bool face,bool assistTap=false)
        {
            if (!CanContact || !Inside(from) || !Inside(to)) return false;
            if(face&&!session.State.HasFact(AtelierFlowRules.DryLearned))return false;
            FaceMode=face;
            float low=face?.60f:.06f, high=face?.96f:.52f;
            if (from.x<low || from.x>high || to.x<low || to.x>high
                || from.y<.12f || from.y>.88f || to.y<.12f || to.y>.88f) return false;
            int side=face?1:0;
            if (assistTap)
            {
                int tap=Mathf.Min(tapCounts[side]++,2);
                float start=low+.03f+(high-low-.06f)*tap/3;
                from=new Vector2(start,.5f);
                to=new Vector2(start+(high-low-.06f)/3,.5f);
            }
            var segments=face?spread:erase;
            int count=face?spreadCount:eraseCount;
            // 有限数组满后复用最后一段，后续有效接触仍立即可见，不会出现“没画出痕却完成”。
            segments[Mathf.Min(count,63)]=new Vector4(from.x,from.y,to.x,to.y);
            if (count<64) { if (face) spreadCount++; else eraseCount++; }
            var bins=face?wetBins:dryBins;
            float min=Mathf.Min(from.x,to.x)-.022f, max=Mathf.Max(from.x,to.x)+.022f;
            for (int n=0;n<40;n++)
            {
                float x=Mathf.Lerp(low,high,(n+.5f)/40);
                if (x>=min && x<=max) bins[n]=true;
            }
            ApplySurface();
            var fact=face?AtelierFlowRules.WetLearned:AtelierFlowRules.DryLearned;
            if (bins.Count(x=>x)>=16 && (!assistTap || tapCounts[side]>=3) && !session.State.HasFact(fact))
            {
                session.PerformAction(AtelierFlowRules.DryPalette,face?"palette_spread":"palette_scrape");
                if(!face)Flow.Voice.Play("G_KNIFE_WET");
                ApplySurface();
            }
            return true;
        }
        private static bool Inside(Vector2 point) => point.x>=0 && point.x<=1 && point.y>=0 && point.y<=1;
        /// <summary>从实际首个射线命中取得UV；UI、遮挡物和板外都不能涂色。</summary>
        public bool HandlePointer(bool overUI)
        {
            if (!CanContact || overUI || Mouse.current==null) { CancelGesture();return false; }
            var mouse=Mouse.current;
            var ray=Flow.References.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
            var hits=Physics.RaycastAll(ray,40,Flow.References.PickingMask,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
            if (hits.Length==0 || hits[0].collider!=SurfacePicking) { CancelGesture();return false; }
            Vector2 uv=WorldUV(hits[0].point);
            if(!held)FaceMode=uv.x>.56f&&session.State.HasFact(AtelierFlowRules.DryLearned);
            PointerKnife.gameObject.SetActive(true);
            RestKnife.gameObject.SetActive(false);
            PointerKnife.position=hits[0].point+Vector3.up*(FaceMode?.006f:.0265f);
            PointerKnife.rotation=Quaternion.Euler(0,-20,FaceMode?8:68);
            if (mouse.leftButton.wasPressedThisFrame)
            { previous=pressed=uv;held=true;moved=false; }
            else if (held && mouse.leftButton.isPressed && Vector2.Distance(previous,uv)>.012f)
            {
                moved=true;
                Contact(previous,uv,FaceMode);
                previous=uv;
            }
            if (held && mouse.leftButton.wasReleasedThisFrame)
            {
                if (!moved && Vector2.Distance(pressed,uv)<.025f) Contact(uv,uv,FaceMode,true);
                held=false;
            }
            else if (held && !mouse.leftButton.isPressed) held=false;
            return true;
        }
        /// <summary>同一世界表面UV用于接触与Shader，避免预览和实际刀痕错位。</summary>
        public Vector2 WorldUV(Vector3 point)
        {
            var b=Surface.bounds;
            return new Vector2((point.x-b.min.x)/b.size.x,(point.z-b.min.z)/b.size.z);
        }
        /// <summary>暂停、返回、开笔记、失焦、切模式都停止旧手势；已留下的痕迹保留。</summary>
        public void CancelGesture()
        {
            held=false;
            if (PointerKnife!=null) PointerKnife.gameObject.SetActive(false);
            if (RestKnife!=null) RestKnife.gameObject.SetActive(true);
        }
        private void ApplySurface()
        {
            var b=Surface.bounds;
            Surface.GetPropertyBlock(block);
            block.SetVector("_ProbeOrigin",b.center);
            block.SetVector("_ProbeScale",new Vector4(b.size.x,b.size.z,0,0));
            block.SetFloat("_ProbeRadius",.048f);
            block.SetFloat("_ProbeEraseCount",eraseCount);
            block.SetFloat("_ProbeSpreadCount",spreadCount);
            block.SetVectorArray("_ProbeErase",erase);
            block.SetVectorArray("_ProbeSpread",spread);
            block.SetFloat("_DryBrushTried",brushTried?1:0);
            block.SetFloat("_WetExposed",session.State.HasFact(AtelierFlowRules.DryLearned)?1:0);
            Surface.SetPropertyBlock(block);
        }
        private void OnDisable() => CancelGesture();
        private void OnDestroy()
        {
            if (Surface!=null && original!=null) { Surface.sharedMaterial=original;Surface.SetPropertyBlock(null); }
            if (owned!=null) Destroy(owned);
        }
    }
}
