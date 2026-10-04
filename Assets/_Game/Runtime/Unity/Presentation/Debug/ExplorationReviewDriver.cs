// =============================================================================
// 文件：ExplorationReviewDriver.cs
// 职责：用显式命令行入口在原生构建中复测检视、联想、次日与星空样板，导出真实帧。
// 边界：是自动技术走查，不伪装成真人操作或陌生玩家理解度验收；不注入系统输入。
//       案例 A 经实际房间射线、近景热点和笔记 API；星空段使用明确开发预设。
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Unity
{
    /// <summary>第四阶段具体复测器，通常启动时不运行，也不挂调试快捷键。</summary>
    public sealed class ExplorationReviewDriver : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private InteractionSystem interaction;
        [SerializeField] private DayTransitionController day;
        [SerializeField] private StarryWorldSequence sequence;
        [SerializeField] private RoomModules room;
        [SerializeField] private WorldExterior exterior;
        [SerializeField] private WorldMutation mutation;
        [SerializeField] private SceneEntity plant, book;
        [SerializeField] private Transform[] retainedProps;
        private Report report;
        private bool capture, quitWhenDone;
        private readonly List<float> frames = new List<float>(2048);
        /// <summary>输出为本游戏用户数据目录，原生程序不依赖开发仓库写权限。</summary>
        public string OutputDirectory => Path.Combine(UnityEngine.Application.persistentDataPath, "ExplorationReview");

        /// <summary>编辑工具显式绑定所有观察对象；采样期间不反复全场景查找。</summary>
        public void Configure(GameBootstrap game, NotebookView notes, InspectionController details, InteractionSystem input,
            DayTransitionController nextDay, StarryWorldSequence director, RoomModules architecture, WorldExterior landscape,
            WorldMutation operations, SceneEntity pottedPlant, SceneEntity oldBook, Transform[] furniture)
        { bootstrap = game; notebook = notes; inspection = details; interaction = input; day = nextDay; sequence = director; room = architecture; exterior = landscape; mutation = operations; plant = pottedPlant; book = oldBook; retainedProps = furniture; }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            capture = Array.IndexOf(args, "-roomDemoExplorationCapture") >= 0;
            if (!capture && Array.IndexOf(args, "-roomDemoExplorationReview") < 0) return;
            quitWhenDone = Array.IndexOf(args, "-roomDemoQuitAfterReview") >= 0;
            report = new Report { result = "Running", startedUtc = DateTime.UtcNow.ToString("O"),
                mode = capture ? "自动探索走查与画面取样（含截图开销，无音轨）" : "自动探索走查与十轮星空技术复测（无截图）",
                unity = UnityEngine.Application.unityVersion, platform = UnityEngine.Application.platform.ToString(), gpu = SystemInfo.graphicsDeviceName,
                device = SystemInfo.deviceModel, width = Screen.width, height = Screen.height, editor = UnityEngine.Application.isEditor, requestedRuns = capture ? 1 : 10 };
            Directory.CreateDirectory(OutputDirectory);
            if (capture)
            {
                Directory.CreateDirectory(Path.Combine(OutputDirectory, "StarryFrames"));
                // 只清理本复测器的帧目录，不触及任何游戏内容、存档或其他取样。
                foreach (var path in Directory.GetFiles(Path.Combine(OutputDirectory, "StarryFrames"), "*.png")) File.Delete(path);
            }
            Save(); StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            bootstrap.Restart(); yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("初始房间");
            if (!Check("植物房间射线可以进入检视", RoomClick(plant))) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            if (!Check("普通点击不能获得生长", !bootstrap.Session.State.Knows(DemoRules.Growth))) yield break;
            yield return Capture("盆栽正面");
            // 多个小角度累计到背面，走实际旋转入口，随后以真实热点射线收集新芽。
            for (int i = 0; i < 12; i++) { inspection.Rotate(new Vector2(15 / .35f, 0)); yield return new WaitForSecondsRealtime(.04f); }
            inspection.Zoom(180); yield return null; Physics.SyncTransforms();
            if (!Check("旋转之后亲点新芽", HotspotClick("sprout") && bootstrap.Session.State.Knows(ExplorationRules.Sprout))) yield break;
            yield return Capture("发现新芽"); inspection.Exit(); yield return new WaitForSecondsRealtime(.25f);
            if (!Check("书本房间射线可以进入检视", RoomClick(book))) yield break;
            yield return new WaitForSecondsRealtime(.35f); yield return Capture("旧书封面");
            if (!Check("封面开启书本", HotspotClick("cover"))) yield break;
            yield return new WaitForSecondsRealtime(.4f); yield return Capture("旧书第一页");
            if (!Check("点击页角翻到短句页", HotspotClick("next1"))) yield break;
            yield return new WaitForSecondsRealtime(.4f); yield return Capture("旧书向光页");
            if (!Check("短句需要亲点才获得向光", HotspotClick("quote") && bootstrap.Session.State.Knows(ExplorationRules.TowardLight))) yield break;
            yield return Capture("发现向光"); inspection.Exit(); yield return new WaitForSecondsRealtime(.25f);
            notebook.SetOpen(true); notebook.SelectClue(ExplorationRules.Sprout); notebook.SelectClue(ExplorationRules.TowardLight);
            yield return new WaitForSecondsRealtime(.4f); yield return Capture("两个观察"); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(1.1f); yield return Capture("形成生长");
            if (!Check("观察联想形成概念而没有世界事实", bootstrap.Session.State.Knows(DemoRules.Growth) && bootstrap.Session.State.Facts.Count == 0)) yield break;
            var word = notebook.transform.Find("NotebookPanel/Notebook/InsightWord").GetComponent<UnityEngine.UI.Text>();
            // 领域解锁和文本赋值不等于能看见奖励，原生构建还必须实际生成中文字形网格。
            if (!Check("生长概念在纸面实际绘制", word.gameObject.activeInHierarchy && word.text == "生长" && word.cachedTextGenerator.verts.Count >= 8)) yield break;
            yield return new WaitForSecondsRealtime(.8f);
            notebook.SelectClue(DemoRules.Clue("growth")); notebook.SelectClue(DemoRules.Clue("plant")); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(1.5f); yield return Capture("当天轻微生长");
            if (!Check("当天有生长意向，书还没有落下", bootstrap.Session.State.HasFact(ExplorationRules.PlantStartsGrowing) && !bootstrap.Session.State.HasFact(DemoRules.BookFallen))) yield break;
            day.Begin(); yield return new WaitForSecondsRealtime(2.7f); yield return Capture("第二天的后果");
            if (!Check("第二天繁茂并自动落书", bootstrap.Session.State.DayIndex == 2 && bootstrap.Session.State.HasFact(ExplorationRules.PlantOvergrown) && bootstrap.Session.State.HasFact(DemoRules.BookFallen))) yield break;
            if (!Check("尚未点击的窗和画仍未知", !bootstrap.Session.State.Knows(DemoRules.Window) && !bootstrap.Session.State.Knows(DemoRules.Painting))) yield break;
            report.caseACompleted = true; Save();

            string sceneName = SceneManager.GetActiveScene().name;
            var retainedPositions = retainedProps.Select(x => x.position).ToArray();
            for (int index = 0; index < report.requestedRuns; index++)
            {
                bootstrap.JumpTo(ExplorationPresets.BeforeStarryWorld); yield return new WaitForSecondsRealtime(.3f);
                if (index == 0) yield return Capture("星空之前");
                frames.Clear(); int collections = GC.CollectionCount(0); float started = Time.realtimeSinceStartup;
                bootstrap.PlayStarryBenchmark();
                if (!Check("星空事实立即成立并启动独立演出", bootstrap.Session.State.HasFact(ExplorationRules.WorldStarry) && sequence.IsPlaying)) yield break;
                float nextFrame = 0; int count = 0;
                do
                {
                    yield return null; frames.Add(Time.unscaledDeltaTime);
                    float elapsed = Time.realtimeSinceStartup - started;
                    if (capture && elapsed >= nextFrame)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, "StarryFrames", (count++).ToString("D4") + ".png")); nextFrame += .125f;
                    }
                    if (!CheckTimeout(elapsed <= sequence.Duration + 5)) yield break;
                }
                while (sequence.IsPlaying || (capture && Time.realtimeSinceStartup - started < sequence.Duration + 2));
                float seconds = Time.realtimeSinceStartup - started;
                bool final = sequence.IsFinalPresenting && !room.Ceiling.gameObject.activeSelf && !room.WallBack.gameObject.activeSelf &&
                    !room.Floor.gameObject.activeSelf && !room.WindowBoundary.gameObject.activeSelf && exterior.Environment.activeSelf && exterior.SkyDome.activeSelf &&
                    exterior.FloorPatch.activeSelf && mutation.SpawnedCount == 1 && SceneManager.GetActiveScene().name == sceneName;
                final &= retainedProps.Select((x, i) => Vector3.Distance(x.position, retainedPositions[i]) < .001f).All(x => x);
                float[] sorted = frames.ToArray(); Array.Sort(sorted);
                report.runs.Add(new RunReport { number = index + 1, success = final, seconds = seconds, frames = frames.Count, capturedFrames = count,
                    averageFps = frames.Count / seconds, p99Milliseconds = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .99f) - 1, 0, sorted.Length - 1)] * 1000,
                    maxMilliseconds = sorted[sorted.Length - 1] * 1000, gen0Collections = GC.CollectionCount(0) - collections, frameSeconds = frames.ToArray() });
                Save(); if (!Check("第 " + (index + 1) + " 轮结构重构、单个篝火与原家具保留", final)) yield break;
                if (index == 0) yield return Capture("星空世界");
            }
            // 同一原生程序再检查直接恢复和重开，不能只验证协程自然播完。
            bootstrap.JumpTo(ExplorationPresets.StarryWorldFinal); yield return null;
            if (!Check("最终预设即时恢复且篝火不重复", !sequence.IsPlaying && sequence.IsFinalPresenting && mutation.SpawnedCount == 1)) yield break;
            bootstrap.Restart(); yield return null;
            if (!Check("重开归还房间并清理生成物", room.Ceiling.gameObject.activeSelf && room.WallBack.gameObject.activeSelf && mutation.SpawnedCount == 0 && !exterior.Environment.activeSelf)) yield break;
            report.result = "Succeeded"; report.finishedUtc = DateTime.UtcNow.ToString("O"); Save();
            Debug.Log("探索技术复测完成，报告位于：" + OutputDirectory);
            if (quitWhenDone) UnityEngine.Application.Quit(0);
        }

        private bool RoomClick(SceneEntity entity)
        {
            Physics.SyncTransforms();
            var point = Camera.main.WorldToScreenPoint(entity.GetComponent<Collider>().bounds.center);
            return point.z > 0 && interaction.InspectAt(point);
        }
        private bool HotspotClick(string id)
        {
            var hotspot = inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(true).Single(x => x.ActionId == id);
            Physics.SyncTransforms(); return inspection.ObserveAt(Camera.main.WorldToScreenPoint(hotspot.transform.position));
        }
        private IEnumerator Capture(string label)
        {
            if (!capture) yield break;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, label + ".png"));
            // 等截图实际落盘后再换页，避免前一阶段图片混入下一阶段。
            yield return new WaitForSecondsRealtime(.18f);
        }
        private bool Check(string label, bool success)
        {
            report.checks.Add(new CheckReport { description = label, success = success });
            if (success) return true;
            report.result = "Failed"; report.error = label; report.finishedUtc = DateTime.UtcNow.ToString("O"); Save();
            Debug.LogError("探索技术复测失败：" + label); if (quitWhenDone) UnityEngine.Application.Quit(1); return false;
        }
        private bool CheckTimeout(bool withinLimit) => withinLimit || Check("星空演出未在限时内完成", false);
        private void Save() => File.WriteAllText(Path.Combine(OutputDirectory, capture ? "capture.json" : "review-10.json"), JsonUtility.ToJson(report, true));
        private void OnDestroy() { if (report != null && report.result == "Running") { report.result = "Interrupted"; Save(); } }

        /// <summary>机器字段稳定，记录运行环境与功能边界，不记录虚构真人评价。</summary>
        [Serializable] private sealed class Report
        {
            public string result, error, startedUtc, finishedUtc, mode, unity, platform, device, gpu;
            public int width, height, requestedRuns; public bool editor, caseACompleted;
            public List<CheckReport> checks = new List<CheckReport>(); public List<RunReport> runs = new List<RunReport>();
        }
        [Serializable] private sealed class CheckReport { public string description; public bool success; }
        /// <summary>原始帧间隔用于复核尖峰；截图模式含编码开销，不能充当无截图性能。</summary>
        [Serializable] private sealed class RunReport
        { public int number, frames, capturedFrames, gen0Collections; public bool success; public float seconds, averageFps, p99Milliseconds, maxMilliseconds; public float[] frameSeconds; }
    }
}
