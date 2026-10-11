// =============================================================================
// 文件：AtelierRepairReview.cs
// 职责：明确命令行开关下复核画布分层、纸面点击适配和拉帘表现，保存独立包真实画面。
// 边界：这是调试命令回放，不是真人输入证据；普通运行不创建对象，不替玩家收集事实。
// =============================================================================
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Unity.ArtRebuild
{
    /// <summary>仅用于本阶段独立构建复核；不会接入正式入口或提供玩家快捷键。</summary>
    public sealed class AtelierRepairReview : MonoBehaviour
    {
        private AtelierGameplay game;
        private string folder;
        private int errors;
        private Vector3[] clothSize;
        private string report = "模式=显式调试命令及 UI 事件回放；不证明原生鼠标送达\n";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();int key=Array.IndexOf(args,"--atelier-repair-review");
            if(key<0||key+1>=args.Length)return;
            var owner=new GameObject("显式画布修复复核");DontDestroyOnLoad(owner);
            owner.AddComponent<AtelierRepairReview>().folder=Path.GetFullPath(args[key+1]);
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);UnityEngine.Application.runInBackground=true;
            UnityEngine.Application.logMessageReceived+=OnLog;
            yield return new WaitForSeconds(5);
            game=FindAnyObjectByType<AtelierGameplay>();
            yield return Save("01_空白亚麻与正常夜空");
            Observe(AtelierTargetKind.Brush);yield return new WaitForSeconds(2);game.ReturnToRoom();yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.WashWater);yield return new WaitForSeconds(2);game.ReturnToRoom();yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Painting);yield return new WaitForSeconds(2);
            Sketch(0,StudioRules.Cup);Sketch(1,AtelierRules.Brush);yield return new WaitForSeconds(3.2f);
            yield return Save("02_杯笔层_没有桌面条带");
            game.ReturnToRoom();yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.StillLamp);Observe(AtelierTargetKind.Window);Observe(AtelierTargetKind.Painting);yield return new WaitForSeconds(2);
            Sketch(2,AtelierRules.StillLamp);Sketch(3,StudioRules.Window);yield return new WaitForSeconds(4.6f);
            yield return Save("03_完整静物画");
            game.ReturnToRoom();yield return new WaitForSeconds(2);Observe(AtelierTargetKind.WashWater);yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Brush);yield return new WaitForSeconds(5);Observe(AtelierTargetKind.WashWater);
            game.Notebook.SetOpen(true);yield return null;
            Place(AtelierRules.Observation("still"),-95);Place(AtelierRules.Observation("spread"),95);
            yield return Save("04_点击选词及纸面放置_事件回放");
            game.Notebook.InterpretSelected();yield return new WaitForSeconds(1);game.Notebook.SetOpen(false);
            Require(game.Session.State.Knows(StudioRules.Flow),"纸面显式提交形成流动");
            game.ReturnToRoom();yield return new WaitForSeconds(2);Observe(AtelierTargetKind.Painting);yield return new WaitForSeconds(2);
            game.Observe(Target(AtelierTargetKind.Painting),game.Painting.WaterUV);
            game.Notebook.SetOpen(true);yield return null;Place(new ClueId(StudioRules.PictureWater.Value),-95);Place(new ClueId(StudioRules.Flow.Value),95);
            game.Notebook.InterpretSelected();yield return new WaitForSeconds(1);game.Notebook.SetOpen(false);yield return new WaitForSeconds(3);
            yield return Save("05_画中水纹_A");yield return new WaitForSeconds(3);yield return Save("06_画中水纹_B_相隔三秒");
            // 本次新演出从实际窗帘适配入口开始；不把点灯段的调试命令称为玩家游玩。
            game.ReturnToRoom();yield return new WaitForSeconds(2);Observe(AtelierTargetKind.Curtain);yield return new WaitForSeconds(2);
            clothSize=game.Curtain.Cloth.Select(x=>x.sharedMesh.bounds.size).ToArray();
            yield return Save("07_拉帘前_约束与接点");
            Observe(AtelierTargetKind.CurtainCord);yield return new WaitForSeconds(1.5f);yield return Save("08_拉帘中_上缘先行");
            Require(!game.Session.State.Knows(AtelierRules.Observation("revealed")),"拉绳不会自动奖励拉开后的观察");
            yield return new WaitForSeconds(4);Observe(AtelierTargetKind.Curtain);yield return Save("09_拉帘后_布褶落稳");
            CheckMeshes();
            game.Notebook.SetOpen(true);yield return null;Place(AtelierRules.Observation("hidden"),-95);Place(AtelierRules.Observation("revealed"),95);
            game.Notebook.InterpretSelected();yield return new WaitForSeconds(1);game.Notebook.SetOpen(false);
            Require(game.Session.State.Knows(StudioRules.Open),"比较前后形成打开");
            game.ReturnToRoom();yield return new WaitForSeconds(2);Observe(AtelierTargetKind.Painting);yield return new WaitForSeconds(2);
            game.Observe(Target(AtelierTargetKind.Painting),game.Painting.WindowUV);
            Require(game.Session.State.Knows(StudioRules.PictureWindow),"画中窗必须经过单独观察入口");
            game.Notebook.SetOpen(true);yield return null;Place(new ClueId(StudioRules.PictureWindow.Value),-95);Place(new ClueId(StudioRules.Open.Value),95);
            game.Notebook.InterpretSelected();yield return new WaitForSeconds(1);game.Notebook.SetOpen(false);yield return new WaitForSeconds(4);
            yield return Save("10_画中窗边补笔");
            Require(game.Session.State.HasFact(StudioRules.PictureOpen),"画中窗解释成功");
            Screen.SetResolution(1024,640,FullScreenMode.Windowed);yield return new WaitForSeconds(2);game.Notebook.SetOpen(true);yield return new WaitForSeconds(1);
            yield return Save("11_小窗口中文纸面");
            report+="运行时错误="+errors+"\n";File.WriteAllText(Path.Combine(folder,"复核记录.txt"),report);
            Debug.Log("画布与拉帘调试复核结束。\n"+report);UnityEngine.Application.Quit();
        }
        private AtelierInteractionTarget Target(AtelierTargetKind kind)=>FindObjectsByType<AtelierInteractionTarget>().First(x=>x.Kind==kind);
        private void Observe(AtelierTargetKind kind)=>game.Observe(Target(kind),Vector2.zero);
        private void Sketch(int index,ObjectId source)
        {
            game.Painting.SelectMotif(index);
            var inputs=source==AtelierRules.Brush?new[]{new ClueId(AtelierRules.Painting.Value),new ClueId(source.Value)}:new[]{new ClueId(AtelierRules.Painting.Value),new ClueId(source.Value),new ClueId(AtelierRules.Brush.Value)};
            game.Session.Combine(inputs);
        }
        private void Place(ClueId clue,float x)
        {
            var canvas=game.Notebook.GetComponentInChildren<NotebookCanvasView>(true);
            var row=game.Notebook.GetComponentsInChildren<Button>(true).First(b=>b.name.EndsWith("_"+clue.Value)&&!b.name.StartsWith("CanvasClue_"));
            int before=canvas.ActiveCardCount,known=game.Session.State.KnownClues.Count(),facts=game.Session.State.Facts.Count();
            // 与 UI 相同的点击处理器，仅验证适配器；原生设备输入仍须另行验收。
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(row.gameObject,data,ExecuteEvents.pointerClickHandler);
            Require(canvas.ActiveCardCount==before,"选词不提前放置："+clue.Value);
            var paper=canvas.GetComponentInChildren<NotebookPaperPlacement>();
            data.position=RectTransformUtility.WorldToScreenPoint(null,paper.transform.TransformPoint(new Vector3(x,30,0)));
            ExecuteEvents.Execute(paper.gameObject,data,ExecuteEvents.pointerClickHandler);
            Require(canvas.ActiveCardCount==before+1,"点击纸面放置："+clue.Value);
            Require(game.Session.State.KnownClues.Count()==known&&game.Session.State.Facts.Count()==facts,"临时纸面操作不写世界事实");
        }
        private void CheckMeshes()
        {
            for(int side=0;side<game.Curtain.Cloth.Length;side++)
            {
                var filter=game.Curtain.Cloth[side];var mesh=filter.sharedMesh;
                Require(mesh.isReadable&&mesh.vertexCount>7000&&mesh.vertices.Length==mesh.vertexCount,"运行时布料可读且不是空数组："+filter.name);
                Require(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"布料顶点有限："+filter.name);
                Require(mesh.bounds.size.x<clothSize[side].x*.88f,"实际网格宽度已收拢："+filter.name);
                Require(Mathf.Abs(mesh.bounds.size.y-clothSize[side].y)<.025f,"下摆保留长度："+filter.name);
                Vector3 destination=filter.GetComponent<Renderer>().bounds.center;
                bool hitCloth=Physics.Raycast(game.ViewCamera.transform.position,(destination-game.ViewCamera.transform.position).normalized,out var hit,20,game.PickingMask,QueryTriggerInteraction.Collide);
                Require(hitCloth&&hit.collider.GetComponent<AtelierInteractionTarget>()?.Kind==AtelierTargetKind.Curtain,"落稳后射线拾取仍命中可见帘布："+filter.name);
                report+="收束后边界="+filter.name+" "+filter.GetComponent<Renderer>().bounds+"\n";
            }
            for(int side=0;side<2;side++)for(int i=1;i<8;i++)Require(Mathf.Abs(game.Curtain.Rings[side*8+i].position.x-game.Curtain.Rings[side*8+i-1].position.x)>.055f,"铜环保持间距");
        }
        private void Require(bool value,string text){report+=(value?"通过=":"失败=")+text+"\n";if(!value)throw new InvalidOperationException("复核失败："+text);}
        private IEnumerator Save(string name)
        {
            yield return null;ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;
            File.WriteAllText(Path.Combine(folder,name+".txt"),"模式=调试命令及 UI 事件回放，非原生输入证明\n事实="+string.Join("、",game.Session.State.Facts.Select(x=>x.Value))+"\n错误="+errors+"\n");
        }
        private void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void OnDestroy(){UnityEngine.Application.logMessageReceived-=OnLog;}
    }
}
