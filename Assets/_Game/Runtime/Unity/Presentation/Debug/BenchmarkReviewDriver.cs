// =============================================================================
// 文件：BenchmarkReviewDriver.cs
// 职责：显式启动独立程序的十轮技术复测或一轮低帧率演出取样，并记录可核验报告。
// 边界：仅 F9/F10 调试入口使用既有预设与正式 Notebook API；不替代玩家手点流程
//       或陌生玩家评审，不决定任何世界规则，不在通常游戏中自动运行。
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity
{
    /// <summary>这一个演出样片的具体复测器，无泛化测试框架或运行时服务定位。</summary>
    public sealed class BenchmarkReviewDriver : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private PaintingSeaSequence sequence;
        [SerializeField] private PaintingPresenter painting;
        [SerializeField] private WaterPresenter water;
        [SerializeField] private PlantPresenter plant;
        [SerializeField] private WindowPresenter window;
        [SerializeField] private EndingPresenter ending;
        private Coroutine running;
        private ReviewReport report;
        private readonly List<float> frames = new List<float>(8192);
        /// <summary>报告使用 Unity 的可写用户目录，不依赖启动程序时的工作目录。</summary>
        public string OutputDirectory => Path.Combine(UnityEngine.Application.persistentDataPath, "BenchmarkReview");
        /// <summary>由场景工具显式绑定全部引用，构建验证器会检查缺失绑定。</summary>
        public void Configure(GameBootstrap game, NotebookView book, PaintingSeaSequence director,
            PaintingPresenter art, WaterPresenter drops, PlantPresenter leaves, WindowPresenter exterior, EndingPresenter atmosphere)
        { bootstrap = game; notebook = book; sequence = director; painting = art; water = drops; plant = leaves; window = exterior; ending = atmosphere; }
        /// <summary>原生构建也可以由显式命令行参数启动，不需要注入或模拟操作系统输入。</summary>
        private void Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-roomDemoReview") >= 0) StartReview(false);
            else if (Array.IndexOf(arguments, "-roomDemoCapture") >= 0) StartReview(true);
        }
        /// <summary>只接受开发者明确的复测按键；没有每帧世界规则检查。</summary>
        private void Update()
        {
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                if (running != null) { StopCoroutine(running); running = null; report.result = "Cancelled"; Save(); sequence.ApplyFinalStateImmediately(); }
                else StartReview(false);
            }
            if (keyboard.f10Key.wasPressedThisFrame && running == null) StartReview(true);
        }
        /// <summary>记录本机与分辨率；截图模式不能作为无截图性能结果使用。</summary>
        private void StartReview(bool capture)
        {
            report = new ReviewReport { result = "Running", mode = capture ? "演出取样（含截图开销，无音轨）" : "十轮技术复测（无截图）",
                startedUtc = DateTime.UtcNow.ToString("O"), unity = UnityEngine.Application.unityVersion,
                platform = UnityEngine.Application.platform.ToString(), device = SystemInfo.deviceModel,
                gpu = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height,
                editor = UnityEngine.Application.isEditor, requestedRuns = capture ? 1 : 10 };
            Directory.CreateDirectory(OutputDirectory);
            if (capture)
            {
                Directory.CreateDirectory(Path.Combine(OutputDirectory, "Frames"));
                // 只删除复测器自己的上一轮 PNG，避免旧帧混入新视频。
                foreach (var path in Directory.GetFiles(Path.Combine(OutputDirectory, "Frames"), "*.png")) File.Delete(path);
            }
            Save(); running = StartCoroutine(Run(capture));
        }
        /// <summary>每轮从正式预设开始，经 Notebook 提交配方，等待真实演出完成。</summary>
        private IEnumerator Run(bool capture)
        {
            if (capture)
            {
                // 独立命令行取样从新游戏启动，因此先留一张真正的初始画面，再跳调试预设。
                // F10 手动入口记录的是按键当时的画面，不能把它误称为新游戏截图。
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, "capture-start.png"));
                yield return new WaitForSecondsRealtime(.3f);
            }
            for (int index = 0; index < report.requestedRuns; index++)
            {
                bootstrap.JumpTo(DebugPresets.BeforeSea);
                notebook.SetOpen(true); notebook.SelectClue(new ClueId("sea")); notebook.SelectClue(new ClueId("painting"));
                yield return new WaitForSecondsRealtime(.4f);
                GameSession expectedSession = bootstrap.Session;
                frames.Clear(); int gc = GC.CollectionCount(0); float started = Time.realtimeSinceStartup;
                notebook.InterpretSelected();
                if (!expectedSession.State.HasFact(DemoRules.Complete) || !sequence.IsPlaying)
                { Fail("正式组合没有产生领域终态或启动演出。"); yield break; }
                float nextCapture = 0; int frameIndex = 0;
                do
                {
                    yield return null;
                    if (bootstrap.Session != expectedSession) { Fail("复测被重开或预设切换中断。"); yield break; }
                    frames.Add(Time.unscaledDeltaTime);
                    float elapsed = Time.realtimeSinceStartup - started;
                    if (capture && elapsed >= nextCapture)
                    {
                        // PNG 取样限制为每秒 8 张。性能统计明确标记包含截图开销。
                        ScreenCapture.CaptureScreenshot(Path.Combine(OutputDirectory, "Frames", (frameIndex++).ToString("D4") + ".png"));
                        nextCapture += .125f;
                    }
                    if (elapsed > sequence.Duration + 8) { Fail("演出超时，可能协程未结束。"); yield break; }
                }
                while (sequence.IsPlaying || (capture && Time.realtimeSinceStartup - started < sequence.Duration + 3));
                float duration = Time.realtimeSinceStartup - started;
                bool final = !sequence.IsBenchmarkPending && painting.TransitionProgress >= .999f && water.RevealProgress >= .999f
                    && plant.AquaticProgress >= .999f && window.OceanProgress >= .999f && ending.AtmosphereProgress >= .999f
                    && ending.EndingPanel.activeSelf && expectedSession.State.HasFact(DemoRules.Complete);
                float[] sorted = frames.ToArray(); Array.Sort(sorted);
                // 原始帧间隔随报告保存；第 99 百分位与最大值能暴露平均帧率掩盖的尖峰。
                report.runs.Add(new ReviewRun { number = index + 1, success = final, seconds = duration, frames = frames.Count,
                    averageFps = frames.Count / duration, p99Milliseconds = sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .99f) - 1, 0, sorted.Length - 1)] * 1000,
                    maxMilliseconds = sorted[sorted.Length - 1] * 1000, gen0Collections = GC.CollectionCount(0) - gc,
                    facts = expectedSession.State.Facts.Count, reactions = expectedSession.State.FiredReactions.Count,
                    recipes = expectedSession.State.CompletedRecipes.Count, capturedFrames = frameIndex, frameSeconds = frames.ToArray() });
                Save();
                if (!final) { Fail("最终视觉进度或终态提示未恢复到位。"); yield break; }
                Debug.Log("演出技术复测：第 " + (index + 1) + " 轮通过。");
                yield return new WaitForSecondsRealtime(.3f);
            }
            report.result = "Succeeded"; report.finishedUtc = DateTime.UtcNow.ToString("O"); Save(); running = null;
            Debug.Log("演出技术复测完成，报告位于：" + OutputDirectory);
        }
        /// <summary>失败即停止，不把部分成功报告为整组通过。</summary>
        private void Fail(string reason) { report.result = "Failed"; report.error = reason; Save(); running = null; Debug.LogError("演出技术复测失败：" + reason); }
        /// <summary>每轮结束落盘，崩溃或被打断时仍保留已完成的证据。</summary>
        private void Save()
        {
            string file = report.requestedRuns == 10 ? "review-10.json" : "capture.json";
            File.WriteAllText(Path.Combine(OutputDirectory, file), JsonUtility.ToJson(report, true));
        }
        /// <summary>退出期间保留中断标记；不恢复或修改世界事实。</summary>
        private void OnDestroy() { if (report != null && report.result == "Running") { report.result = "Interrupted"; Save(); } }
        /// <summary>机器结果保留稳定字段，说明文字使用中文。</summary>
        [Serializable] private sealed class ReviewReport
        { public string result, mode, startedUtc, finishedUtc, unity, platform, device, gpu, error; public int width, height, requestedRuns; public bool editor; public List<ReviewRun> runs = new List<ReviewRun>(); }
        /// <summary>单轮功能终态与帧间隔；不宣称视觉品质或普通 PC 的帧率。</summary>
        [Serializable] private sealed class ReviewRun
        { public int number, frames, gen0Collections, facts, reactions, recipes, capturedFrames; public bool success; public float seconds, averageFps, p99Milliseconds, maxMilliseconds; public float[] frameSeconds; }
    }
}
