// 文件：AtelierOpeningPlayerReview.StageThree.cs
// 职责：在正常前两阶段之后回放第三阶段来源、世界接触、有限合成与主动收层。
// 边界：复用场景实际入口和按钮；仅显式取证启用，不是正常玩家自动解谜或真人验收。
using System;
using System.Collections;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Game.Unity.ArtRebuild
{
    /// <summary>扩展同一正常会话，不恢复调试预设或直接发第三阶段笔刷。</summary>
    public sealed partial class AtelierOpeningPlayerReview
    {
        private IEnumerator ReviewThirdStage(AtelierFlowGameplay flow,Func<ClueId,ClueId,ClueId,string,IEnumerator> compose)
        {
            var refs=flow.References;
            var third=flow.StageThree;
            var palette=third.Palette;
            var canvas=flow.CanvasPort;
            var targets=FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include);
            AtelierInteractionTarget Target(AtelierTargetKind kind)=>targets.First(t=>t.Kind==kind);
            void FirstHit(AtelierTargetKind kind)
            {
                var collider=Target(kind).GetComponent<Collider>();
                var ray=refs.ViewCamera.ScreenPointToRay(refs.ViewCamera.WorldToScreenPoint(collider.bounds.center));
                var hits=Physics.RaycastAll(ray,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
                Check(hits.Length>0 && hits[0].collider==collider,"第三阶段目标被其他拾取面挡住："+kind);
            }
            Check(third!=null && flow.Draft.WeightForStage(3)>.99f,"N04完成后第三阶段尚未显色。");
            Check(flow.Draft.WeightForStage(4)==0 && flow.Draft.WeightForStage(0)==0,"第三阶段开放了油灯或屋顶。");
            foreach(var kind in new[] {AtelierTargetKind.DryPalette,AtelierTargetKind.PaletteKnife})
            {
                var b=Target(kind).GetComponent<Collider>().bounds;
                for(int n=0;n<8;n++)
                {
                    var v=refs.ViewCamera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1)));
                    Check(v.x>.03f && v.x<.97f && v.y>.05f && v.y<.95f && v.z>0,"第三阶段实物没有完整留在固定画幅内："+kind);
                }
                FirstHit(kind);
            }
            yield return Shot("16_第三阶段只新增窗景板与刀");
            flow.Observe(Target(AtelierTargetKind.Window));
            yield return new WaitForSeconds(1.5f);
            Check(new[] {"reality","night","stars"}.All(id=>!flow.Session.State.Knows(AtelierFlowRules.C(id))),"仅进入窗边就发了近远观察。");
            FirstHit(AtelierTargetKind.WindowNear);FirstHit(AtelierTargetKind.WindowFar);
            flow.Observe(Target(AtelierTargetKind.WindowFar));
            flow.Observe(Target(AtelierTargetKind.WindowNear));
            Check(flow.Session.State.HasFact(AtelierFlowRules.WindowNearSeen)&&flow.Session.State.HasFact(AtelierFlowRules.WindowFarSeen),"近远热点未分别记录实际观察。");
            Check(!flow.Session.State.Knows(AtelierFlowRules.C("stars")),"比较前提前授予星辰。");
            yield return Shot("17_近屋与远处灯火");
            third.CompareButton.onClick.Invoke();
            yield return new WaitForSeconds(1.5f);
            Check(flow.Session.State.Knows(AtelierFlowRules.C("stars")),"离远看看没有记录星辰意象。");
            yield return Shot("18_离远看看比较窗灯");
            flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
            flow.Observe(Target(AtelierTargetKind.DryPalette));yield return new WaitForSeconds(1.5f);
            Check(!flow.Session.State.Knows(AtelierFlowRules.C("chaos")),"拿起颜料板就提前给了细看观察。");
            third.StudyButton.onClick.Invoke();
            third.TryBrushButton.onClick.Invoke();
            yield return null;
            Check(flow.Session.State.Knows(AtelierFlowRules.C("chaos")),"细看颜料没有记录混乱。");
            Check(!flow.Session.State.Knows(AtelierFlowRules.PaletteKnife),"看板或试笔代领了刮刀。");
            Check(!palette.Contact(new Vector2(.1f,.4f),new Vector2(.4f,.4f),false),"没拿工具也能刮开。");
            yield return Shot("19_干壳试笔与刮刀摆放");
            flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
            yield return compose(AtelierFlowRules.C("chaos"),AtelierFlowRules.C("night"),new ClueId("atelier.flow.body"),"");
            yield return compose(AtelierFlowRules.C("reality"),AtelierFlowRules.C("stars"),new ClueId("atelier.flow.present"),"");
            yield return compose(new ClueId("atelier.flow.body"),new ClueId("atelier.flow.present"),AtelierFlowRules.Brush(3),"20_夜城笔刷双词形成");
            refs.Notebook.SetOpen(false);yield return null;yield return null;yield return null;
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.5f);
            for(int n=0;n<3;n++) Check(canvas.Commit(new Vector2(.24f+n*.25f,.24f+n*.04f),20,0).Accepted,"夜城图样落笔未接受。");
            Check(!canvas.CanFinish && !refs.ContextAction.interactable && !canvas.Finish().Accepted,"没有试两面就完成第三层。");
            yield return Shot("21_三笔后明确显示刀具门槛");
            third.LocateButton.onClick.Invoke();yield return new WaitForSeconds(1.5f);
            Check(!flow.Session.State.Knows(AtelierFlowRules.PaletteKnife),"定位入口自动领取了工具。");
            FirstHit(AtelierTargetKind.PaletteKnife);
            flow.Observe(Target(AtelierTargetKind.PaletteKnife));yield return new WaitForSeconds(.2f);
            Check(palette.Equipped && !palette.FaceMode,"亲点工具后没有默认选择刀刃。");
            Check(!palette.Contact(new Vector2(-.1f,.5f),new Vector2(.4f,.5f),false),"板外接触被接受。");
            Check(!palette.Contact(new Vector2(.7f,.5f),new Vector2(.9f,.5f),false),"刀刃在软色区错误授予干色经验。");
            palette.Contact(new Vector2(.1f,.4f),new Vector2(.14f,.4f),false);
            Check(!flow.Session.State.HasFact(AtelierFlowRules.DryLearned),"短小接触过早完成刀刃教学。");
            float partial=palette.DryCoverage;palette.CancelGesture();
            Check(palette.DryCoverage==partial && partial>0,"中断丢失了已经留下的有效痕迹。");
            refs.Notebook.SetOpen(true);yield return null;
            Check(!palette.Contact(new Vector2(.1f,.5f),new Vector2(.4f,.5f),false),"笔记打开时仍能刮板。");
            refs.Notebook.SetOpen(false);yield return null;yield return null;yield return null;
            bool tapReview=Array.IndexOf(Environment.GetCommandLineArgs(),"--atelier-knife-tap-review")>=0;
            Mouse reviewMouse=null;
            if(tapReview)
            {
                Check(UnityEngine.Application.isFocused,"短点回放时Player没有获得焦点。");
                reviewMouse=InputSystem.AddDevice<Mouse>();
                yield return TapPalette(flow,reviewMouse,new Vector2(.25f,.5f),AtelierFlowRules.DryLearned);
            }
            else Check(palette.Contact(new Vector2(.12f,.45f),new Vector2(.35f,.45f),false),"有效刀刃接触没有留下痕迹。");
            Check(flow.Session.State.HasFact(AtelierFlowRules.DryLearned) && !flow.Session.State.HasFact(AtelierFlowRules.WetLearned),"刀刃完成错误串到了刀面经验。");
            yield return null;
            Check(third.Instruction.text.Contains("刀面推色"),"刀刃完成后没有明确提示下一步切刀面。");
            yield return Shot("22_刀刃刮开露出木底");
            third.FaceButton.onClick.Invoke();
            Check(!palette.Contact(new Vector2(.1f,.5f),new Vector2(.4f,.5f),true),"刀面把干壳当成了软色。");
            if(tapReview)
            {
                yield return TapPalette(flow,reviewMouse,new Vector2(.78f,.5f),AtelierFlowRules.WetLearned);
            }
            else Check(palette.Contact(new Vector2(.65f,.5f),new Vector2(.89f,.5f),true),"有效刀面接触没有推色。");
            Check(flow.Session.State.HasFact(AtelierFlowRules.WetLearned),"刀面推色未记录经验。");
            Check(!flow.Session.State.Knows(StudioRules.Ceiling) && !flow.Session.State.Knows(StudioRules.Room),"刀具教学代领了屋顶或房间。");
            yield return Shot("23_刀面推开右侧软色");
            if(reviewMouse!=null) InputSystem.RemoveDevice(reviewMouse);
            flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.5f);
            Check(canvas.CanFinish && refs.ContextAction.interactable,"两面都完成后完成按钮仍未开放。");
            for(int n=0;n<2;n++) Check(canvas.Commit(new Vector2(.35f+n*.30f,.40f),20,0).Accepted,"第三层余下合法颜料无法用尽。");
            Check(canvas.Document.Current.Count==5 && canvas.Document.Current.RemainingPaint==0,"夜城没有按五笔用尽预算。");
            Check(canvas.Document.Layers[0].Count==3 && canvas.Document.Layers[1].Count==5,"第三层覆盖掉了前两层账本。");
            yield return Shot("24_卷涡柏影夜城真实叠层");
            refs.ContextAction.onClick.Invoke();yield return new WaitForSeconds(2);
            Check(flow.Session.State.HasFact(AtelierFlowRules.Finished(3)) && flow.Voice.ActiveKey=="N05","第三层没有主动进入N05。");
            Check(flow.IsStageTransition && flow.Draft.WeightForStage(4)==0 && flow.Draft.WeightForStage(0)==0,"N05期间提前显色或揭屋顶。");
            yield return Shot("25_N05期间仍遮住屋顶与点灯区");
            float deadline=Time.realtimeSinceStartup+90;
            while(!flow.GameplayInputEnabled && Time.realtimeSinceStartup<deadline) yield return null;
            Check(flow.GameplayInputEnabled && flow.Draft.WeightForStage(0)==0,"N05交接未结束或提前显露屋顶。");
            Check(!flow.Session.State.Knows(AtelierFlowRules.Brush(4)) && !flow.Session.State.CanvasLocked,"第三层结束凭空送了暖星或锁了画布。");
        }
        /// <summary>通过当前Player的InputSystem鼠标事件走真正的按下、松开与世界射线路径。</summary>
        private IEnumerator TapPalette(AtelierFlowGameplay flow,Mouse mouse,Vector2 uv,FactId fact)
        {
            var b=flow.StageThree.Palette.Surface.bounds;
            var world=new Vector3(Mathf.Lerp(b.min.x,b.max.x,uv.x),b.max.y+.001f,Mathf.Lerp(b.min.z,b.max.z,uv.y));
            var screen=flow.References.ViewCamera.WorldToScreenPoint(world);
            for(int n=0;n<3;n++)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(screen.x,screen.y),buttons=1});
                yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(screen.x,screen.y)});
                yield return null;yield return null;
                if(n<2) Check(!flow.Session.State.HasFact(fact),"不足三次辅助短点就完成了工具经验。");
            }
        }
    }
}
