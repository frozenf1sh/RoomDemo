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
            yield return new WaitForSecondsRealtime(2);
            var flow=FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null) { File.WriteAllText(Path.Combine(output,"运行结果.txt"),"缺少新流程控制器。");UnityEngine.Application.Quit(1);yield break; }
            var refs=flow.References;
            // 画架视觉属于独立Prefab，不在玩法根节点下；通过已绑定Surface定位自己的Logic。
            AtelierInteractionTarget Target(AtelierTargetKind kind) => kind == AtelierTargetKind.Painting
                ? flow.CanvasPort.Surface.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind)
                : refs.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind);
            yield return Shot("01_未读便条");
            Check(!flow.Session.State.Knows(StudioRules.Cup),"开局不得代授杯子。");
            flow.Observe(Target(AtelierTargetKind.Manuscript));yield return Shot("02_原有便条");
            flow.ReturnToRoom();yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("03_阶段一底稿");
            flow.Observe(Target(AtelierTargetKind.WashWater));yield return new WaitForSecondsRealtime(1.4f);
            flow.Observe(Target(AtelierTargetKind.Brush));flow.Observe(Target(AtelierTargetKind.WashWater));
            Check(!flow.Session.State.HasFact(AtelierFlowRules.Washed),"开始靠近就错误提交了洗笔事实。");
            yield return new WaitForSecondsRealtime(1.7f);
            Check(flow.Session.State.HasFact(AtelierFlowRules.Washed),"真实笔尖接触未提交动作。");
            Check(flow.Session.State.Knows(AtelierFlowRules.C("blue"))&&flow.Session.State.Knows(AtelierFlowRules.C("flow")),"洗笔结果没有同次收录。");
            yield return Shot("04_接触与散色");
            flow.ReturnToRoom();yield return new WaitForSecondsRealtime(1.4f);
            var screen=refs.ViewCamera.WorldToScreenPoint(flow.CrayonPicking.bounds.center);
            var ray=refs.ViewCamera.ScreenPointToRay(screen);
            var hits=Physics.RaycastAll(ray,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance).ToArray();
            Check(hits.Length>0&&hits[0].collider==flow.CrayonPicking,"小画主视角射线被其他Collider挡住。");
            flow.ObserveCrayon();yield return new WaitForSecondsRealtime(1.4f);
            flow.Session.Combine(new[]{AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue")});
            flow.Session.Combine(new[]{AtelierFlowRules.C("flow"),AtelierFlowRules.C("free")});
            flow.Session.Combine(new[]{new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl")});
            Check(flow.Session.State.Knows(AtelierFlowRules.Brush(1)),"正常配方未形成第一笔刷。");
            Check(!flow.Session.State.CanvasLocked,"第三次概念配方错误触发了旧收束。");
            flow.ReturnToRoom();yield return new WaitForSecondsRealtime(1.4f);
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSecondsRealtime(1.4f);
            var canvas=flow.CanvasPort;
            var paintingTarget = Target(AtelierTargetKind.Painting);
            var paintingCollider = paintingTarget.GetComponent<Collider>();
            var canvasRay = refs.ViewCamera.ScreenPointToRay(refs.ViewCamera.WorldToScreenPoint(paintingCollider.bounds.center));
            var canvasHits = Physics.RaycastAll(canvasRay,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x=>x.distance).ToArray();
            Check(canvasHits.Length > 0 && canvasHits[0].collider == paintingCollider, "画布近景射线被其他Collider挡住。");
            if (canvasHits.Length > 0) Check(canvasHits[0].textureCoord.sqrMagnitude > .01f, "画布UV拾取无效。");
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
            flow.ReturnToRoom();yield return new WaitForSecondsRealtime(1.4f);
            flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSecondsRealtime(1.4f);
            Check(artwork==Hash(canvas.CanvasTexture as RenderTexture),"返回再进入改变或丢失了作品。");
            Check(refs.ContextAction.interactable,"三笔后完成按钮没有开放。");
            refs.ContextAction.onClick.Invoke();yield return new WaitForSecondsRealtime(4.5f);
            Check(flow.Voice.ActiveKey == "N03", "新阶段旁白被过时开场队列拖延。 ");
            Check(!refs.ContextPanel.activeSelf, "回到主视角重新弹出了旧物件说明。");
            Check(flow.Session.State.HasFact(AtelierFlowRules.Finished(1)),"真实完成按钮没有推进首层。");
            Check(canvas.Document.Layers[0].CommittedUnits+canvas.Document.Layers[0].DiscardedUnits==100,"首层颜料账本不守恒。");
            Check(flow.Draft.LeftDraft<.01f&&flow.Draft.WindowDraft>.95f,"首层后区域显色错误。");
            yield return Shot("06_收层后左侧显色");
            string[] collected;lock(errors) collected=errors.ToArray();
            File.WriteAllText(Path.Combine(output,"运行结果.txt"),"P00至P07自动构建回放完成。\n错误数："+collected.Length+"\n"+string.Join("\n",collected)+"\n边界：自动命令及按钮回调，不是真人鼠标操作。第二阶段玩法尚待下一批接入。");
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
