// 文件：AtelierFlowStageFour.cs
// 职责：协调实体点灯、最终工具、持久屋痕、亲手调查及两条结尾的场景适配。
// 边界：临时选工具与镜头不写事实；所有收色、揭底和终局均通过同一个GameSession。
using System;
using System.Collections;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>独立功能占位，UI同学可替换控件并直接复用公开入口。</summary>
    public sealed class AtelierFlowStageFour : MonoBehaviour
    {
        public AtelierFlowGameplay Flow;
        public AtelierFinaleTool Tool;
        public Transform RoofFocus,RoomFocus;
        public Vector3 RoofOrigin;
        public Vector2 RoofSize=new Vector2(1.35f,1.35f);
        public LineRenderer RoofTrace;
        public Collider[] RoofOccluders=Array.Empty<Collider>();
        private MaterialPropertyBlock traceProperties;
        public AtelierInteractionTarget RoofTarget,SkyTarget,MarkTarget,RoomTarget;
        public GameObject Panel,EndingPanel;
        public CanvasGroup EndingFade;
        public Text Instruction,EndingTitle;
        public Button PaintButton,FaceButton,EdgeButton,TargetButton,UndoButton,AgainButton,RetryButton,QuitButton;
        private GameSession session;
        private bool glimmerPlayed;
        private float glimmerAge=100;
        private Coroutine ending;
        private bool n07Completed;
        /// <summary>当前选中的真实表面；只由笔记选择或正式UI选择事件更新。</summary>
        public bool RoofTargetSelected { get; private set; }
        /// <summary>终局命令已经接受；演出是否完成不再影响互斥结果。</summary>
        public bool Ending => session!=null&&(session.State.CanvasLocked||session.State.HasFact(AtelierRules.RoomErased));
        /// <summary>结尾菜单已经可见，供运行检查读取。</summary>
        public bool MenuVisible => EndingPanel!=null&&EndingPanel.activeSelf;
        /// <summary>绑定唯一会话，恢复纯表现并接上可替换按钮；不补发任何玩家动作。</summary>
        public void Bind(GameSession game)
        {
            session=game;
            Flow.References.Lamp.KeepMatchBurning=true;
            Flow.References.Lamp.Bind(game);
            Flow.References.Finale.FourStageFlow=true;
            Flow.References.Finale.FlowRoofOrigin=RoofOrigin;
            Flow.References.Finale.FlowRoofSize=RoofSize;
            Flow.References.Finale.Bind(game);
            Tool.Bind(game);
            session.EventRaised+=OnEvent;
            Flow.Voice.LineCompleted+=NarrationCompleted;
            Flow.References.Notebook.ClueRead+=OnClueRead;
            PaintButton.onClick.AddListener(()=>Tool.SelectMode(AtelierFinaleToolMode.Paint));
            FaceButton.onClick.AddListener(()=>Tool.SelectMode(AtelierFinaleToolMode.Face));
            EdgeButton.onClick.AddListener(()=>Tool.SelectMode(AtelierFinaleToolMode.Edge));
            TargetButton.onClick.AddListener(()=>Flow.References.Notebook.SetOpen(true));
            UndoButton.onClick.AddListener(()=>Flow.CanvasPort.Undo());
            AgainButton.onClick.AddListener(Flow.RestartGame);
            RetryButton.onClick.AddListener(Flow.RetryFourthStage);
            QuitButton.onClick.AddListener(()=>UnityEngine.Application.Quit());
            Panel.SetActive(false);EndingPanel.SetActive(false);EndingFade.alpha=0;
            EndingFade.blocksRaycasts=false;
        }
        private void NarrationCompleted(string key) {if(key=="N07")n07Completed=true;}
        private void OnClueRead(ClueId clue) { if(clue==new ClueId(StudioRules.Ceiling.Value)) SelectRoofTarget(); }
        /// <summary>正式笔记选择已知屋面时调用；不代替屋面首次调查和刀刃接触。</summary>
        public void SelectRoofTarget()
        {
            if(session==null||!session.State.Knows(StudioRules.Ceiling)||Ending)return;
            RoofTargetSelected=true;
        }
        private void OnEvent(DomainEvent e)
        {
            if(!(e is FactAdded fact))return;
            if(fact.Fact==StudioRules.LampTurned) Flow.Voice.Play("N06");
            if(fact.Fact==AtelierRules.CanvasDelivered&&ending==null) ending=StartCoroutine(EndCanvas());
            if(fact.Fact==AtelierRules.RoomErased&&ending==null) ending=StartCoroutine(EndWorld());
            if(fact.Fact==AtelierRules.RoofErased) Flow.ReturnFromToolForWorld();
        }
        /// <summary>渲染已开孔的区域也不再用整片屋面碰撞挡住后方天空。</summary>
        public bool SkipPicking(RaycastHit hit)
        {
            var target=hit.collider.GetComponent<AtelierInteractionTarget>();
            if((target==null||target.Kind!=AtelierTargetKind.Ceiling)&&Array.IndexOf(RoofOccluders,hit.collider)<0)return false;
            if(session.State.HasFact(AtelierRules.RoofErased)&&Flow.References.Finale.RoofClear(hit.point))return true;
            if(!session.State.HasFact(AtelierFlowRules.RoofProbed)||session.State.AtelierCanvas.RoofProbe.Count==0)return false;
            var path=session.State.AtelierCanvas.RoofProbe;
            var first=path[0];var last=path[path.Count-1];
            Vector2 a=new Vector2(RoofOrigin.x+(first.U0/10000f-.5f)*RoofSize.x,RoofOrigin.z+(first.V0/10000f-.5f)*RoofSize.y);
            Vector2 b=new Vector2(RoofOrigin.x+(last.U1/10000f-.5f)*RoofSize.x,RoofOrigin.z+(last.V1/10000f-.5f)*RoofSize.y);
            Vector2 p=new Vector2(hit.point.x,hit.point.z),d=b-a;
            return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude)))<.065f;
        }
        /// <summary>只声明目前可见且规则允许的物体，不因悬停授予观察。</summary>
        public bool Allowed(AtelierInteractionTarget target)
        {
            if(session==null||Ending||Flow.Draft.VisibleStage<4)return false;
            switch(target.Kind)
            {
                case AtelierTargetKind.OilLamp: return session.IsInspectable(StudioRules.LampBody);
                case AtelierTargetKind.Matches:
                case AtelierTargetKind.MatchStriker:
                case AtelierTargetKind.Wick: return Flow.IsLampFocused;
                case AtelierTargetKind.Ceiling: return RoofTrace.gameObject.activeSelf&&session.IsInspectable(StudioRules.Ceiling)&&!session.State.HasFact(AtelierRules.RoofErased);
                case AtelierTargetKind.Sky: return Flow.References.Finale.RoofSettled&&session.IsInspectable(StudioRules.Sky);
                case AtelierTargetKind.SharedMark: return Flow.References.Finale.RoofSettled&&session.State.Knows(StudioRules.Sky);
                case AtelierTargetKind.Room: return Flow.References.Finale.RoofSettled&&session.IsInspectable(StudioRules.Room);
                default:return false;
            }
        }
        /// <summary>真实点击入口；火柴先沿砂纸划亮，火柴头实际靠近灯芯后才提交点灯。</summary>
        public void Observe(AtelierInteractionTarget target)
        {
            if(!Flow.CanUseStageThreeInput||!Allowed(target))return;
            var lamp=Flow.References.Lamp;
            switch(target.Kind)
            {
                case AtelierTargetKind.OilLamp:
                    session.Inspect(StudioRules.LampBody);
                    Flow.FocusStageFour("lamp",Flow.References.LampFocusPose,43);break;
                case AtelierTargetKind.Matches:
                    session.Inspect(AtelierRules.Matches);lamp.TakeMatch();
                    Flow.ShowWorldObservation("沿火柴盒侧面的砂纸划一下。");break;
                case AtelierTargetKind.MatchStriker:
                    if(!lamp.HasMatch) {Flow.ShowWorldObservation("先从盒里取一根火柴。");break;}
                    if(!session.State.HasFact(AtelierRules.MatchStruck)) session.PerformAction(AtelierRules.Matches,"strike_match");
                    else lamp.StrikeAgain();
                    Flow.ShowWorldObservation("把火柴头移到灯芯旁，再点灯芯。");break;
                case AtelierTargetKind.Wick:
                    if(session.State.HasFact(StudioRules.LampTurned))break;
                    if(!lamp.CanTouchWick) {Flow.ShowWorldObservation("把划着的火柴头移近灯芯。");break;}
                    lamp.HoldAtWick();session.PerformAction(StudioRules.LampBody,"light_wick");break;
                case AtelierTargetKind.Ceiling:
                    bool known=session.State.Knows(StudioRules.Ceiling);
                    session.Inspect(StudioRules.Ceiling);
                    Flow.FocusStageFour("roof",RoofFocus,50);
                    if(known)Flow.ShowWorldObservation("在笔记中选择屋面，再用刀刃沿细痕刮开。");break;
                case AtelierTargetKind.Sky: session.Inspect(StudioRules.Sky);break;
                case AtelierTargetKind.SharedMark:
                    session.Inspect(AtelierRules.SharedMark);session.PerformAction(AtelierRules.SharedMark,"study_shared_mark");break;
                case AtelierTargetKind.Room:
                    session.Inspect(StudioRules.Room);Flow.FocusStageFour("room_surface",RoomFocus,50);
                    Flow.ShowWorldObservation("沿这道笔迹继续。");break;
            }
        }
        private void Update()
        {
            if(session==null)return;
            var state=session.State;
            bool lampLit=state.HasFact(StudioRules.LampTurned);
            bool readyForRoof=lampLit&&Flow.Voice.ActiveKey!="N06"&&Flow.Voice.ActiveKey!="N05"&&Flow.Draft.VisibleStage>=4&&!Flow.IsStageTransition;
            RoofTrace.gameObject.SetActive(readyForRoof&&!state.HasFact(AtelierRules.RoofErased)&&!Ending);
            RoofTarget.GetComponent<Collider>().enabled=readyForRoof&&!state.HasFact(AtelierRules.RoofErased);
            SkyTarget.GetComponent<Collider>().enabled=state.HasFact(AtelierRules.RoofErased)&&Flow.References.Finale.RoofSettled&&!Ending;
            MarkTarget.GetComponent<Collider>().enabled=state.Knows(StudioRules.Sky)&&Flow.References.Finale.RoofSettled&&!Ending;
            RoomTarget.GetComponent<Collider>().enabled=state.HasFact(AtelierRules.SharedMarkSeen)&&!Ending;
            if(readyForRoof)
            {
                // N06全组结束才让独立屋顶目标显色，错过短闪也一直保留可调查细痕。
                Flow.Draft.RevealRoof();
                if(!glimmerPlayed&&Flow.IsRoomView) {glimmerPlayed=true;glimmerAge=0;}
                glimmerAge+=Time.deltaTime;
                float flash=glimmerAge<1.4f?Mathf.Sin(glimmerAge/1.4f*Mathf.PI)*.32f:0;
                traceProperties ??= new MaterialPropertyBlock();
                RoofTrace.GetPropertyBlock(traceProperties);
                traceProperties.SetColor("_BaseColor",new Color(.10f+flash,.22f+flash,.33f+flash));
                RoofTrace.SetPropertyBlock(traceProperties);
                bool spread=state.AtelierCanvas.SpreadRevision==state.AtelierCanvas.Revision;
                RoofTrace.positionCount=spread?5:4;
                for(int n=0;n<RoofTrace.positionCount;n++)
                    RoofTrace.SetPosition(n,RoofOrigin+new Vector3((n-1.5f)*.075f,-.008f,Mathf.Sin(n)*.012f));
            }
            if(state.HasFact(AtelierRules.RoomErased))Flow.Draft.ReleaseForWorld(Flow.References.Finale.Erasure);
            bool use=Flow.CanUseStageThreeInput&&!Ending;
            bool canvas=use&&Flow.IsCanvasFocused&&state.AtelierCanvas.Current.Id==4&&!Flow.UsesExternalCanvasWorkspace;
            bool roof=use&&Flow.IsRoofFocused;
            bool room=use&&Flow.IsRoomSurfaceFocused;
            bool lamp=use&&Flow.IsLampFocused;
            Panel.SetActive(canvas||roof||room||lamp);
            if(!Panel.activeSelf)return;
            bool three=state.AtelierCanvas.Layers[3].Count>=3;
            PaintButton.gameObject.SetActive(canvas);
            FaceButton.gameObject.SetActive(canvas);
            EdgeButton.gameObject.SetActive(roof||room);
            TargetButton.gameObject.SetActive(roof&&!RoofTargetSelected);
            UndoButton.gameObject.SetActive(canvas);
            FaceButton.interactable=three;
            EdgeButton.interactable=three&&(room||RoofTargetSelected);
            PaintButton.interactable=Tool.Mode!=AtelierFinaleToolMode.Paint;
            UndoButton.interactable=state.AtelierCanvas.Layers[3].Count>0;
            if(lamp) Instruction.text=lampLit?"灯已点亮，可以回到画布。":!Flow.References.Lamp.HasMatch?"点火柴盒取一根火柴。":!Flow.References.Lamp.HasBurningMatch?"点盒侧砂纸划燃，再靠近灯芯。":"把火柴头移到灯芯旁，再点灯芯。";
            else if(roof) Instruction.text=!RoofTargetSelected?"在笔记中选择已记录的屋面。":!three?"先完成本阶段至少三笔绘画。":state.HasFact(AtelierFlowRules.RoofProbed)?"小孔已露出。可以返回，也可以沿边缘继续刮开。":"选择刀刃，沿细痕拖一小段；也可短点三次。";
            else if(room) Instruction.text="选择刀刃，沿接连的笔迹刮开一小段。";
            else Instruction.text=!three?"使用暖星笔刷，至少落三笔。":Tool.Mode==AtelierFinaleToolMode.Face?"沿已有颜料推一小段；也可短点三次。":Flow.CanvasPort.CanFinish?"可以继续作画，也可以完成绘画或返回画室。":"用刮刀涂抹画布上的颜料。";
        }
        private IEnumerator EndCanvas()
        {
            Flow.PrepareEnding();Tool.Cancel();
            Flow.Voice.Play("N07");
            // 结尾保留实际作品；缓动只靠近少许，不把外部成品图盖上去。
            yield return new WaitForSeconds(.1f);
            while(!n07Completed)yield return null;
            yield return new WaitForSeconds(2);
            yield return FadeAndMenu("重新点亮");
        }
        private IEnumerator EndWorld()
        {
            Flow.PrepareEnding();Tool.Cancel();
            while(!Flow.References.Finale.Finished)yield return null;
            yield return new WaitForSeconds(3);
            yield return FadeAndMenu("星之河之外");
        }
        private IEnumerator FadeAndMenu(string title)
        {
            EndingFade.blocksRaycasts=true;
            for(float t=0;t<1;t+=Time.deltaTime/2) {EndingFade.alpha=t;yield return null;}
            EndingFade.alpha=1;EndingTitle.text=title;
            EndingPanel.SetActive(true);
        }
        private void OnDestroy()
        {
            if(session!=null)session.EventRaised-=OnEvent;
            if(Flow!=null&&Flow.Voice!=null)Flow.Voice.LineCompleted-=NarrationCompleted;
            if(Flow!=null&&Flow.References.Notebook!=null)Flow.References.Notebook.ClueRead-=OnClueRead;
        }
    }
}
