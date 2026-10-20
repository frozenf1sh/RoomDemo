// 文件：AtelierStarNightWalkthrough.cs
// 职责：仅在 Editor 用真实会话命令与 UI 事件慢速走完四阶段现实路线，配合录制器生成整段录像。
// 边界：不写存档、不跳事实、不改变正式构建；录像用于观看和复核，不是真人鼠标验收。
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity.ArtRebuild
{
    /// <summary>固定步进的慢速整段回放；每一步都使用正常游玩入口，不额外授予材料。</summary>
    public sealed class AtelierStarNightWalkthrough : MonoBehaviour
    {
        public string OutputDirectory;
        private AtelierFlowGameplay flow;
        private AtelierWalkthroughCapture capture;

        /// <summary>显式开始录制；只从 Editor 的 Play 模式调用。</summary>
        public static void StartEditor(string directory)
        {
            var go = new GameObject("星夜流程慢速回放");
            var walk = go.AddComponent<AtelierStarNightWalkthrough>();
            walk.OutputDirectory = Path.GetFullPath(directory);
        }

        private IEnumerator Start()
        {
            UnityEngine.Application.runInBackground = true;
            Time.timeScale = .8f;
            Directory.CreateDirectory(OutputDirectory);
            var captureGo = new GameObject("星夜流程录制");
            capture = captureGo.AddComponent<AtelierWalkthroughCapture>();
            capture.OutputPath = Path.Combine(OutputDirectory, "star-night-walkthrough.mp4");
            capture.Fps = 24;
            // 等录制器 Start 设定固定帧步进，再开始寻找会话。
            yield return null; yield return null;
            flow = FindAnyObjectByType<AtelierFlowGameplay>();
            if (flow == null) { Debug.LogError("[星夜回放] 找不到会话。"); yield break; }
            yield return Ready();

            var notebook = flow.References.Notebook;
            notebook.SetOpen(true); yield return Wait(1.0f);
            var help = notebook.GetComponent<AtelierNotebookHelp>();
            if (help != null)
            {
                // 四步帮助逐页停留，让画面能读到每一步的区域说明。
                foreach (var button in help.GetComponentsInChildren<Button>(true).Where(x => x.name == "下一步").ToArray())
                    button.onClick.Invoke();
                yield return Wait(1.4f);
                var next = help.GetComponentsInChildren<Button>(true).First(x => x.name == "下一步");
                next.onClick.Invoke(); yield return Wait(1.4f);
                next.onClick.Invoke(); yield return Wait(1.4f);
                help.CloseFromBack();
            }
            notebook.SetOpen(false); yield return Ready();

            var g = flow.Session;
            g.Inspect(StudioRules.Cup);
            g.Inspect(AtelierRules.Brush);
            g.PerformAction(StudioRules.Cup, "wash_release_pigment");
            g.Inspect(AtelierFlowRules.Crayon);
            yield return Compose(AtelierFlowRules.C("transparent"), AtelierFlowRules.C("blue"));

            var paper = notebook.CombinationCanvas;
            paper.Add(AtelierFlowRules.C("flow"), new Vector2(-100, 0));
            paper.Add(AtelierFlowRules.C("free"), new Vector2(100, 0));
            yield return Wait(2.0f);
            var card = paper.Workspace.GetComponentsInChildren<Button>().First(x => x.name.StartsWith("CanvasClue_"));
            card.onClick.Invoke(); yield return Wait(1.0f);
            yield return Compose(AtelierFlowRules.C("flow"), AtelierFlowRules.C("free"));
            yield return Compose(new ClueId("atelier.flow.ice_blue"), new ClueId("atelier.flow.swirl"));
            yield return PaintLayer();
            if (!flow.CanvasPort.Finish().Accepted) { Fail("第一层无法完成。"); yield break; }
            yield return Wait(1.0f); yield return Ready();

            foreach (var source in new[] { AtelierFlowRules.Plaster, AtelierFlowRules.HandMirror, AtelierFlowRules.DryBrush, AtelierFlowRules.MirrorShards })
                g.Inspect(source);
            g.PerformAction(AtelierFlowRules.HandMirror, "mirror_compare");
            yield return Compose(AtelierFlowRules.C("fixed"), AtelierFlowRules.C("flip"));
            yield return Compose(AtelierFlowRules.C("black"), AtelierFlowRules.C("sharp"));
            yield return Compose(new ClueId("atelier.flow.twisted"), new ClueId("atelier.flow.rough"));
            yield return PaintLayer();
            if (!flow.CanvasPort.Finish().Accepted) { Fail("第二层无法完成。"); yield break; }
            yield return Wait(1.0f); yield return Ready();

            flow.Observe(Target(AtelierTargetKind.DryPalette)); yield return Ready(); yield return Wait(.9f);
            flow.Observe(Target(AtelierTargetKind.PaletteKnife)); yield return Ready(); yield return Wait(.9f);
            var palette = flow.StageThree.Palette;
            palette.Contact(new Vector2(.12f, .5f), new Vector2(.44f, .5f), false); yield return Wait(.8f);
            palette.Contact(new Vector2(.64f, .5f), new Vector2(.94f, .5f), true); yield return Wait(1.2f);
            flow.ReturnToRoom(); yield return Ready();
            flow.Observe(Target(AtelierTargetKind.Window)); yield return Ready(); yield return Wait(1.0f);
            flow.StageThree.ObserveWindowDetail(true); yield return Wait(1.2f);
            flow.StageThree.ObserveWindowDetail(false); yield return Wait(1.2f);

            yield return Compose(new ClueId(AtelierFlowRules.NearWindowLight.Value), new ClueId(AtelierFlowRules.FarWindowLight.Value));
            yield return Compose(AtelierFlowRules.C("window_lights"), new ClueId(StudioRules.Window.Value));
            yield return PaintLayer();
            if (!flow.CanvasPort.Finish().Accepted) { Fail("第三层无法完成。"); yield break; }
            yield return Wait(1.2f); yield return Ready();

            flow.Observe(Target(AtelierTargetKind.OilLamp)); yield return Ready(); yield return Wait(1.2f);
            flow.Observe(Target(AtelierTargetKind.Matches)); yield return Wait(1.0f);
            flow.Observe(Target(AtelierTargetKind.MatchStriker)); yield return Wait(1.2f);
            flow.References.Lamp.HoldAtWick();
            g.PerformAction(StudioRules.LampBody, "light_wick");

            float roofStart = Time.time;
            while ((!flow.StageFour.Allowed(flow.StageFour.RoofTarget) || flow.Draft.WeightForStage(0) < .98f) && Time.time - roofStart < 180)
                yield return null;
            yield return Ready(); yield return Wait(1.2f);

            flow.Observe(flow.StageFour.RoofTarget); yield return Ready(); yield return Wait(1.2f);
            yield return Compose(AtelierFlowRules.C("dry_paint"), new ClueId(StudioRules.Ceiling.Value));
            yield return CloseNotebook();
            yield return Wait(1.6f);
            Tool(AtelierFinaleAction.ProbeRoof); yield return Wait(1.4f);
            Tool(AtelierFinaleAction.ContinueRoof, 6300, 7800); yield return Wait(7f); yield return Ready();

            var entry = flow.StageFour.RoomEntryTarget;
            flow.Observe(entry); yield return Ready(); yield return Wait(1.4f);
            yield return Compose(AtelierFlowRules.C("wet_paint"), new ClueId(StudioRules.Room.Value));
            yield return CloseNotebook();
            yield return Compose(AtelierFlowRules.C("warm"), AtelierFlowRules.C("bright"));
            yield return PaintLayer();

            flow.ReturnToRoom(); yield return Ready();
            flow.Observe(flow.StageFour.RoomTarget); yield return Ready(); yield return Wait(1.2f);
            Tool(AtelierFinaleAction.EraseRoom); yield return Wait(7f); yield return Ready(); yield return Wait(1.2f);

            flow.Observe(Target(AtelierTargetKind.Painting)); yield return Wait(1.6f);
            flow.Observe(flow.StageFour.SkyTarget); yield return Wait(1.4f);
            yield return Compose(new ClueId(StudioRules.Sky.Value), new ClueId(AtelierRules.Painting.Value));
            yield return CloseNotebook();
            flow.Observe(flow.StageFour.SkyTarget); yield return Ready(); yield return Wait(1.0f);

            flow.StageFour.PaintSky(); yield return Wait(2.5f);
            flow.StageFour.PaintSky();
            float finalStart = Time.time;
            while (!flow.References.Finale.Finished && Time.time - finalStart < 240) yield return null;
            yield return Wait(3f);
            while (!flow.StageFour.MenuVisible && Time.time - finalStart < 300) yield return null;
            yield return Wait(4f);
            capture.StopAndClose();
            yield return Wait(2f);
            Debug.Log("[星夜回放] 慢速整段完成。");
        }

        private IEnumerator Ready()
        {
            float start = Time.time;
            while ((flow.IsOpening || flow.IsStageTransition || flow.CameraMoving || !flow.GameplayInputEnabled) && Time.time - start < 150)
                yield return null;
            yield return null; yield return null;
        }

        private static IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

        private IEnumerator CloseNotebook()
        {
            flow.References.Notebook.SetOpen(false);
            yield return Ready();
            yield return Wait(.5f);
        }

        private IEnumerator Compose(ClueId first, ClueId second)
        {
            var notebook = flow.References.Notebook;
            if (!notebook.IsOpen) { notebook.SetOpen(true); yield return Wait(.6f); }
            notebook.GetComponent<AtelierNotebookHelp>()?.CloseFromBack();
            var paper = notebook.CombinationCanvas;
            foreach (var old in paper.CombinationInput.ToArray()) paper.Remove(old);
            paper.Add(first, new Vector2(-100, 20));
            paper.Add(second, new Vector2(100, 20));
            yield return Wait(.8f);
            notebook.InterpretSelected();
            yield return Wait(1.6f);
        }

        private IEnumerator PaintLayer()
        {
            yield return CloseNotebook();
            flow.ReturnToRoom(); yield return Ready();
            flow.Observe(Target(AtelierTargetKind.Painting)); yield return Ready();
            foreach (var point in new[] { new Vector2(.30f, .58f), new Vector2(.64f, .48f), new Vector2(.48f, .73f) })
            {
                flow.CanvasPort.Commit(point, flow.CanvasPort.DefaultCost, 0);
                yield return Wait(.5f);
            }
            yield return Wait(1.0f);
        }

        private AtelierCanvasResult Tool(AtelierFinaleAction action, int a = 4200, int b = 5500)
            => flow.Session.ApplyAtelierFinale(flow.Session.SessionEpoch, flow.CanvasPort.Document.Revision, action,
                new[] { new AtelierContactSegment(a, 5000, b, 5000) });

        private static AtelierInteractionTarget Target(AtelierTargetKind kind)
            => FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include).First(x => x.Kind == kind);

        private void Fail(string message)
        {
            Debug.LogError("[星夜回放] " + message);
            capture?.StopAndClose();
        }
    }
}
#endif
