// =============================================================================
// 文件：VisualSprintCapture.cs
// 职责：通过显式命令行入口录下三个实际运行的美术片段与前后对比截图。
// 边界：只在开发者指定录制参数时运行；复用应用入口，不伪装真人试玩或写性能报告。
// =============================================================================
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity
{
    /// <summary>这轮美术交付的具体录像导演；正常启动不会运行或显示任何控件。</summary>
    public sealed class VisualSprintCapture : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private DayTransitionController day;
        [SerializeField] private InspectableObject book;
        [SerializeField] private PlantPresenter plant;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private VisualSprintAudioCapture sound;
        private string output;
        private bool recording;
        private string clip;
        private int frame;
        private float nextFrame;
        private const float FramesPerSecond = 24;
        /// <summary>显式绑定现有入口，不添加谜题或修改领域内容。</summary>
        public void Configure(GameBootstrap game, NotebookView notes, InspectionController detail, DayTransitionController days,
            InspectableObject bookObject, PlantPresenter pottedPlant, Camera camera, VisualSprintAudioCapture audio)
        { bootstrap = game; notebook = notes; inspection = detail; day = days; book = bookObject; plant = pottedPlant; viewCamera = camera; sound = audio; }
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-roomDemoVisualCapture");
            if (index < 0) return;
            output = index + 1 < args.Length ? args[index + 1] : Path.Combine(UnityEngine.Application.persistentDataPath, "VisualSprint");
            Directory.CreateDirectory(output);
            // 录制仅暂停本进程的输入设备，检视仍正常更新镜头、提示淡出与视觉生命周期。
            if (Mouse.current != null) InputSystem.DisableDevice(Mouse.current);
            if (Keyboard.current != null) InputSystem.DisableDevice(Keyboard.current);
            sound.enabled = true;
            StartCoroutine(RecordShots());
        }
        private IEnumerator RecordShots()
        {
            // 等待首次教学淡出；对比截图是实际玩家画面，包含真实 HUD 显隐。
            bootstrap.Restart(); yield return new WaitForSecondsRealtime(4.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "重制后_工作室.png"));
            yield return new WaitForSecondsRealtime(.3f);
            BeginClip("书本与灵感");
            inspection.Enter(book); yield return new WaitForSecondsRealtime(.9f);
            yield return new WaitForSecondsRealtime(1.1f);
            inspection.ActOnHotspot("cover"); yield return new WaitForSecondsRealtime(3);
            inspection.ActOnHotspot("next1"); yield return new WaitForSecondsRealtime(2);
            var quote = inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(true).Single(x => x.ActionId == "quote");
            quote.SetHover(true); yield return new WaitForSecondsRealtime(2);
            inspection.ActOnHotspot("quote"); yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "重制后_书本灵感.png"));
            nextFrame = Time.unscaledTime + 1 / FramesPerSecond;
            yield return new WaitForSecondsRealtime(2); EndClip();
            inspection.Exit(false);
            bootstrap.JumpTo(ExplorationPresets.GrowthUnlocked);
            yield return new WaitForSecondsRealtime(.5f);
            // 镜头仅用于交付特写，不改变正常玩家固定机位或修改任何事实。
            var originalPosition = viewCamera.transform.localPosition; var originalRotation = viewCamera.transform.localRotation;
            float originalFov = viewCamera.fieldOfView;
            viewCamera.transform.position = plant.transform.position + new Vector3(0, 1.5f, -4.1f);
            viewCamera.transform.LookAt(plant.transform.position + Vector3.up * 1.5f); viewCamera.fieldOfView = 50;
            BeginClip("植物生长"); yield return new WaitForSecondsRealtime(1);
            notebook.SetOpen(true); notebook.SelectClue(DemoRules.Clue("growth")); notebook.SelectClue(DemoRules.Clue("plant"));
            yield return new WaitForSecondsRealtime(1.2f); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(7.5f);
            day.Begin(); yield return new WaitForSecondsRealtime(3.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "重制后_植物.png"));
            nextFrame = Time.unscaledTime + 1 / FramesPerSecond;
            yield return new WaitForSecondsRealtime(3); EndClip();
            viewCamera.transform.localPosition = originalPosition; viewCamera.transform.localRotation = originalRotation; viewCamera.fieldOfView = originalFov;
            bootstrap.JumpTo(ExplorationPresets.BeforeStarryWorld); yield return new WaitForSecondsRealtime(.7f);
            BeginClip("星空世界重构"); yield return new WaitForSecondsRealtime(1);
            bootstrap.PlayStarryBenchmark(); yield return new WaitForSecondsRealtime(20);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "重制后_星空世界.png"));
            // 同帧只允许一个截图请求，避免逐帧录像覆盖独立静帧的目标路径。
            nextFrame = Time.unscaledTime + 1 / FramesPerSecond;
            yield return new WaitForSecondsRealtime(2); EndClip();
            File.WriteAllText(Path.Combine(output, "录制完成.txt"), "三个片段均来自实际运行画面和监听器混音，24帧每秒取样；不是陌生玩家试玩。\n");
            Debug.Log("美术录像已完成：" + output);
            if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(0);
        }
        private void BeginClip(string name)
        {
            clip = Path.Combine(output, name); Directory.CreateDirectory(clip);
            frame = 0; nextFrame = Time.unscaledTime; recording = true;
            sound.Begin();
        }
        private void EndClip() { recording = false; sound.Finish(Path.Combine(clip, "游戏混音.wav")); }
        private void LateUpdate()
        {
            // 临时 PNG 在仓库外编码成影片；不输出几百帧验收报告或帧率表。
            if (!recording || Time.unscaledTime < nextFrame) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(clip, (frame++).ToString("D5") + ".png"));
            nextFrame += 1 / FramesPerSecond;
        }
    }
}
