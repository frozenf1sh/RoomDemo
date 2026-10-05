// =============================================================================
// 文件：NarrativeReviewDriver.cs
// 职责：通过明确开发入口从新游戏走一次亲点、近景热点、拖词、跨日和最终世界。
// 边界：只调用正式输入适配器；不授予调试线索、添加事实或加载预设。
//       自动走查不是陌生玩家真人试玩，不据此宣称时长、理解度或情绪验收。
// =============================================================================
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>一次简短叙事路线走查；正常启动不会运行、占据 HUD 或启用快捷键。</summary>
    public sealed class NarrativeReviewDriver : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private InteractionSystem interaction;
        [SerializeField] private DayTransitionController day;
        [SerializeField] private StarryWorldSequence sequence;
        [SerializeField] private RoomModules room;
        [SerializeField] private WorldExterior exterior;
        private string output;
        private bool quit, running, compliant, capture;
        private int checks;
        private string steps="";
        /// <summary>编辑工具显式绑定已有玩家入口，不创建另一条解谜通道。</summary>
        public void Configure(GameBootstrap game, NotebookView notes, InspectionController details, InteractionSystem input,
            DayTransitionController days, StarryWorldSequence director, RoomModules architecture, WorldExterior landscape)
        { bootstrap=game; notebook=notes; inspection=details; interaction=input; day=days; sequence=director; room=architecture; exterior=landscape; }
        private void Start()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-roomDemoNarrativeReview");
            if(index<0) return;
            quit=Array.IndexOf(args,"-roomDemoQuitAfterReview")>=0;
            Begin(index+1<args.Length ? args[index+1] : Path.Combine(UnityEngine.Application.persistentDataPath,"NarrativeReview"),Array.IndexOf(args,"-roomDemoCanvasRoute")>=0,Array.IndexOf(args,"-roomDemoNarrativeCapture")>=0);
        }
        /// <summary>明确开发请求才开始；输出几张实际截图和简短操作记录。</summary>
        public void Begin(string folder, bool canvasRoute = false, bool record = false)
        {
            if(running) return;
            compliant=canvasRoute; capture=record; output=folder; Directory.CreateDirectory(output); running=true; checks=0; steps="";
            // 仅隔离这次自动输入，不修改项目的 Input Actions 或正式键位。
            if(Mouse.current!=null) InputSystem.DisableDevice(Mouse.current);
            if(Keyboard.current!=null) InputSystem.DisableDevice(Keyboard.current);
            File.WriteAllText(Path.Combine(output,"状态.txt"),"正在从新游戏走查，不使用调试预设。\n");
            StartCoroutine(Run());
        }
        private IEnumerator Run()
        {
            bootstrap.Restart(); yield return Wait(4);
            Require(!bootstrap.Session.State.Knows(DemoRules.Clue("room")),"房间不是开局词条");
            yield return Capture("初始房间");
            Require(RoomClick(DemoRules.Plant),"亲点盆栽进入近景"); yield return Wait(.9f);
            Require(!bootstrap.Session.State.Knows(DemoRules.Clue("growth")),"点击盆栽不会直接奖励生长");
            for(int i=0;i<12;i++){inspection.Rotate(new Vector2(15/.35f,0));yield return Wait(.04f);}
            Require(Hotspot("sprout"),"转到背面亲点新芽"); inspection.Exit(); yield return Wait(.5f);
            Require(RoomClick(DemoRules.Book),"亲点书本进入近景"); yield return Wait(.9f);
            Require(Hotspot("cover"),"点封面打开书"); yield return Wait(.85f);
            Require(Hotspot("next1"),"点击页角翻页"); yield return Wait(.85f);
            Require(Hotspot("quote"),"亲点向光短句"); inspection.Exit(); yield return Wait(.5f);
            yield return Combine(ExplorationRules.Sprout,ExplorationRules.TowardLight,"联想");
            Require(bootstrap.Session.State.Knows(DemoRules.Clue("growth")),"观察形成生长概念");
            yield return Capture("灵感_生长");
            Require(RoomClick(DemoRules.Painting),"亲点画架获得画");
            yield return Combine(DemoRules.Clue("growth"),DemoRules.Clue(compliant?"painting":"plant"),"解释"); yield return Wait(6);
            if(!compliant) yield return Capture("旁白_那不是画布");
            Require(bootstrap.Session.State.DeviationCount==(compliant?0:1),"只有首次正式解释记入叙事倾向");
            Require(!bootstrap.Session.State.HasFact(DemoRules.BookFallen),"当天书尚未落下");
            EndDay(); yield return Wait(3);
            Require(bootstrap.Session.State.DayIndex==2 && bootstrap.Session.State.HasFact(DemoRules.BookFallen)==!compliant,"第二天共享探索入口，只有现实生长会碰落书");
            Require(RoomClick(DemoRules.Book),compliant?"亲点仍在书架的书本":"亲点落地书本"); yield return Wait(.9f);
            Require(Hotspot("cover"),"再次打开书"); yield return Wait(.85f);
            Require(Hotspot("next1"),"翻到第二页"); yield return Wait(.85f);
            Require(Hotspot("next2"),"翻到地平线页"); yield return Wait(.85f);
            Require(Hotspot("horizon"),"亲点地平线获得远方"); inspection.Exit(); yield return Wait(.5f);
            Require(!bootstrap.Session.State.Knows(DemoRules.Clue("window")),"书不会代替玩家授予窗户");
            Require(RoomClick(DemoRules.Window),"亲点窗户");
            yield return Combine(DemoRules.Clue("far"),DemoRules.Clue(compliant?"painting":"window"),"解释"); yield return Wait(3.5f);
            Require(RoomClick(DemoRules.Paper),compliant?"亲点画架显露的纸张获得海":"亲点被风吹出的纸张获得海");
            Require(bootstrap.Session.State.Knows(DemoRules.Clue("painting")),"画只能由之前亲点画架获得");
            Require(RoomClick(DemoRules.Painting),"亲点画");
            yield return Combine(DemoRules.Clue("sea"),DemoRules.Clue("painting"),"解释"); yield return Wait(17.5f);
            Require(bootstrap.Session.State.CanvasLocked==compliant,"三次连续服从且从未偏离才收束路线");
            Require(bootstrap.Session.State.HasFact(DemoRules.WaterPresent)==!compliant,"画里的海只在旧现实解释存在时外溢");
            EndDay(); yield return Wait(3);
            Require(bootstrap.Session.State.DayIndex==3,"明确结束第二天进入第三天");
            Require(RoomClick(MainlineRules.Ceiling),"亲点天花板获得夜色");
            Require(RoomClick(DemoRules.Window),"亲点窗外获得无边");
            yield return Combine(MainlineRules.Night,MainlineRules.Boundless,"联想");
            yield return Combine(MainlineRules.Starry,DemoRules.Clue(compliant?"painting":"ceiling"),"解释"); yield return Wait(6.5f);
            if(compliant)
            {
                Require(bootstrap.Session.State.HasFact(NarrativeRules.BanalComplete),"服从路线画作完成");
                Require(room.Ceiling.gameObject.activeSelf && room.WallBack.gameObject.activeSelf && !exterior.Environment.activeSelf,"房间建筑保持原样");
                Require(!bootstrap.Session.State.HasFact(DemoRules.PlantGrown) && !bootstrap.Session.State.HasFact(DemoRules.WindowFar),"画内枝叶与远方没有修改现实");
                Face(bootstrap.transform.root.GetComponentsInChildren<SceneEntity>(true).Single(x=>x.Id==DemoRules.Painting).transform.position);
                yield return Capture("画布路线_完成的普通作品");
                yield return Wait(11);
                Require(FindAnyObjectByType<CanvasEndingSequence>().IsFinished,"旁白之后安静淡出到完成");
                yield return Capture("画布路线_结束");
                WriteResult(); yield break;
            }
            Require(!room.Ceiling.gameObject.activeSelf && room.WallBack.gameObject.activeSelf && !exterior.Environment.activeSelf,"星空只打开屋顶，森林尚未出现");
            yield return Capture("屋顶星空_墙仍保留");
            Require(RoomClick(DemoRules.Window),"重新亲点窗外获得树影");
            Require(!bootstrap.Session.State.Knows(DemoRules.Clue("room")),"屋顶打开没有自动把房间送进笔记");
            Require(RoomClick(MainlineRules.Room),"亲点脚下房间，第一次获得房间与风声");
            Require(bootstrap.Session.State.Knows(DemoRules.Clue("room")),"房间本身成为可解释之物");
            yield return Combine(MainlineRules.TreeShadow,MainlineRules.WindSound,"联想");
            yield return Combine(MainlineRules.Forest,DemoRules.Clue("room"),"解释"); yield return Wait(8.2f);
            Require(exterior.Environment.activeSelf && FindObjectsByType<CampfireVisual>().Length==0,"树林先进入，营火尚未生成");
            yield return Wait(10.2f);
            Require(bootstrap.Session.State.HasFact(MainlineRules.Complete) && FindObjectsByType<CampfireVisual>().Length==1,"最后营火出现并完成主线");
            Require(!notebook.IsOpen && !room.WallBack.gameObject.activeSelf,"最后返回干净玩家画面，房间边界退去");
            // 回到初始朝向比较房间和最终世界；只使用受限转头入口。
            Face(new Vector3(0,2,3)); yield return Wait(.4f); yield return Capture("森林营地_最终玩家画面");
            if(capture) yield return Wait(1);
            WriteResult();
        }
        private void WriteResult()
        {
            File.WriteAllText(Path.Combine(output,"状态.txt"),"通过："+checks+" 项实际输入适配器操作。\n路线："+(compliant?"画布完成":"现实展开")+"。从新游戏开始，未使用调试预设、授予或手工改状态。\n自动走查不证明陌生玩家情绪与理解度。\n"+steps);
            Finish(); if(quit) UnityEngine.Application.Quit();
        }
        private IEnumerator Combine(ClueId a,ClueId b,string label)
        {
            notebook.SetOpen(true); yield return Wait(.3f); Canvas.ForceUpdateCanvases();
            Drag(a,new Vector2(-88,30)); Drag(b,new Vector2(88,30)); yield return Wait(.4f);
            var action=notebook.GetComponentsInChildren<Button>(true).Single(x=>x.name=="CanvasAction");
            Require(action.gameObject.activeInHierarchy && action.GetComponentInChildren<Text>().text==label,"相邻词条出现"+label+"入口："+a.Value+" + "+b.Value);
            if(a==ExplorationRules.Sprout)
            {
                Drag(DemoRules.Clue("book"),new Vector2(0,-50));
                Require(action.gameObject.activeInHierarchy,"三词靠近也允许尝试，不预告答案");
                var card=notebook.GetComponentsInChildren<Button>(true).Single(x=>x.name=="CanvasClue_book"); Click(card);
                Require(action.gameObject.activeInHierarchy,"归还第三词后恢复两词联想");
                yield return Capture("笔记本V2_拖动与淡线");
            }
            if(capture && a==MainlineRules.Starry && !compliant) StartCoroutine(Record(7,"屋顶"));
            if(capture && a==MainlineRules.Forest && !compliant)
            { Face(new Vector3(0,2,3)); StartCoroutine(Record(20,"森林")); }
            Click(action); yield return Wait(label=="联想" ? 2.1f : 1.1f);
        }
        private void Drag(ClueId clue,Vector2 position)
        {
            var row=notebook.GetComponentsInChildren<NotebookClueDrag>().Single(x=>!x.name.StartsWith("CanvasClue_") && x.name.EndsWith("_"+clue.Value));
            var canvas=notebook.GetComponentInChildren<NotebookCanvasView>(true).GetComponent<RectTransform>();
            var from=RectTransformUtility.WorldToScreenPoint(null,row.transform.position);
            var to=RectTransformUtility.WorldToScreenPoint(null,canvas.TransformPoint(position));
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=from};
            ExecuteEvents.Execute(row.gameObject,pointer,ExecuteEvents.beginDragHandler);
            pointer.position=to; ExecuteEvents.Execute(row.gameObject,pointer,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(row.gameObject,pointer,ExecuteEvents.endDragHandler);
        }
        private void EndDay()
        {
            var hud=FindAnyObjectByType<PlayerHudView>(); hud.SetPaused(true);
            var button=hud.GetComponentsInChildren<Button>(true).Single(x=>x.gameObject.activeInHierarchy && x.GetComponentInChildren<Text>(true)?.text=="结束今天");
            Click(button);
        }
        private static void Click(Button button)
        { ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler); }
        private bool RoomClick(ObjectId id)
        {
            var entity=bootstrap.transform.root.GetComponentsInChildren<SceneEntity>(true).Single(x=>x.Id==id);
            var center=entity.GetComponent<Collider>().bounds.center; Face(center); Physics.SyncTransforms();
            var point=Camera.main.WorldToScreenPoint(center);
            return point.z>0 && interaction.InspectAt(point);
        }
        private void Face(Vector3 point)
        {
            var camera=Camera.main; var target=Quaternion.LookRotation(point-camera.transform.position).eulerAngles;
            var current=camera.transform.eulerAngles;
            camera.GetComponentInParent<FixedLook>().ApplyLook(new Vector2(Mathf.DeltaAngle(current.y,target.y)/.12f,-Mathf.DeltaAngle(current.x,target.x)/.12f));
        }
        private bool Hotspot(string id)
        {
            Physics.SyncTransforms(); var hotspot=inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(true).Single(x=>x.ActionId==id);
            return inspection.ObserveAt(Camera.main.WorldToScreenPoint(hotspot.transform.position));
        }
        private static WaitForSecondsRealtime Wait(float seconds)=>new WaitForSecondsRealtime(seconds);
        /// <summary>仅显式录像参数取样真实构建画面，不记录音轨，也不用于性能验收。</summary>
        private IEnumerator Record(float seconds,string beat)
        {
            string folder=Path.Combine(output,beat+"帧"); Directory.CreateDirectory(folder);
            float started=Time.unscaledTime, next=0;
            int index=0;
            while(Time.unscaledTime-started<seconds)
            {
                yield return new WaitForEndOfFrame();
                if(Time.unscaledTime-started<next) continue;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frame_"+(index++).ToString("D4")+".png"));
                next+=.2f;
            }
        }
        private IEnumerator Capture(string label)
        { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,label+".png")); yield return Wait(.2f); }
        private void Require(bool success,string description)
        {
            if(success) {checks++;steps+="✓ "+description+"\n";return;}
            File.WriteAllText(Path.Combine(output,"状态.txt"),"失败："+description+"\n"+steps);Finish();
            throw new InvalidOperationException("叙事路线走查失败："+description);
        }
        private void Finish()
        {
            running=false;
            if(Mouse.current!=null && !Mouse.current.enabled) InputSystem.EnableDevice(Mouse.current);
            if(Keyboard.current!=null && !Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
        }
        private void OnDestroy() { if(running) Finish(); }
    }
}
