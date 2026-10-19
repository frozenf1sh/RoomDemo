// 文件：AtelierOpeningPlayerReview.cs
// 职责：显式参数下在真实构建回放首批命令和既有按钮回调，截图并记录边界。
// 边界：只为自动取证启用；正常玩家运行不自动操作，不当作真人鼠标验收。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Game.Unity.ArtRebuild
{
    /// <summary>P00至P07的实际Player验证；使用正常会话，禁止调试授予。</summary>
    public sealed class AtelierOpeningPlayerReview : MonoBehaviour
    {
        private string output;
        private readonly List<string> errors=new List<string>();
        private void OnEnable() => UnityEngine.Application.logMessageReceivedThreaded += Collect;
        private void OnDestroy() => UnityEngine.Application.logMessageReceivedThreaded -= Collect;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if(UnityEngine.Application.isEditor) return;
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--atelier-opening-review-output");
            if(at<0||at+1>=args.Length) return;
            var root=new GameObject("首批自动取证_仅显式参数启用");
            root.AddComponent<AtelierOpeningPlayerReview>().output=Path.GetFullPath(args[at+1]);
        }
        private void Check(bool valid,string reason) { if(!valid) lock(errors) errors.Add(reason); }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);UnityEngine.Application.runInBackground=true;
            string[] arguments = Environment.GetCommandLineArgs();
            int speedAt = Array.IndexOf(arguments,"--atelier-review-speed");
            if (speedAt >= 0 && speedAt+1 < arguments.Length && float.TryParse(arguments[speedAt+1],out var speed))
                Time.timeScale = Mathf.Clamp(speed,1,8);
            yield return null;
            var flow=FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null) { File.WriteAllText(Path.Combine(output,"运行结果.txt"),"缺少新流程控制器。");UnityEngine.Application.Quit(1);yield break; }
            var refs=flow.References;
            // 画架视觉属于独立Prefab，不在玩法根节点下；通过已绑定Surface定位自己的Logic。
            AtelierInteractionTarget Target(AtelierTargetKind kind) => kind == AtelierTargetKind.Painting
                ? flow.CanvasPort.Surface.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind)
                : refs.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind);
            yield return Shot("00_黑场旁白");
            Check(flow.IsOpening,"进场没有立即锁定开场输入。");
            Check(flow.Draft.SurroundingsDraft > .99f,"第一帧未建立外围草稿。");
            flow.Observe(Target(AtelierTargetKind.Manuscript));
            flow.Observe(Target(AtelierTargetKind.WashWater));
            flow.ObserveCrayon();
            refs.Reminder.onClick.Invoke();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N,Key.Escape,Key.Space));
            yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            Check(!refs.Notebook.IsOpen && !flow.IsPaused,"开场N/Esc触发了笔记或暂停。");
            Check(!refs.ManuscriptPanel.activeSelf,"开场点击穿透到便签。");
            Check(!refs.Reminder.gameObject.activeSelf,"开场显示了笔记按钮。");
            Check(!flow.Session.State.Knows(StudioRules.Cup) && !flow.Session.State.Knows(AtelierRules.Manuscript)
                && !flow.Session.State.Knows(AtelierFlowRules.Crayon),"开场输入锁仍授予了物件。");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("01_黑场与失焦渐退");
            yield return new WaitForSeconds(4);
            yield return Shot("02_旁白期间草稿遮挡");
            float deadline = Time.realtimeSinceStartup + 70;
            while (flow.IsOpening && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!flow.IsOpening,"完整开场旁白未交回操作。");
            yield return new WaitForSecondsRealtime(.2f);
            Check(flow.GameplayInputEnabled && refs.Reminder.gameObject.activeSelf,"旁白结束后笔记入口未恢复。");
            Check(!flow.Session.State.HasFact(AtelierFlowRules.IntroRead),"旁白结束伪装成已读便签。");
            refs.Notebook.Toggle();
            Check(refs.Notebook.IsOpen,"旁白结束后无法打开笔记。");
            refs.Notebook.Toggle();
            yield return new WaitForSecondsRealtime(.15f);
            yield return Shot("03_第一阶段允许行动");
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            var point = refs.ViewCamera.WorldToScreenPoint(flow.CanvasPort.Surface.bounds.center);
            Color canvasPixel = frame.GetPixel(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.y));
            Color ceilingPixel = frame.GetPixel(Mathf.RoundToInt(frame.width*.9f),Mathf.RoundToInt(frame.height*.9f));
            Check(canvasPixel.b > canvasPixel.r + .06f,"可交互画布被外围底稿误遮挡。");
            Check(Mathf.Abs(ceilingPixel.b-ceilingPixel.r)<.05f,"天花板没有进入外围草稿。");
            Destroy(frame);
            flow.Observe(Target(AtelierTargetKind.WashWater));yield return new WaitForSeconds(1.4f);
            flow.Observe(Target(AtelierTargetKind.Brush));flow.Observe(Target(AtelierTargetKind.WashWater));
            Check(!flow.Session.State.HasFact(AtelierFlowRules.Washed),"开始靠近就错误提交了洗笔事实。");
            yield return new WaitForSeconds(1.7f);
            Check(flow.Session.State.HasFact(AtelierFlowRules.Washed),"真实笔尖接触未提交动作。");
            Check(flow.Session.State.Knows(AtelierFlowRules.C("blue"))&&flow.Session.State.Knows(AtelierFlowRules.C("flow")),"洗笔结果没有同次收录。");
            yield return Shot("04_接触与散色");
            flow.ReturnToRoom();yield return new WaitForSeconds(1.4f);
            var screen=refs.ViewCamera.WorldToScreenPoint(flow.CrayonPicking.bounds.center);
            var ray=refs.ViewCamera.ScreenPointToRay(screen);
            var hits=Physics.RaycastAll(ray,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance).ToArray();
            Check(hits.Length>0&&hits[0].collider==flow.CrayonPicking,"小画主视角射线被其他Collider挡住。");
            flow.ObserveCrayon();yield return new WaitForSeconds(1.4f);
            yield return Shot("04b_正式蜡笔画贴图");
            flow.Session.Combine(new[]{AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue")});
            flow.Session.Combine(new[]{AtelierFlowRules.C("flow"),AtelierFlowRules.C("free")});
            flow.Session.Combine(new[]{new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl")});
            Check(flow.Session.State.Knows(AtelierFlowRules.Brush(1)),"正常配方未形成第一笔刷。");
            Check(!flow.Session.State.CanvasLocked,"第三次概念配方错误触发了旧收束。");
            flow.ReturnToRoom();yield return new WaitForSeconds(1.4f);
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.4f);
            var canvas=flow.CanvasPort;
            var paintingTarget = Target(AtelierTargetKind.Painting);
            var paintingCollider = paintingTarget.GetComponent<Collider>();
            var canvasRay = refs.ViewCamera.ScreenPointToRay(refs.ViewCamera.WorldToScreenPoint(paintingCollider.bounds.center));
            var canvasHits = Physics.RaycastAll(canvasRay,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance).ToArray();
            Check(canvasHits.Length > 0 && canvasHits[0].collider == paintingCollider, "画布近景射线被其他Collider挡住。");
            if (canvasHits.Length > 0) Check(canvasHits[0].textureCoord.sqrMagnitude > .01f, "画布UV拾取无效。");
            flow.SetExternalCanvasWorkspaceActive(true);
            yield return null;
            Check(!refs.ContextAction.gameObject.activeSelf && !refs.ReturnButton.gameObject.activeSelf,"外部Workspace接管后旧画布控件仍在抢输入。");
            flow.SetExternalCanvasWorkspaceActive(false);
            string blank = Hash(canvas.CanvasTexture as RenderTexture);
            Check(!canvas.Commit(new Vector2(0,0),canvas.DefaultCost,0).Accepted,"画外大半笔印未被阻止。");
            Check(canvas.Commit(new Vector2(.3f,.58f),canvas.DefaultCost,0).Accepted,"第一笔未被接受。");
            Check(canvas.Commit(new Vector2(.64f,.48f),canvas.DefaultCost,30).Accepted,"第二笔未被接受。");
            Check(!refs.ContextAction.interactable,"两笔时完成按钮不应可用。");
            Check(!canvas.Finish().Accepted,"两笔时命令不应允许收层。");
            int before=canvas.Document.Current.RemainingPaint;
            Check(canvas.Undo().Accepted,"撤销失败。");Check(canvas.Document.Current.RemainingPaint>before,"撤销没有退款。");
            canvas.Commit(new Vector2(.64f,.48f),canvas.DefaultCost,30);
            canvas.Commit(new Vector2(.48f,.73f),canvas.DefaultCost,330);
            yield return Shot("05_三笔真实作品");
            string artwork=Hash(canvas.CanvasTexture as RenderTexture);
            Check(artwork != blank, "接受三笔后作品纹理仍然为空。");
            flow.ReturnToRoom();yield return new WaitForSeconds(1.4f);
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.4f);
            Check(artwork==Hash(canvas.CanvasTexture as RenderTexture),"返回再进入改变或丢失了作品。");
            Check(refs.ContextAction.interactable,"三笔后完成按钮没有开放。");
            refs.ContextAction.onClick.Invoke();yield return new WaitForSeconds(4.5f);
            Check(flow.Voice.ActiveKey == "N03", "新阶段旁白被过时开场队列拖延。 ");
            Check(!refs.ContextPanel.activeSelf, "回到主视角重新弹出了旧物件说明。");
            Check(flow.Session.State.HasFact(AtelierFlowRules.Finished(1)),"真实完成按钮没有推进首层。");
            Check(canvas.Document.Layers[0].CommittedUnits+canvas.Document.Layers[0].DiscardedUnits==100,"首层颜料账本不守恒。");
            Check(flow.IsStageTransition && !flow.GameplayInputEnabled,"阶段旁白期间提前交回输入。");
            Check(flow.Draft.WeightForStage(2) == 0,"N03没有播完就提前显色。");
            Check(flow.Draft.SurroundingsDraft>.99f && flow.Draft.WeightForStage(0)==0,"阶段推进提前清除了背景或屋顶草稿。");
            yield return Shot("06_N03期间新物件仍为草稿");
            deadline = Time.realtimeSinceStartup+70;
            while (!flow.GameplayInputEnabled && Time.realtimeSinceStartup<deadline) yield return null;
            Check(flow.Draft.WeightForStage(2)>.99f,"N03结束后第二阶段未增量显色。");
            Check(flow.Draft.WeightForStage(1)>.99f && flow.Draft.WeightForStage(0)==0,"第二阶段丢失首阶段显色或误揭屋顶。");
            yield return Shot("07_第二阶段仅新增四类物件");
            var allTargets = FindObjectsByType<AtelierInteractionTarget>();
            AtelierInteractionTarget Second(AtelierTargetKind kind) => allTargets.First(x=>x.Kind==kind);
            foreach (var kind in new[] { AtelierTargetKind.Plaster,AtelierTargetKind.HandMirror,AtelierTargetKind.DryBrush,AtelierTargetKind.MirrorShards })
            {
                var source = Second(kind);
                var sourceCollider = source.GetComponent<Collider>();
                var sourceRay = refs.ViewCamera.ScreenPointToRay(refs.ViewCamera.WorldToScreenPoint(sourceCollider.bounds.center));
                var sourceHits = Physics.RaycastAll(sourceRay,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance).ToArray();
                Check(sourceHits.Length>0 && sourceHits[0].collider==sourceCollider,"第二阶段来源拾取被挡："+kind);
                flow.Observe(source);
                yield return new WaitForSeconds(1.5f);
                if (kind == AtelierTargetKind.HandMirror)
                {
                    Check(!flow.Session.State.Knows(AtelierFlowRules.C("flip")),"拿起手镜自动授予了翻转。");
                    yield return Shot("09_手镜正视真实反射");
                    var mirrorFrame = ScreenCapture.CaptureScreenshotAsTexture();
                    var mirrorScreen = refs.ViewCamera.WorldToScreenPoint(flow.Mirror.Reflection.Surface.transform.TransformPoint(flow.Mirror.Reflection.Surface.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                    int clippedWhite = 0, samples = 0;
                    for (int y = -24; y <= 24; y++) for (int x = -24; x <= 24; x++)
                    {
                        Color pixel = mirrorFrame.GetPixel(Mathf.Clamp((int)mirrorScreen.x+x,0,mirrorFrame.width-1),Mathf.Clamp((int)mirrorScreen.y+y,0,mirrorFrame.height-1));
                        if (pixel.r>.97f && pixel.g>.97f && pixel.b>.97f) clippedWhite++;
                        samples++;
                    }
                    Check(clippedWhite<samples*.15f,"镜面附近出现大面积白色溢出。");
                    Destroy(mirrorFrame);
                    var mirrorTexture = flow.Mirror.Reflection.ReflectionTexture;
                    var oldRT = RenderTexture.active;
                    RenderTexture.active = mirrorTexture;
                    var mirrorImage = new Texture2D(mirrorTexture.width,mirrorTexture.height,TextureFormat.RGBA32,false);
                    mirrorImage.ReadPixels(new Rect(0,0,mirrorTexture.width,mirrorTexture.height),0,0);
                    mirrorImage.Apply();
                    File.WriteAllBytes(Path.Combine(output,"09b_真实镜像RT.png"),mirrorImage.EncodeToPNG());
                    RenderTexture.active = oldRT;
                    Destroy(mirrorImage);
                    flow.Mirror.TurnTo(35);
                    yield return new WaitForSeconds(.5f);
                    Check(Mathf.Abs(flow.Mirror.Angle-15)<.01f,"手镜没有限制到15度。");
                    Check(flow.Session.State.Knows(AtelierFlowRules.C("flip")),"主动转动没有记录翻转。");
                    Check(flow.Mirror.Reflection.SourceUsesPostProcessing,"镜面兼容关闭了主镜头后处理。");
                    yield return Shot("10_手镜15度比较");
                }
                else yield return Shot(kind == AtelierTargetKind.Plaster ? "08_石膏像" : kind == AtelierTargetKind.DryBrush ? "11_干画笔" : "12_已有碎镜片");
                flow.ReturnToRoom();
                yield return new WaitForSeconds(1.5f);
            }
            flow.Session.Combine(new[]{AtelierFlowRules.C("fixed"),AtelierFlowRules.C("flip")});
            flow.Session.Combine(new[]{AtelierFlowRules.C("black"),AtelierFlowRules.C("sharp")});
            flow.Session.Combine(new[]{new ClueId("atelier.flow.twisted"),new ClueId("atelier.flow.rough")});
            Check(flow.Session.State.Knows(AtelierFlowRules.Brush(2)),"第二阶段正常配方未形成柏影笔刷。");
            flow.Observe(Target(AtelierTargetKind.Painting));
            yield return new WaitForSeconds(1.5f);
            string firstLayer = Hash(canvas.CanvasTexture as RenderTexture);
            for (int index = 0; index < 5; index++)
                Check(canvas.Commit(new Vector2(.18f+index*.15f,.30f+index*.06f),canvas.DefaultCost,index*3).Accepted,"柏影笔印未被接受。");
            Check(canvas.Document.Current.RemainingPaint==0 && canvas.Document.Current.Count==5,"柏影层未按5笔合法用尽。");
            Check(canvas.Document.Layers[0].Count==3 && firstLayer!=Hash(canvas.CanvasTexture as RenderTexture),"柏影层没有保留第一层并新增图样。");
            yield return Shot("13_卷涡与柏影真实叠层");
            refs.ContextAction.onClick.Invoke();
            yield return new WaitForSeconds(2);
            Check(flow.IsStageTransition && flow.Voice.ActiveKey=="N04","第二层没有进入N04过场。");
            Check(flow.Draft.WeightForStage(3)==0 && flow.Draft.WeightForStage(0)==0,"N04未结束就提前显色或揭屋顶。");
            yield return Shot("14_N04阶段过场");
            deadline = Time.realtimeSinceStartup+70;
            while (!flow.GameplayInputEnabled && Time.realtimeSinceStartup<deadline) yield return null;
            Check(flow.Session.State.HasFact(AtelierFlowRules.Finished(2)),"第二层完成按钮没有推进。");
            Check(flow.Draft.WeightForStage(0)==0 && flow.Draft.SurroundingsDraft>.99f,"第二层完成误揭屋顶或全部房间。");
            flow.Observe(Target(AtelierTargetKind.Manuscript));
            Check(refs.ManuscriptPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(x=>x.text==AtelierOpeningCopy.NoteBody),"便签仍显示旧作画目标。");
            yield return Shot("15_便签回看");
            flow.ReturnToRoom();
            string[] collected;lock(errors) collected=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"运行结果.txt"),"前两阶段与旁白后逐物件显色自动构建回放完成。\n错误数："+collected.Length+"\n"+string.Join("\n",collected)+"\n边界：自动命令及按钮回调，不是真人鼠标操作。第三、四阶段玩法尚待下一批接入。");
            UnityEngine.Application.logMessageReceivedThreaded-=Collect;
            UnityEngine.Application.Quit(collected.Length==0?0:1);
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
        private string Hash(RenderTexture target)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            byte[] data=image.EncodeToPNG();Destroy(image);RenderTexture.active=previous;
            using(var hash=SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(data));
        }
        private void Collect(string message,string stack,LogType kind)
        {
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)
                lock(errors) if(errors.Count<40) errors.Add(message);
        }
    }
}
