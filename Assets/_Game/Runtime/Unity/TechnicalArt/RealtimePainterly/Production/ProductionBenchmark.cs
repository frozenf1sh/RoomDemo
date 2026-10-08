// 文件：ProductionBenchmark.cs
// 职责：在独立场景中演示生产组件事件入口、镜头慢转、UI 与真实播放器采集。
// 边界：只用于 Benchmark，不包含游戏路线，不随生产 Prefab 进入主游戏。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>独立验证外壳；运行时临时管线只属于本实验，生产效果不换全局管线。</summary>
    public sealed class ProductionBenchmark : MonoBehaviour
    {
        public RenderPipelineAsset Pipeline;
        public Camera View;
        public PainterlyStyleController Style;
        public PainterlyLocalController Local, Region;
        public LivingSkyController Sky;
        public LivingWaterController Water;
        public PainterlyRewriteController Rewrite;
        public Canvas Controls;
        public Text Status;
        public Button[] Buttons;
        public bool RotateCamera;
        private RenderPipelineAsset previousDefault, previousQuality;
        private Quaternion initialRotation;
        private Vector3 initialPosition;
        private bool ownsPipeline, paused, checking;
        private int previousVsync, previousFrameRate;
        private bool previousBackground;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            previousVsync = QualitySettings.vSyncCount;
            previousFrameRate = Application.targetFrameRate;
            previousBackground = Application.runInBackground;
            GraphicsSettings.defaultRenderPipeline = Pipeline;
            QualitySettings.renderPipeline = Pipeline;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            ownsPipeline = true;
        }

        private void Start()
        {
            initialRotation = View.transform.rotation;
            initialPosition = View.transform.position;
            Sky.BindCamera(View);
            Local.BindCamera(View);
            Region.BindCamera(View);
            BindButtons();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--production-check") StartCoroutine(Check(args[i + 1]));
        }

        private void BindButtons()
        {
            // 按钮直接调用公开组件 API；不存在模拟游戏事实或隐藏的事件总线。
            Buttons[0].onClick.AddListener(Style.ShowReality);
            Buttons[1].onClick.AddListener(Style.ShowTouched);
            Buttons[2].onClick.AddListener(Style.ShowDream);
            Buttons[3].onClick.AddListener(Style.ShowFinal);
            Buttons[4].onClick.AddListener(TouchLocal);
            Buttons[5].onClick.AddListener(GrowRegion);
            Buttons[6].onClick.AddListener(Sky.ShowLocal);
            Buttons[7].onClick.AddListener(Sky.StartFlow);
            Buttons[8].onClick.AddListener(Sky.ExpandFull);
            Buttons[9].onClick.AddListener(Water.Swirl);
            Buttons[10].onClick.AddListener(Water.StartOverflow);
            Buttons[11].onClick.AddListener(Water.Stop);
            Buttons[12].onClick.AddListener(Rewrite.Open);
            Buttons[13].onClick.AddListener(ResetAll);
            Buttons[14].onClick.AddListener(() => RotateCamera = !RotateCamera);
            Buttons[15].onClick.AddListener(() => SetPaused(!paused));
        }

        private void Update()
        {
            if (RotateCamera && !paused)
                View.transform.Rotate(Vector3.up, .45f * Time.deltaTime, Space.World);
            if (Status != null)
                Status.text = "全局 " + Style.PainterlyIntensity.ToString("F2") + "  局部 " + Local.PainterlyIntensity.ToString("F2") +
                    "  天空 " + Sky.Coverage.ToString("F2") + "  水流 " + Water.Flow.ToString("F2");
            if (checking || Keyboard.current == null) return;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) SetPaused(!paused);
            if (Keyboard.current.rKey.wasPressedThisFrame) ResetAll();
            if (Keyboard.current.cKey.wasPressedThisFrame) RotateCamera = !RotateCamera;
            if (Keyboard.current.hKey.wasPressedThisFrame) Controls.gameObject.SetActive(!Controls.gameObject.activeSelf);
            if (Keyboard.current.escapeKey.wasPressedThisFrame) Application.Quit();
        }

        private void TouchLocal() => Local.TransitionTo(.75f, 3);
        private void GrowRegion()
        {
            Region.SetPainterlyIntensity(.62f);
            Region.ExpandTo(1, 6);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            Style.Paused = Local.Paused = Region.Paused = Rewrite.Paused = value;
            Sky.SetPaused(value);
            Water.SetPaused(value);
        }

        /// <summary>重置本实验所有组件，取消旧渐变，保留独立场景的普通材质。</summary>
        public void ResetAll()
        {
            Style.SetPainterlyIntensity(.06f);
            Local.SetPainterlyIntensity(0);
            Region.SetPainterlyIntensity(0);
            Region.ExpandTo(.02f, 0);
            Sky.ResetEffect(); Water.ResetEffect(); Rewrite.ResetEffect();
            View.transform.SetPositionAndRotation(initialPosition, initialRotation);
            View.GetUniversalAdditionalCameraData().resetHistory = true;
            RotateCamera = false;
            SetPaused(false);
        }

        private IEnumerator SaveFrame(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }

        private IEnumerator Check(string directory)
        {
            checking = true;
            Directory.CreateDirectory(directory);
            ResetAll();
            yield return new WaitForSecondsRealtime(1);
            yield return SaveFrame(directory, "01_轻现实与清晰UI");
            TouchLocal(); Sky.ShowLocal();
            yield return new WaitForSecondsRealtime(5.2f);
            yield return SaveFrame(directory, "02_局部绘画与天空");
            Sky.StartFlow(); Water.Swirl();
            yield return new WaitForSecondsRealtime(4);
            yield return SaveFrame(directory, "03_杯水旋流");
            GrowRegion(); Water.StartOverflow(); Rewrite.Open();
            yield return new WaitForSecondsRealtime(6.5f);
            yield return SaveFrame(directory, "04_溢出与笔势擦开");
            // 用同一 API 检查中断：Final 进行一半时改回 Dream，再进入 Final。
            Style.TransitionTo(PainterlyStage.Final, 2);
            yield return new WaitForSecondsRealtime(.7f);
            Style.TransitionTo(PainterlyStage.Dream, .8f);
            yield return new WaitForSecondsRealtime(1);
            if (Mathf.Abs(Style.PainterlyIntensity - .62f) > .002f) throw new InvalidOperationException("风格中断没有到达最后目标。");
            Style.ShowFinal(); Sky.ExpandFull();
            yield return new WaitForSecondsRealtime(5.2f);
            yield return SaveFrame(directory, "05_完整绘画世界");
            Water.Stop();
            yield return new WaitForSecondsRealtime(3);
            float cupTime = Water.Cup.VisualTime, wetTime = Water.Wet.VisualTime;
            yield return new WaitForSecondsRealtime(1);
            if (Water.Flow > .001f || Water.OverflowAmount > .001f || Water.Cup.VisualTime != cupTime || Water.Wet.VisualTime != wetTime)
                throw new InvalidOperationException("水停止后仍在推进颜料历史。");
            yield return SaveFrame(directory, "06_停止并保留湿痕");
            SetPaused(true);
            float skyTime = Sky.Pigment.VisualTime;
            yield return new WaitForSecondsRealtime(.5f);
            if (skyTime != Sky.Pigment.VisualTime) throw new InvalidOperationException("天空暂停后仍在推进历史。");
            // 静止颜料隔离镜头稳定性，八秒慢转与平移观察锚定效果。
            RotateCamera = true;
            SetPaused(false);
            Sky.SetFlow(0);
            string frames = Path.Combine(directory, "camera"); Directory.CreateDirectory(frames);
            float start = Time.realtimeSinceStartup, next = start;
            int frame = 0;
            var cameraSamples = new List<string> { "file,seconds,x,y,z,yaw" };
            while (Time.realtimeSinceStartup - start < 8)
            {
                View.transform.position = initialPosition + Vector3.right * Mathf.Sin((Time.realtimeSinceStartup - start) * .25f) * .12f;
                yield return new WaitForEndOfFrame();
                if (Time.realtimeSinceStartup < next) continue;
                string file = (frame++).ToString("D4") + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(frames, file));
                Vector3 p = View.transform.position;
                cameraSamples.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}",
                    file, Time.realtimeSinceStartup - start, p.x, p.y, p.z, View.transform.eulerAngles.y));
                next += 1f / 12;
            }
            File.WriteAllLines(Path.Combine(directory, "镜头采样.csv"), cameraSamples);
            yield return SaveFrame(directory, "07_慢镜头与世界锚定");
            RotateCamera = false;
            // 性能段不截图；正常真实时间更新两个 GPU 状态与全局绘画。
            Sky.SetFlow(1); Water.StartOverflow();
            yield return new WaitForSecondsRealtime(2.2f);
            var timings = new List<float>(); start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 5)
            {
                timings.Add(Time.unscaledDeltaTime * 1000); yield return null;
            }
            timings.Sort();
            string result = "{\"median_ms\":" + timings[timings.Count / 2].ToString("F3", CultureInfo.InvariantCulture) +
                ",\"p95_ms\":" + timings[Mathf.FloorToInt(timings.Count * .95f)].ToString("F3", CultureInfo.InvariantCulture) +
                ",\"frames\":" + timings.Count + ",\"width\":" + Screen.width + ",\"height\":" + Screen.height +
                ",\"gpu\":\"" + SystemInfo.graphicsDeviceName + "\",\"state_checks\":\"passed\"}";
            File.WriteAllText(Path.Combine(directory, "性能与控制.json"), result);
            Screen.SetResolution(800, 450, false);
            yield return new WaitForSecondsRealtime(1);
            yield return SaveFrame(directory, "08_小窗口中文UI");
            ResetAll();
            yield return new WaitForSecondsRealtime(.5f);
            yield return SaveFrame(directory, "09_重置回现实");
            Debug.Log("生产组件独立验证完成：渐变中断、暂停、停水、重置、慢镜头与真实构建中文 UI。");
            Application.Quit();
        }

        private void OnDisable()
        {
            if (!ownsPipeline) return;
            if (GraphicsSettings.defaultRenderPipeline == Pipeline) GraphicsSettings.defaultRenderPipeline = previousDefault;
            if (QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previousQuality;
            QualitySettings.vSyncCount = previousVsync;
            Application.targetFrameRate = previousFrameRate;
            Application.runInBackground = previousBackground;
            ownsPipeline = false;
        }
    }
}
