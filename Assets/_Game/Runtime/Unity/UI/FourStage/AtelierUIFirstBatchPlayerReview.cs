// 文件：AtelierUIFirstBatchPlayerReview.cs
// 职责：显式参数下回放真实第一层会话并检查正式UI按钮和截图。
// 边界：不是Fake，不赠送线索；自动命令取证不能替代真人鼠标验收。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEngine;
namespace Game.Unity.UI.FourStage
{
    /// <summary>只装在临时验证场景，正常运行不执行回放。</summary>
    public sealed class AtelierUIFirstBatchPlayerReview : MonoBehaviour
    {
        public AtelierFlowGameplay Gameplay;
        public AtelierCanvasWorkspace Workspace;
        public AtelierPaintingSessionAdapter Painting;
        public AtelierBrushResultView Result;
        private string output;
        private readonly List<string> errors = new List<string>();
        private void Collect(string copy, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                lock(errors) errors.Add(copy);
        }
        private void Check(bool success, string reason) { if (!success) errors.Add(reason); }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args,"--atelier-ui-review-output");
            if (at < 0 || at + 1 >= args.Length) yield break;
            output = Path.GetFullPath(args[at+1]);
            Directory.CreateDirectory(output);
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Application.logMessageReceivedThreaded += Collect;
            yield return null;
            float until = Time.realtimeSinceStartup + 90;
            while (Gameplay.IsOpening && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Check(!Gameplay.IsOpening,"开场旁白没有结束。");
            var refs = Gameplay.References;
            AtelierInteractionTarget Target(AtelierTargetKind kind) => kind == AtelierTargetKind.Painting ?
                Gameplay.CanvasPort.Surface.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind) :
                refs.GetComponentsInChildren<AtelierInteractionTarget>(true).First(x => x.Kind == kind);
            Gameplay.Observe(Target(AtelierTargetKind.WashWater));
            yield return new WaitForSecondsRealtime(1.5f);
            Gameplay.Observe(Target(AtelierTargetKind.Brush));
            Gameplay.Observe(Target(AtelierTargetKind.WashWater));
            yield return new WaitForSecondsRealtime(2);
            Check(Gameplay.Session.State.HasFact(AtelierFlowRules.Washed),"真实接触未形成洗笔结果。");
            Gameplay.ReturnToRoom();
            yield return new WaitForSecondsRealtime(1.5f);
            Gameplay.ObserveCrayon();
            yield return new WaitForSecondsRealtime(1.5f);
            refs.Notebook.SetOpen(true);
            yield return new WaitForSecondsRealtime(.2f);
            Gameplay.Session.Combine(new[]{AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue")});
            Gameplay.Session.Combine(new[]{AtelierFlowRules.C("flow"),AtelierFlowRules.C("free")});
            Gameplay.Session.Combine(new[]{new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl")});
            yield return null;
            Check(Result.Panel.activeSelf,"真实配方未显示笔刷结果页。");
            Check(Result.Preview.texture == Gameplay.CanvasPort.Brushes[0],"笔刷结果不是对应原始PNG。");
            yield return Shot("01_真实笔刷形成页");
            Result.Close.onClick.Invoke();
            Check(!refs.Notebook.IsOpen,"收起笔记按钮没有关闭书。");
            Check(!Gameplay.IsCanvasFocused,"关闭笔记错误地自动进入画架。");
            Gameplay.ReturnToRoom();
            yield return new WaitForSecondsRealtime(1.5f);
            Gameplay.Observe(Target(AtelierTargetKind.Painting));
            yield return new WaitForSecondsRealtime(1.5f);
            var r = Workspace.References;
            Check(Gameplay.UsesExternalCanvasWorkspace,"Workspace未接管旧输入。");
            Check(!refs.ContextAction.gameObject.activeSelf,"旧完成控件仍然可见。");
            Check(!r.Finish.interactable,"零笔时完成按钮错误开放。");
            yield return Shot("02_零笔正式Workspace");
            var point = refs.ViewCamera.WorldToScreenPoint(Gameplay.CanvasPort.Surface.bounds.center);
            Check(Painting.TryMap(point,out var uv),"真实屏幕点没有映射到世界画布。");
            var old = Painting.Read();
            string Commit()
            {
                var snapshot = Painting.Read();
                return Painting.RequestCommitStroke(new AtelierStrokeIntent(snapshot,snapshot.SelectedCost,35),new Vector2(.5f,.5f));
            }
            Check(string.IsNullOrEmpty(Commit()),"第一笔UI端口提交失败。");
            Check(!string.IsNullOrEmpty(Painting.RequestCommitStroke(new AtelierStrokeIntent(old,old.SelectedCost,0),new Vector2(.5f,.5f))),"过期版本未被拒绝。");
            Check(string.IsNullOrEmpty(Commit()),"第二笔UI端口提交失败。");
            yield return null;
            Check(!r.Finish.interactable,"两笔时完成按钮错误开放。");
            yield return Shot("03_两笔缺项提示");
            r.Undo.onClick.Invoke();
            yield return null;
            Check(Gameplay.CanvasPort.Document.Current.Count == 1,"撤销按钮没有通过真实命令退款。");
            Check(string.IsNullOrEmpty(Commit()),"撤销后重新落笔失败。");
            Check(string.IsNullOrEmpty(Commit()),"第三笔提交失败。");
            yield return null;
            Check(r.Finish.interactable,"三笔时完成按钮未开放。");
            yield return Shot("04_三笔可提前完成");
            int remaining = Gameplay.CanvasPort.Document.Current.RemainingPaint;
            r.Return.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            Gameplay.Observe(Target(AtelierTargetKind.Painting));
            yield return new WaitForSecondsRealtime(1.5f);
            Check(Gameplay.CanvasPort.Document.Current.Count == 3 && Gameplay.CanvasPort.Document.Current.RemainingPaint == remaining,"返回重进丢失画作或颜料。");
            if (!args.Contains("--ui-early-finish"))
            {
                int attempts = 0;
                while (Gameplay.CanvasPort.Document.Current.Count < 6 && attempts++ < 6)
                {
                    Check(string.IsNullOrEmpty(Commit()),"后续笔触提交失败。");
                    yield return null;
                    if (Gameplay.CanvasPort.Document.Current.Count == 5)
                    {
                        var last = Painting.Read();
                        Check(last.MinCost == last.MaxCost && last.MinCost >= 12 && last.MaxCost <= 24,"末笔范围不合法。");
                        yield return Shot("05_末笔大小固定");
                    }
                }
                Check(Gameplay.CanvasPort.Document.Current.RemainingPaint == 0,"六笔没有精确耗尽。");
                yield return Shot("06_六笔耗尽待主动收层");
            }
            r.Finish.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            Check(Gameplay.Session.State.HasFact(AtelierFlowRules.Finished(1)),"正式完成按钮未封存首层。");
            yield return Shot("07_主动完成首层");
            UnityEngine.Application.logMessageReceivedThreaded -= Collect;
            File.WriteAllText(Path.Combine(output,"运行结果.txt"),"第一批UI真实构建自动回放完成。\n错误数："+errors.Count+"\n"+
                string.Join("\n",errors)+"\n边界：真实会话、UI按钮回调和端口命令；不是真人鼠标或首次理解度验收。");
            UnityEngine.Application.Quit(errors.Count == 0 ? 0 : 1);
        }
        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            Destroy(image);
        }
        private void OnDestroy() { UnityEngine.Application.logMessageReceivedThreaded -= Collect; }
    }
}
