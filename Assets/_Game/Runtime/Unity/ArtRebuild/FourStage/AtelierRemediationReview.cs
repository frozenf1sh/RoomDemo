// 文件：AtelierRemediationReview.cs
// 职责：仅在显式调用或构建参数下回放本轮正常命令，采集UI与双路线运行画面。
// 边界：不是真人鼠标验收；不调试授予、不替换正式作品，也不在普通运行自动执行。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>独立构建和Editor共用的明确取证入口。</summary>
    public sealed class AtelierRemediationReview : MonoBehaviour
    {
        private string output;
        private bool ordinary;
        private readonly List<string> errors=new List<string>();
        private AtelierFlowGameplay flow;
        private float started;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if(UnityEngine.Application.isEditor)return;
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--atelier-remediation-output");
            if(i<0||i+1>=args.Length)return;
            var review=new GameObject("整改流程显式运行取证").AddComponent<AtelierRemediationReview>();
            review.output=Path.GetFullPath(args[i+1]);review.ordinary=args.Contains("--atelier-remediation-normal");
        }
        /// <summary>只允许主动发起，不修改场景或领域初始状态。</summary>
        public static void StartEditor(string directory,bool normal=false)
        {
            var review=new GameObject("整改流程Editor取证").AddComponent<AtelierRemediationReview>();review.output=Path.GetFullPath(directory);review.ordinary=normal;
        }
        private void OnEnable()=>UnityEngine.Application.logMessageReceived+=Log;
        private void OnDestroy()=>UnityEngine.Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(text+"\n"+stack);}
        private void Check(bool value,string message){if(!value)errors.Add(message);}
        private void Update(){if(started>0&&Time.realtimeSinceStartup-started>360){errors.Add("运行取证超时。");Finish();}}
        private IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);Time.timeScale=4;UnityEngine.Application.runInBackground=true;
            yield return null;flow=FindAnyObjectByType<AtelierFlowGameplay>();
            yield return Ready();
            var notebook=flow.References.Notebook;
            notebook.SetOpen(true);yield return new WaitForSecondsRealtime(.35f);
            var help=notebook.GetComponent<AtelierNotebookHelp>();Check(help!=null&&help.IsVisible,"首次帮助未出现。");
            yield return Shot("01_首开帮助_材料");
            foreach(var button in help.GetComponentsInChildren<Button>(true).Where(x=>x.name=="下一步").ToArray())button.onClick.Invoke();
            yield return Shot("02_帮助_两词与单击归还");
            var next=help.GetComponentsInChildren<Button>(true).First(x=>x.name=="下一步");next.onClick.Invoke();next.onClick.Invoke();
            yield return Shot("03_帮助_笔刷收藏入口");help.CloseFromBack();notebook.SetOpen(false);yield return Ready();
            var g=flow.Session;
            g.Inspect(StudioRules.Cup);g.Inspect(AtelierRules.Brush);g.PerformAction(StudioRules.Cup,"wash_release_pigment");g.Inspect(AtelierFlowRules.Crayon);
            yield return Compose(AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue"),"04_合成按钮白字与结果");
            var paper=notebook.CombinationCanvas;
            paper.Add(AtelierFlowRules.C("flow"),new Vector2(-100,0));paper.Add(AtelierFlowRules.C("free"),new Vector2(100,0));
            yield return new WaitForSecondsRealtime(1.9f);Check(paper.ActiveCardCount==2,"旧预览计时误删下一组输入。");
            var card=paper.Workspace.GetComponentsInChildren<Button>().First(x=>x.name.StartsWith("CanvasClue_"));card.onClick.Invoke();
            Check(paper.ActiveCardCount==1,"右页单击没有归还参与词。");
            yield return Compose(AtelierFlowRules.C("flow"),AtelierFlowRules.C("free"));
            yield return Compose(new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl"),"05_紫色卷涡与蓝词退场");
            yield return PaintLayer();Check(flow.CanvasPort.Finish().Accepted,"第一层不能完成。");yield return Ready();
            foreach(var source in new[] {AtelierFlowRules.Plaster,AtelierFlowRules.HandMirror,AtelierFlowRules.DryBrush,AtelierFlowRules.MirrorShards})g.Inspect(source);
            g.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");
            yield return Compose(AtelierFlowRules.C("fixed"),AtelierFlowRules.C("flip"));
            yield return Compose(AtelierFlowRules.C("black"),AtelierFlowRules.C("sharp"));
            yield return Compose(new ClueId("atelier.flow.twisted"),new ClueId("atelier.flow.rough"));
            yield return PaintLayer();Check(flow.CanvasPort.Finish().Accepted,"第二层不能完成。");yield return Ready();
            flow.Observe(Target(AtelierTargetKind.DryPalette));yield return Ready();flow.Observe(Target(AtelierTargetKind.PaletteKnife));yield return Ready();
            var palette=flow.StageThree.Palette;
            palette.Contact(new Vector2(.12f,.5f),new Vector2(.44f,.5f),false);
            palette.Contact(new Vector2(.64f,.5f),new Vector2(.94f,.5f),true);
            Check(g.State.HasFact(AtelierFlowRules.DryLearned)&&g.State.HasFact(AtelierFlowRules.WetLearned),"自动干湿接触未成立。");
            Check(!flow.StageThree.Panel.activeSelf,"第三阶段旧世界面板仍可见。");yield return Shot("06_自动刀姿与干湿板面");
            flow.ReturnToRoom();yield return Ready();flow.Observe(Target(AtelierTargetKind.Window));yield return Ready();
            flow.StageThree.ObserveWindowDetail(true);flow.StageThree.ObserveWindowDetail(false);
            yield return Shot("07_近远窗灯无比较面板");
            yield return Compose(new ClueId(AtelierFlowRules.NearWindowLight.Value),new ClueId(AtelierFlowRules.FarWindowLight.Value));
            yield return Compose(AtelierFlowRules.C("window_lights"),new ClueId(StudioRules.Window.Value));
            yield return PaintLayer();Check(flow.CanvasPort.Finish().Accepted,"第三层不能完成。");yield return Ready();
            flow.Observe(Target(AtelierTargetKind.OilLamp));yield return Ready();
            flow.Observe(Target(AtelierTargetKind.Matches));flow.Observe(Target(AtelierTargetKind.MatchStriker));
            flow.References.Lamp.HoldAtWick();g.PerformAction(StudioRules.LampBody,"light_wick");
            // 打字演出按真实帧推进，等待实际解锁，不能用固定秒数跳过旁白。
            float roofLimit=Time.realtimeSinceStartup+75;
            bool sawN06=false;
            while((!flow.StageFour.Allowed(flow.StageFour.RoofTarget)||flow.Draft.WeightForStage(0)<.98f)&&Time.realtimeSinceStartup<roofLimit)
            {
                if(!sawN06&&flow.Voice.ActiveKey=="N06")
                {sawN06=true;Check(flow.Draft.WeightForStage(0)<.001f,"N06播放前屋顶已经提前显色。");}
                yield return null;
            }
            Check(sawN06,"未经过点灯后的N06完整播放。");
            yield return Ready();
            Check(flow.IsRoomView,"点灯后未自动回主镜头。");yield return Shot("08_点灯回镜与屋顶显露");
            if(!ordinary)
            {
                flow.Observe(flow.StageFour.RoofTarget);yield return Ready();
                yield return Compose(AtelierFlowRules.C("dry_paint"),new ClueId(StudioRules.Ceiling.Value));
                yield return CloseNotebook();yield return new WaitForSeconds(1.4f);yield return Shot("09_中央屋顶结壳");
                Check(Tool(AtelierFinaleAction.ProbeRoof).Accepted,"首次屋顶试刮失败。");
                yield return Shot("10_首次刮开普通夜空");
                Check(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted,"第二次屋顶扩刮失败。");
                yield return new WaitForSeconds(7);yield return Ready();yield return Shot("11_湿色落地与全屋显色");
                Check(flow.Draft.Density<.01f,"湿色落地后草稿没有全部退去。");
                var camera=flow.References.ViewCamera;
                var entry=flow.StageFour.RoomEntryTarget;
                var toEntry=entry.transform.position-camera.transform.position;
                var firstHit=Physics.RaycastAll(new Ray(camera.transform.position,toEntry.normalized),40,flow.References.PickingMask,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).First();
                Check(firstHit.collider.GetComponent<AtelierInteractionTarget>()==entry,"桌面湿色被前景物体挡住，主视角无法点击。");
                flow.Observe(entry);yield return Ready();
                var floorHit=Physics.RaycastAll(new Ray(camera.transform.position,camera.transform.forward),40,flow.References.PickingMask,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).First();
                Check(floorHit.collider.GetComponent<AtelierInteractionTarget>()==flow.StageFour.RoomTarget,"地板湿色近景被其他表面挡住。");
                yield return Compose(AtelierFlowRules.C("wet_paint"),new ClueId(StudioRules.Room.Value));
                yield return CloseNotebook();Check(!Tool(AtelierFinaleAction.EraseRoom).Accepted,"未画第四层就擦开房间。");
            }
            yield return Compose(AtelierFlowRules.C("warm"),AtelierFlowRules.C("bright"));yield return PaintLayer();
            if(ordinary)
            {
                Check(Tool(AtelierFinaleAction.SpreadCanvas).Accepted,"刀面收色失败。");Check(flow.CanvasPort.Finish().Accepted,"普通定稿失败。");
            }
            else
            {
                flow.ReturnToRoom();yield return Ready();flow.Observe(flow.StageFour.RoomTarget);yield return Ready();
                Check(Tool(AtelierFinaleAction.EraseRoom).Accepted,"已解释的湿房间未能推开。");
                yield return new WaitForSeconds(7);yield return Ready();yield return Shot("12_室内退去_旧画暂留");
                flow.Observe(Target(AtelierTargetKind.Painting));yield return Shot("13_主角拒绝继续旧画");Check(!flow.IsCanvasFocused,"旧画拒绝后仍进入Workspace。");
                flow.Observe(flow.StageFour.SkyTarget);yield return Shot("14_天空像画布的想法");
                yield return Compose(new ClueId(StudioRules.Sky.Value),new ClueId(AtelierRules.Painting.Value));
                yield return CloseNotebook();flow.Observe(flow.StageFour.SkyTarget);yield return Ready();
                foreach(var point in new[]{new Vector3(.05f,.95f,0),new Vector3(.5f,.9f,0),new Vector3(.95f,.95f,0),new Vector3(.5f,.3f,0)})
                    Check(flow.StageFour.SkyTarget.GetComponent<Collider>().Raycast(flow.References.ViewCamera.ViewportPointToRay(point),out _,40),"天空边缘不能被正常拾取："+point);
                Check(flow.StageFour.PaintSky().Accepted,"天空云的一次点击失败。");yield return new WaitForSeconds(2);
                Check(flow.References.Finale.Sky.Flow<.001f,"只有云时天空提前流动。");yield return Shot("15_单击画云_天空仍静止");
                Check(flow.StageFour.PaintSky().Accepted,"天空星的一次点击失败。");
            }
            float limit=Time.realtimeSinceStartup+40;
            if(!ordinary)
            {
                while(!flow.References.Finale.Finished&&Time.realtimeSinceStartup<limit)yield return null;
                yield return Shot("16a_全景退去与流动星夜");
            }
            while(!flow.StageFour.MenuVisible&&Time.realtimeSinceStartup<limit)yield return null;
            Check(flow.StageFour.MenuVisible,"最终结尾没有完成。");yield return Shot(ordinary?"16_普通结尾":"16_现实路线结尾");
            Finish();
        }
        private IEnumerator Ready()
        {
            float limit=Time.realtimeSinceStartup+65;
            while((flow.IsOpening||flow.IsStageTransition||flow.CameraMoving||!flow.GameplayInputEnabled)&&Time.realtimeSinceStartup<limit)yield return null;
            yield return null;yield return null;
        }
        private AtelierInteractionTarget Target(AtelierTargetKind kind)=>FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include).First(x=>x.Kind==kind);
        private IEnumerator Compose(ClueId first,ClueId second,string shot="")
        {
            var notebook=flow.References.Notebook;
            if(!notebook.IsOpen){notebook.SetOpen(true);yield return new WaitForSecondsRealtime(.3f);}
            notebook.GetComponent<AtelierNotebookHelp>()?.CloseFromBack();var paper=notebook.CombinationCanvas;
            foreach(var old in paper.CombinationInput.ToArray())paper.Remove(old);
            Check(paper.Add(first,new Vector2(-100,20))!=null,"无法放入材料："+first.Value);
            Check(paper.Add(second,new Vector2(100,20))!=null,"无法放入材料："+second.Value);
            Check(paper.SubmitButton.GetComponentInChildren<Text>().color==Color.white,"合成按钮没有纯白文字。");
            if(shot=="04_合成按钮白字与结果")yield return Shot("04a_纯白合成按钮");
            notebook.InterpretSelected();Check(paper.ActiveCardCount==0&&paper.HasResultPreview,"成功结果仍占参与词位置。");
            if(shot!="")yield return Shot(shot);
        }
        private IEnumerator CloseNotebook(){flow.References.Notebook.SetOpen(false);yield return Ready();}
        private IEnumerator PaintLayer()
        {
            yield return CloseNotebook();flow.ReturnToRoom();yield return Ready();flow.Observe(Target(AtelierTargetKind.Painting));yield return Ready();
            foreach(var point in new[] {new Vector2(.30f,.58f),new Vector2(.64f,.48f),new Vector2(.48f,.73f)})
                Check(flow.CanvasPort.Commit(point,flow.CanvasPort.DefaultCost,0).Accepted,"真实画布落笔失败。");
        }
        private AtelierCanvasResult Tool(AtelierFinaleAction action,int a=4200,int b=5500)
            =>flow.Session.ApplyAtelierFinale(flow.Session.SessionEpoch,flow.CanvasPort.Document.Revision,action,new[] {new AtelierContactSegment(a,5000,b,5000)});
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
            File.WriteAllText(Path.Combine(output,"进度.txt"),name);yield return null;
        }
        private void Finish()
        {
            if(started<0)return;started=-1;
            File.WriteAllText(Path.Combine(output,"运行结果.txt"),"显式正常命令与UI事件回放，4倍速度；不是人工鼠标验收。\n错误数："+errors.Count+"\n"+string.Join("\n",errors));
            if(!UnityEngine.Application.isEditor)UnityEngine.Application.Quit(errors.Count==0?0:1);
        }
    }
}
