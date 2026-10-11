// =============================================================================
// 文件：AtelierFlowReview.cs
// 职责：显式开发开关下走两条路线，保存实际 Player 画面与输入适配复核。
// 边界：命令与 UI 事件回放不是原生鼠标或真人理解度证明；正常启动不创建此对象。
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
    /// <summary>独立包只在命令行明确指定 ordinary / reality / reverse 时进行开发复核。</summary>
    public sealed class AtelierFlowReview : MonoBehaviour
    {
        private AtelierGameplay game;
        private string folder, route, report = "证据类型=显式调试命令及输入适配事件回放，非真人鼠标验收\n";
        private int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "--atelier-flow-review");
            if (key < 0 || key + 2 >= args.Length)
                return;
            var owner = new GameObject("显式双线流程复核");
            DontDestroyOnLoad(owner);
            var review = owner.AddComponent<AtelierFlowReview>();
            review.folder = Path.GetFullPath(args[key + 1]);
            review.route = args[key + 2];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Application.logMessageReceived += OnLog;
            yield return new WaitForSeconds(5);
            game = FindAnyObjectByType<AtelierGameplay>();
            Require(game != null && game.Session != null, "可玩会话初始化");
            // 显式回放时暂停设备采样，防止桌面指针覆盖本回放的接触射线；正常运行不执行。
            game.enabled = false;
            Require(!game.Session.State.Knows(StudioRules.Room) && game.Finale.Sky.Coverage == 0,
                    "开局没有房间词条和程序化星夜");
            yield return Save("01_普通夜色与空画布");
            Observe(AtelierTargetKind.Manuscript);
            yield return new WaitForSeconds(1);
            yield return Save("02_便条中文");
            game.ReturnToRoom();
            Observe(AtelierTargetKind.Brush);
            yield return new WaitForSeconds(2);
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.WashWater);
            yield return new WaitForSeconds(2);
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.StillLamp);
            Observe(AtelierTargetKind.Window);
            Observe(AtelierTargetKind.Painting);
            yield return new WaitForSeconds(2);
            foreach (var source in new[] { StudioRules.Cup, AtelierRules.Brush, AtelierRules.StillLamp,
                                           StudioRules.Window })
                game.Session.Combine(
                    source == AtelierRules.Brush
                        ? new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(source.Value) }
                        : new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(source.Value),
                                  new ClueId(AtelierRules.Brush.Value) });
            yield return new WaitForSeconds(4.7f);
            yield return Save("03_四项成画");
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.WashWater);
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Brush);
            Require(!game.Session.State.Knows(AtelierRules.Observation("spread")), "洗笔动作不自动授予洗后观察");
            yield return new WaitForSeconds(4.6f);
            Observe(AtelierTargetKind.WashWater);
            yield return Pair(AtelierRules.Observation("still"), AtelierRules.Observation("spread"));
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Painting);
            yield return new WaitForSeconds(2);
            game.Observe(Target(AtelierTargetKind.Painting), game.Painting.WaterUV);
            yield return Pair(StudioRules.Flow, new ClueId(StudioRules.PictureWater.Value));
            yield return new WaitForSeconds(3);
            yield return Save("04_画中水流");
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.OilLamp);
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Matches);
            Observe(AtelierTargetKind.MatchStriker);
            yield return new WaitForSeconds(.8f);
            for (int i = 0; i < 25; i++)
            {
                game.Lamp.SetPointer(new Ray(game.ViewCamera.transform.position,
                                             game.Lamp.Wick.position - game.ViewCamera.transform.position),
                                     true);
                yield return new WaitForSeconds(.025f);
            }
            Require(game.Lamp.CanTouchWick, "火柴头真实接近灯芯");
            Observe(AtelierTargetKind.Wick);
            Require(!game.Session.State.Knows(AtelierRules.Observation("bright")), "点火不自动授予亮起观察");
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.OilLamp);
            yield return Pair(AtelierRules.Observation("dark"), AtelierRules.Observation("bright"));
            yield return Save("05_实体油灯");
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Painting);
            yield return new WaitForSeconds(2);
            game.Observe(Target(AtelierTargetKind.Painting), game.Painting.LampUV);
            yield return Pair(StudioRules.Light, new ClueId(StudioRules.PictureLamp.Value));
            yield return new WaitForSeconds(3.5f);
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Curtain);
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.CurtainCord);
            yield return new WaitForSeconds(5.8f);
            Observe(AtelierTargetKind.Curtain);
            yield return Pair(AtelierRules.Observation("hidden"), AtelierRules.Observation("revealed"));
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Painting);
            yield return new WaitForSeconds(2);
            game.Observe(Target(AtelierTargetKind.Painting), game.Painting.WindowUV);
            yield return Pair(StudioRules.Open, new ClueId(StudioRules.PictureWindow.Value));
            yield return new WaitForSeconds(4);
            Require(game.Session.State.HasFact(AtelierRules.ReadyToDeliver) && !game.Session.State.CanvasLocked,
                    "画内三次解释完成但尚未自动定稿");
            game.Observe(Target(AtelierTargetKind.Painting), new Vector2(.1f, .1f));
            yield return Save("06_主动定稿之前");
            if (route == "ordinary")
            {
                Require(game.ContextAction.gameObject.activeInHierarchy, "定稿按钮仅在实际看画时出现");
                game.ContextAction.onClick.Invoke();
                Require(game.Session.State.HasFact(AtelierRules.CanvasDelivered), "主动定稿成立");
                yield return new WaitForSeconds(4);
                yield return Save("07_定稿留在画架晾干");
                Require(game.Finale.Sky.Coverage == 0 && !game.Session.State.HasFact(AtelierRules.RoofErased),
                        "普通完成保留房间和普通黑夜");
            }
            else
                yield return Reality();
            // 恢复正常 Update，再检查结尾真实输入屏蔽；不以停用控制器的回放代替此检查。
            game.enabled = true;
            yield return null;
            yield return null;
            game.Notebook.SetOpen(true);
            Require(game.Notebook.ExternalInputBlocked && !game.Notebook.IsOpen,
                    "结尾保留观看和暂停，但不再打开解释纸面");
            Screen.SetResolution(1024, 640, FullScreenMode.Windowed);
            yield return new WaitForSeconds(2);
            game.SetPaused(true);
            yield return Save("小窗口中文暂停菜单");
            game.SetPaused(false);
            // 用实际暂停菜单的事件重新载入，确认旧材质、笔刷进度和会话没有带进下一局。
            game.RestartButton.onClick.Invoke();
            yield return new WaitForSeconds(3);
            game = FindAnyObjectByType<AtelierGameplay>();
            Require(game != null && game.Session != null && !game.Session.State.Knows(StudioRules.Room) &&
                        !game.Session.State.HasFact(AtelierRules.RoomErased) && !game.Session.State.CanvasLocked &&
                        Shader.GetGlobalFloat("_AtelierRoom") == 0 && Shader.GetGlobalFloat("_AtelierRoof") == 0 &&
                        game.Finale.Sky.Coverage == 0,
                    "重新开始恢复新会话、完整屋顶和普通黑夜");
            yield return Save("22_重新开始恢复开局");
            Require(errors == 0, "本次运行没有 Console 错误");
            File.WriteAllText(Path.Combine(folder, "复核记录.txt"),
                              report + "运行错误=" + errors + "\n路线=" + route + "\n");
            UnityEngine.Application.Quit();
        }
        private IEnumerator Reality()
        {
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.WashWater);
            yield return new WaitForSeconds(2);
            yield return Pair(StudioRules.Flow, new ClueId(StudioRules.Cup.Value));
            yield return new WaitForSeconds(3);
            yield return Save("07_现实杯水持续流动");
            Require(game.Session.State.HasFact(AtelierRules.RealityTouched), "现实解释进入偏离路线");
            if (route == "reverse")
            {
                yield return Pair(StudioRules.Light, new ClueId(StudioRules.Cup.Value));
                yield return new WaitForSeconds(2);
                yield return Save("可选_杯水色纹发光");
            }
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Trial);
            yield return new WaitForSeconds(2);
            Observe(AtelierTargetKind.Trial);
            yield return Save("08_擦前木板湿色");
            Observe(AtelierTargetKind.TrialCloth);
            // 先短擦并松手，确认局部痕迹保留且布面不会挡住下一次起笔。
            for (int i = 0; i <= 4; i++)
            {
                Vector3 p = game.Trial.BoardCentre + new Vector3(-.245f + i * .012f, .008f, 0);
                var ray = new Ray(game.ViewCamera.transform.position, p - game.ViewCamera.transform.position);
                Require(game.Continuation.Pick(ray, out var hit), "短擦有实际接触表面");
                var actual = hit.collider.GetComponent<AtelierInteractionTarget>();
                game.Continuation.Pointer(actual, hit, i == 0, true, false);
                yield return new WaitForSeconds(.045f);
            }
            game.Continuation.Pointer(Target(AtelierTargetKind.Trial), default, false, false, true);
            yield return new WaitForSeconds(.6f);
            Require(!game.Session.State.HasFact(AtelierRules.TrialWiped), "短擦不替玩家提交完整擦除");
            yield return Save("08a_短擦松手后保留局部痕迹");
            for (int i = 0; i <= 25; i++)
            {
                Vector3 p = game.Trial.BoardCentre + new Vector3(-.245f + i / 25f * .49f, .008f, 0);
                var ray = new Ray(game.ViewCamera.transform.position, p - game.ViewCamera.transform.position);
                bool picked = game.Continuation.Pick(ray, out var hit);
                var actual = picked ? hit.collider.GetComponent<AtelierInteractionTarget>() : null;
                Require(actual != null && (actual.Kind == AtelierTargetKind.Trial ||
                                           actual.Kind == AtelierTargetKind.TrialCloth),
                        "拖布接触拾取 " + i);
                game.Continuation.Pointer(actual, hit, i == 0, true, false);
                yield return new WaitForSeconds(.045f);
            }
            Require(game.Session.State.HasFact(AtelierRules.TrialWiped) &&
                        !game.Session.State.Knows(AtelierRules.Observation("base")),
                    "真实擦拭完成但未自动授予擦后观察");
            yield return new WaitForSeconds(1.2f);
            ObservePicked(AtelierTargetKind.Trial, game.Trial.BoardCentre + new Vector3(-.16f, .006f, 0));
            yield return Save("09_擦后露出木纹");
            yield return Pair(AtelierRules.Observation("wet"), AtelierRules.Observation("base"));
            game.ReturnToRoom();
            yield return new WaitForSeconds(2);
            if (route == "reverse")
            {
                Observe(AtelierTargetKind.Window);
                Look(new Vector3(1.35f, 2.5f, 2.45f));
                yield return Pair(AtelierRules.Erase, new ClueId(StudioRules.Window.Value));
                yield return new WaitForSeconds(6);
                yield return Save("可选_窗框擦去而帘布仍在");
            }
            Look(new Vector3(0, 4.25f, -.1f));
            ObservePicked(AtelierTargetKind.Ceiling, new Vector3(0, 4.20f, -.1f));
            yield return Pair(AtelierRules.Erase, new ClueId(StudioRules.Ceiling.Value));
            yield return new WaitForSeconds(3);
            yield return Save("10_屋顶擦开中");
            yield return new WaitForSeconds(5);
            yield return Save("11_梁间普通黑夜");
            Require(!game.Session.State.Knows(StudioRules.Sky), "屋顶动画没有自动授予天空");
            ObservePicked(AtelierTargetKind.Sky, new Vector3(0, 4.60f, -.1f));
            yield return Pair(route == "reverse" ? StudioRules.Flow : StudioRules.Light,
                              new ClueId(StudioRules.Sky.Value));
            yield return new WaitForSeconds(7);
            yield return Save("12_天空第一次解释");
            yield return Pair(route == "reverse" ? StudioRules.Light : StudioRules.Flow,
                              new ClueId(StudioRules.Sky.Value));
            yield return new WaitForSeconds(7);
            yield return Save("13_完整流动星夜");
            yield return new WaitForSeconds(4);
            yield return Save("14_同机位流动对照");
            Require(game.Session.State.HasFact(StudioRules.FullSky) && !game.Session.State.Knows(StudioRules.Room),
                    "完整星夜不自动授予房间");
            Look(Target(AtelierTargetKind.SharedMark).transform.position);
            ObservePicked(AtelierTargetKind.SharedMark, Target(AtelierTargetKind.SharedMark).transform.position);
            yield return Save("15_接连笔迹的亲点调查");
            Require(!game.Session.State.Knows(StudioRules.Room), "接连笔迹调查也不自动授予房间");
            Look(Target(AtelierTargetKind.Room).transform.position);
            ObservePicked(AtelierTargetKind.Room, Target(AtelierTargetKind.Room).transform.position);
            Require(game.Session.State.Knows(StudioRules.Room), "亲点整间画室后取得房间");
            yield return Save("16_整间画室亲点");
            Look(new Vector3(.4f, 2.8f, 2.05f));
            yield return Pair(AtelierRules.Erase, new ClueId(StudioRules.Room.Value));
            yield return new WaitForSeconds(4);
            yield return Save("17_统一笔势擦去梁与窗上缘");
            yield return new WaitForSeconds(4);
            yield return Save("18_统一笔势经过窗帘和桌面");
            yield return new WaitForSeconds(9);
            yield return Save("19_房间完全消失");
            Require(game.Finale.Finished, "全房间擦除演出结束");
            Look(new Vector3(7, 2.7f, -1));
            yield return Save("20_侧向仍是同一片程序化星夜");
            Look(new Vector3(-7, 3, -2));
            yield return Save("21_另一侧的连续星夜");
            Require(Shader.GetGlobalFloat("_AtelierRoom") > .999f, "所有表面共用一个最终擦除进度");
        }
        private void Look(Vector3 point)
        {
            game.ViewCamera.transform.LookAt(point);
        }
        private void ObservePicked(AtelierTargetKind kind, Vector3 point)
        {
            var ray = new Ray(game.ViewCamera.transform.position, point - game.ViewCamera.transform.position);
            Require(game.Continuation.Pick(ray, out var hit), "射线拾取存在：" + kind);
            var target = hit.collider.GetComponent<AtelierInteractionTarget>();
            Require(target != null && target.Kind == kind,
                    "射线首先命中可见来源：" + kind + " 实际=" + hit.collider.name);
            game.Observe(target, hit.textureCoord);
        }
        private AtelierInteractionTarget Target(AtelierTargetKind kind) =>
            FindObjectsByType<AtelierInteractionTarget>().First(t => t.Kind == kind);
        private void Observe(AtelierTargetKind kind) => game.Observe(Target(kind), Vector2.zero);
        private IEnumerator Pair(ClueId a, ClueId b)
        {
            game.Notebook.SetOpen(true);
            yield return null;
            Place(a, -95);
            Place(b, 95);
            // 只有玩家意图对应的显式提交才交给引擎；摆放本身没有副作用。
            game.Notebook.InterpretSelected();
            yield return new WaitForSeconds(.5f);
            game.Notebook.SetOpen(false);
        }
        private void Place(ClueId clue, float x)
        {
            var canvas = game.Notebook.GetComponentInChildren<NotebookCanvasView>(true);
            var row = game.Notebook.GetComponentsInChildren<Button>(true).First(
                b => b.name.EndsWith("_" + clue.Value) && !b.name.StartsWith("CanvasClue_"));
            int before = canvas.ActiveCardCount, known = game.Session.State.KnownClues.Count(),
                facts = game.Session.State.Facts.Count();
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.pointerClickHandler);
            var paper = canvas.GetComponentInChildren<NotebookPaperPlacement>();
            data.position = RectTransformUtility.WorldToScreenPoint(
                null, paper.transform.TransformPoint(new Vector3(x, 30, 0)));
            ExecuteEvents.Execute(paper.gameObject, data, ExecuteEvents.pointerClickHandler);
            Require(canvas.ActiveCardCount == before + 1 && game.Session.State.KnownClues.Count() == known &&
                        game.Session.State.Facts.Count() == facts,
                    "纸面临时摆放无领域副作用");
        }
        private IEnumerator Save(string name)
        {
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            File.WriteAllText(Path.Combine(folder, name + ".txt"),
                              "证据=调试命令及输入适配事件回放\n事实=" +
                                  string.Join("、", game.Session.State.Facts.Select(f => f.Value)) +
                                  "\n错误=" + errors + "\n");
        }
        private void Require(bool value, string message)
        {
            report += (value ? "通过=" : "失败=") + message + "\n";
            File.WriteAllText(Path.Combine(folder, "进行中复核.txt"), report);
            if (!value)
                throw new InvalidOperationException("流程复核失败：" + message);
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors++;
        }
        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
        }
    }
}
