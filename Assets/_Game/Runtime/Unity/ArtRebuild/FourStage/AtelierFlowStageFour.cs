// 文件：AtelierFlowStageFour.cs
// 职责：协调点灯退镜、屋顶解释、湿色落地、室内擦开及天空两次点击。
// 边界：演出只读事实；每个物体来源、表面操作和天空落笔均通过同一会话提交。
using System;
using System.Collections;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>有限主流程适配；旧面板引用只为场景兼容保留，不再向玩家显示。</summary>
    public sealed class AtelierFlowStageFour : MonoBehaviour
    {
        public AtelierFlowGameplay Flow;
        public AtelierFinaleTool Tool;
        public Transform RoofFocus,RoomFocus,SkyFocus;
        public Vector3 RoofOrigin;
        public Vector2 RoofSize=new Vector2(1.35f,1.35f);
        public LineRenderer RoofTrace;
        public Renderer RoofCoat;
        public Renderer[] WetPuddles=Array.Empty<Renderer>(),FallingDrops=Array.Empty<Renderer>();
        public Vector3[] LandingPoints=Array.Empty<Vector3>();
        public Collider[] RoofOccluders=Array.Empty<Collider>(),InteriorColliders=Array.Empty<Collider>();
        public AtelierInteractionTarget RoofTarget,SkyTarget,MarkTarget,RoomTarget;
        /// <summary>主视角可见的桌面落点，亲点后移近同一片流到地板的湿色。</summary>
        public AtelierInteractionTarget RoomEntryTarget;
        public GameObject Panel,EndingPanel;
        public CanvasGroup EndingFade;
        public Text Instruction,EndingTitle;
        /// <summary>结尾副题由路线决定，标题仍沿用原稿的“重新点亮／星之河之外”。</summary>
        public Text EndingSubtitle;
        public Button PaintButton,FaceButton,EdgeButton,TargetButton,UndoButton,AgainButton,RetryButton,QuitButton;
        private GameSession session;
        private Coroutine ending;
        private bool n06Completed,n07Completed,roofThought,restPlayed,lampPrompt,collidersCleared;
        private float roofThoughtAge,wetAge,coatAmount;
        private MaterialPropertyBlock coatBlock;
        private MaterialPropertyBlock wetBlock;
        /// <summary>目标解释已存在；笔记阅读不再设置工具目标。</summary>
        public bool RoofTargetSelected => session!=null&&session.State.HasFact(AtelierFlowRules.RoofAsPaint);
        /// <summary>房间消失仍可开笔记；天空最后一笔或普通定稿之后才进入结尾。</summary>
        public bool Ending => session!=null&&(session.State.CanvasLocked||session.State.HasFact(AtelierFlowRules.SkyFinished));
        public bool MenuVisible => EndingPanel!=null&&EndingPanel.activeSelf;
        /// <summary>重绑只恢复表现，不能补发来源和动作。</summary>
        public void Bind(GameSession game)
        {
            session=game;n06Completed=false;
            Flow.References.Lamp.KeepMatchBurning=true;Flow.References.Lamp.Bind(game);
            var finale=Flow.References.Finale;
            finale.FourStageFlow=true;finale.FlowRoofOrigin=RoofOrigin;finale.FlowRoofSize=RoofSize;finale.Bind(game);
            Tool.Bind(game);session.EventRaised+=OnEvent;Flow.Voice.LineCompleted+=NarrationCompleted;
            AgainButton.onClick.AddListener(Flow.RestartGame);RetryButton.onClick.AddListener(Flow.RetryFourthStage);
            QuitButton.onClick.AddListener(()=>UnityEngine.Application.Quit());
            Panel.SetActive(false);EndingPanel.SetActive(false);EndingFade.alpha=0;EndingFade.blocksRaycasts=false;
            if(RoofTrace!=null)RoofTrace.gameObject.SetActive(false);
            foreach(var surface in WetPuddles.Concat(FallingDrops))if(surface!=null)surface.gameObject.SetActive(false);
            PrepareWetLook();
        }
        /// <summary>滩面由着色器裁出不规则边，尺寸取各自网格本地包围盒；下落液滴不参与成滩裁边。</summary>
        private void PrepareWetLook()
        {
            wetBlock??=new MaterialPropertyBlock();
            // 三个落点的网格尺寸不同，必须逐件传入半尺寸，不能用同一固定半径。
            foreach(var puddle in WetPuddles)
            {
                if(puddle==null)continue;
                var extents=puddle.localBounds.extents;
                wetBlock.Clear();
                wetBlock.SetFloat("_Puddle",1);
                wetBlock.SetVector("_PuddleHalfSize",new Vector4(Mathf.Max(.001f,extents.x),Mathf.Max(.001f,extents.z),0,0));
                puddle.SetPropertyBlock(wetBlock);
            }
            // 下落滴液共用同一材质；关掉成滩裁边后保留球面蓝渐变。
            foreach(var drop in FallingDrops)
            {
                if(drop==null)continue;
                wetBlock.Clear();
                wetBlock.SetFloat("_Puddle",0);
                drop.SetPropertyBlock(wetBlock);
            }
        }
        private void NarrationCompleted(string key)
        {
            if(key=="N06")n06Completed=true;
            if(key=="N07")n07Completed=true;
            if(key=="G_ROOM_ANGRY")Flow.ShowWorldObservation(AtelierFlowNarration.TextFor("V_WET"));
        }
        /// <summary>兼容旧UI调用；阅读来源不再装备或选择屋顶。</summary>
        public void SelectRoofTarget() { }
        private void OnEvent(DomainEvent raised)
        {
            if(!(raised is FactAdded fact))return;
            if(fact.Fact==StudioRules.LampTurned){Flow.ReturnFromToolForWorld();Flow.Voice.Play("N06");}
            if(fact.Fact==AtelierFlowRules.RoofProbed)Flow.Voice.Play("G_ROOF_WARN");
            if(fact.Fact==AtelierRules.RoofErased){Flow.ReturnFromToolForWorld();Flow.Voice.Play("G_ROOM_ANGRY");}
            if(fact.Fact==AtelierRules.RoomErased){Tool.Cancel();Flow.ReturnFromToolForWorld();}
            if(fact.Fact==AtelierFlowRules.SkyCloudPainted)Flow.ShowWorldObservation(AtelierFlowNarration.TextFor("V_STAR"));
            if(fact.Fact==AtelierRules.CanvasDelivered&&ending==null)ending=StartCoroutine(EndCanvas());
            if(fact.Fact==AtelierFlowRules.SkyFinished&&ending==null)ending=StartCoroutine(EndWorld());
        }
        /// <summary>已擦去的屋顶不再挡住后面的真实天空拾取。</summary>
        public bool SkipPicking(RaycastHit hit)
        {
            var target=hit.collider.GetComponent<AtelierInteractionTarget>();
            if((target==null||target.Kind!=AtelierTargetKind.Ceiling)&&Array.IndexOf(RoofOccluders,hit.collider)<0)return false;
            if(session.State.HasFact(AtelierRules.RoofErased)&&Flow.References.Finale.RoofClear(hit.point))return true;
            if(!session.State.HasFact(AtelierFlowRules.RoofProbed)||session.State.AtelierCanvas.RoofProbe.Count==0)return false;
            var path=session.State.AtelierCanvas.RoofProbe;var first=path[0];var last=path[path.Count-1];
            Vector2 a=new Vector2(RoofOrigin.x+(first.U0/10000f-.5f)*RoofSize.x,RoofOrigin.z+(first.V0/10000f-.5f)*RoofSize.y);
            Vector2 b=new Vector2(RoofOrigin.x+(last.U1/10000f-.5f)*RoofSize.x,RoofOrigin.z+(last.V1/10000f-.5f)*RoofSize.y);
            Vector2 p=new Vector2(hit.point.x,hit.point.z),d=b-a;
            return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude)))<.065f;
        }
        /// <summary>只开放目前已显色的现场目标；恢复画面不代表自动认识。</summary>
        public bool Allowed(AtelierInteractionTarget target)
        {
            if(session==null||Ending||Flow.Draft.VisibleStage<4)return false;
            bool erased=session.State.HasFact(AtelierRules.RoomErased);
            switch(target.Kind)
            {
                case AtelierTargetKind.OilLamp:return !erased&&session.IsInspectable(StudioRules.LampBody);
                case AtelierTargetKind.Matches:
                case AtelierTargetKind.MatchStriker:
                case AtelierTargetKind.Wick:return !erased&&Flow.IsLampFocused;
                case AtelierTargetKind.Ceiling:return ReadyRoof&&!session.State.HasFact(AtelierRules.RoofErased);
                case AtelierTargetKind.Sky:return Flow.References.Finale.InteriorSettled&&session.IsInspectable(StudioRules.Sky);
                case AtelierTargetKind.Room:return !erased&&wetAge>3&&session.IsInspectable(StudioRules.Room);
                default:return false;
            }
        }
        // 等待完整播放事件；N06排队等待旧句结束时也不能提前揭屋顶。
        private bool ReadyRoof=>session.State.HasFact(StudioRules.LampTurned)&&n06Completed
            &&Flow.Draft.VisibleStage>=4&&!Flow.IsStageTransition&&!Flow.CameraMoving;
        /// <summary>直接观察与实体操作入口，不再弹世界操作面板。</summary>
        public void Observe(AtelierInteractionTarget target)
        {
            if(!Flow.CanUseStageThreeInput||!Allowed(target))return;
            var lamp=Flow.References.Lamp;
            switch(target.Kind)
            {
                case AtelierTargetKind.OilLamp:
                    session.Inspect(StudioRules.LampBody);Flow.FocusStageFour("lamp",Flow.References.LampFocusPose,43);
                    if(!lamp.HasMatch)Flow.Voice.Play("G_MATCH");break;
                case AtelierTargetKind.Matches:
                    session.Inspect(AtelierRules.Matches);lamp.TakeMatch();Flow.ShowWorldObservation("沿火柴盒侧面的砂纸划一下。");break;
                case AtelierTargetKind.MatchStriker:
                    if(!lamp.HasMatch){Flow.ShowWorldObservation("先从盒里取一根火柴。");break;}
                    if(!session.State.HasFact(AtelierRules.MatchStruck))session.PerformAction(AtelierRules.Matches,"strike_match");else lamp.StrikeAgain();
                    Flow.ShowWorldObservation("把火柴头移到灯芯旁，再点灯芯。");break;
                case AtelierTargetKind.Wick:
                    if(session.State.HasFact(StudioRules.LampTurned))break;
                    if(!lamp.CanTouchWick){Flow.ShowWorldObservation("把划着的火柴头移近灯芯。");break;}
                    lamp.HoldAtWick();session.PerformAction(StudioRules.LampBody,"light_wick");break;
                case AtelierTargetKind.Ceiling:
                    session.Inspect(StudioRules.Ceiling);Flow.FocusStageFour("roof",RoofFocus,50);break;
                case AtelierTargetKind.Room:
                    session.Inspect(StudioRules.Room);Flow.FocusStageFour("room_surface",RoomFocus,50);break;
                case AtelierTargetKind.Sky:
                    if(!session.State.Knows(StudioRules.Sky)){session.Inspect(StudioRules.Sky);break;}
                    if(!session.State.HasFact(AtelierFlowRules.SkyAsCanvas))
                    {Flow.ShowWorldObservation("把‘天空’和‘画布’放在笔记里，联系起来试试。");break;}
                    if(!Flow.IsSkyFocused)
                    {
                        Flow.FocusStageFour("sky",SkyFocus,65);
                        Flow.ShowWorldObservation(AtelierFlowNarration.TextFor(session.State.HasFact(AtelierFlowRules.SkyCloudPainted)?"V_STAR":"V_CLOUD"));
                    }
                    else PaintSky();
                    break;
            }
        }
        /// <summary>每个实际点击只提交一个步骤，不给旧作添加笔触或扣除颜料。</summary>
        public AtelierCanvasResult PaintSky()
        {
            if(!Flow.CanUseStageThreeInput||!Flow.IsSkyFocused||!Flow.References.Finale.InteriorSettled)
                return new AtelierCanvasResult(false,"先看看天空。");
            return session.ClickAtelierSky(session.SessionEpoch,session.State.HasFact(AtelierFlowRules.SkyCloudPainted));
        }
        private void Update()
        {
            if(session==null)return;
            Panel.SetActive(false);
            if(RoofTrace!=null)RoofTrace.gameObject.SetActive(false);
            var state=session.State;
            if(!lampPrompt&&Flow.Draft.VisibleStage>=4&&!Flow.IsStageTransition&&!Flow.CameraMoving&&!state.HasFact(StudioRules.LampTurned))
            {lampPrompt=true;Flow.Voice.Play("G_LAMP");}
            if(ReadyRoof)
            {
                Flow.Draft.RevealRoof();
                if(!roofThought&&Flow.Draft.WeightForStage(0)>.98f)
                {roofThought=true;Flow.ShowWorldObservation(AtelierFlowNarration.TextFor("V_ROOF"));}
            }
            if(roofThought&&!restPlayed)
            {roofThoughtAge+=Time.deltaTime;if(roofThoughtAge>2.6f){restPlayed=true;Flow.Voice.Play("G_REST");}}
            RoofTarget.GetComponent<Collider>().enabled=ReadyRoof&&!state.HasFact(AtelierRules.RoofErased)&&!Ending;
            MarkTarget.GetComponent<Collider>().enabled=false;
            RoomTarget.GetComponent<Collider>().enabled=wetAge>3&&!state.HasFact(AtelierRules.RoomErased)&&!Ending;
            if(RoomEntryTarget!=null)RoomEntryTarget.GetComponent<Collider>().enabled=wetAge>3&&!state.HasFact(AtelierRules.RoomErased)&&!Ending;
            SkyTarget.GetComponent<Collider>().enabled=Flow.References.Finale.InteriorSettled&&!Ending;
            if(RoofCoat!=null)
            {
                RoofCoat.gameObject.SetActive(state.HasFact(AtelierFlowRules.RoofAsPaint));
                coatAmount=Mathf.MoveTowards(coatAmount,state.HasFact(AtelierFlowRules.RoofAsPaint)?1:0,Time.deltaTime/1.2f);
                coatBlock??=new MaterialPropertyBlock();RoofCoat.GetPropertyBlock(coatBlock);
                // 非Properties参数必须由当前表现组件绑定，不能依赖编辑器材质中的临时值。
                coatBlock.SetFloat("_CoatReveal",coatAmount);
                coatBlock.SetFloat("_ProbeRoof",2);coatBlock.SetFloat("_AtelierEraseGroup",1);
                coatBlock.SetVector("_ProbeOrigin",RoofOrigin);
                coatBlock.SetVector("_ProbeScale",new Vector4(RoofSize.x,RoofSize.y,0,0));
                RoofCoat.SetPropertyBlock(coatBlock);
            }
            if(state.HasFact(AtelierRules.RoofErased))UpdateWetPaint();
            if(state.HasFact(AtelierRules.RoomErased)&&Flow.References.Finale.InteriorSettled&&!collidersCleared)
            {
                // 只让被擦去的实体让出拾取；画布暂留以表达主角的拒绝。
                collidersCleared=true;foreach(var collider in InteriorColliders)if(collider!=null)collider.enabled=false;
            }
        }
        private void UpdateWetPaint()
        {
            wetAge+=Time.deltaTime;
            for(int i=0;i<LandingPoints.Length;i++)
            {
                float impact=1.8f+i*.45f;float progress=Mathf.Clamp01(wetAge/impact);
                if(i<FallingDrops.Length&&FallingDrops[i]!=null)
                {
                    var drop=FallingDrops[i];drop.gameObject.SetActive(progress<1);
                    drop.transform.position=Vector3.Lerp(RoofOrigin+new Vector3((i-1)*.18f,-.05f,0),LandingPoints[i],progress*progress);
                }
                if(i<WetPuddles.Length&&WetPuddles[i]!=null)
                {
                    var puddle=WetPuddles[i];puddle.gameObject.SetActive(wetAge>=impact);
                    float growth=Mathf.SmoothStep(.12f,1,Mathf.Clamp01((wetAge-impact)/1.3f));
                    puddle.transform.localScale=new Vector3(growth,1,growth);
                }
            }
            Flow.Draft.ReleaseForWorld(Mathf.Clamp01((wetAge-1.8f)/7.5f));
        }
        private IEnumerator EndCanvas()
        {
            Flow.PrepareEnding();Tool.Cancel();Flow.Voice.Play("N07");
            while(!n07Completed)yield return null;
            yield return new WaitForSeconds(2);
            yield return FadeAndMenu("重新点亮","画留下了。房间还亮着，夜色也还在。");
        }
        private IEnumerator EndWorld()
        {
            Flow.PrepareEnding();Tool.Cancel();
            while(!Flow.References.Finale.Finished)yield return null;
            yield return new WaitForSeconds(3);
            yield return FadeAndMenu("星之河之外","房间退去了。星夜还在慢慢流动。");
        }
        private IEnumerator FadeAndMenu(string title,string subtitle)
        {
            EndingFade.blocksRaycasts=true;
            for(float t=0;t<1;t+=Time.deltaTime/2){EndingFade.alpha=t;yield return null;}
            EndingFade.alpha=1;EndingTitle.text=title;
            if(EndingSubtitle!=null)EndingSubtitle.text=subtitle;
            EndingPanel.SetActive(true);
        }
        private void OnDestroy()
        {
            if(session!=null)session.EventRaised-=OnEvent;
            if(Flow!=null&&Flow.Voice!=null)Flow.Voice.LineCompleted-=NarrationCompleted;
        }
    }
}
