// 文件：Painterly3DBenchmark.cs
// 职责：独立静物场景的普通／绘画对照、摄像机环绕与实际播放器截图。
// 边界：只做 TA 验证；不进入游戏路线，不写领域事实，退出时恢复原渲染管线。
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>运行时临时安装 Benchmark 管线，并管理纯表现层的检查视角。</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Painterly3DBenchmark : MonoBehaviour
    {
        [SerializeField] private UniversalRenderPipelineAsset benchmarkPipeline;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PainterlyCamera painterly;
        [SerializeField] private Canvas controls;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Slider intensitySlider;
        private RenderPipelineAsset oldDefault, oldQuality;
        private bool pipelineInstalled, orbit, captureRunning;
        private float orbitTime, azimuth = 8;
        private const float Distance = 5.3f;
        private static readonly Vector3 Target = new Vector3(0,1.4f,0);

        /// <summary>由 Editor 生成器保存场景引用，不改变项目全局设置资产。</summary>
        public void Configure(UniversalRenderPipelineAsset pipeline, Camera camera, PainterlyCamera effect,
            Canvas canvas, Text label, Slider slider)
        {
            benchmarkPipeline = pipeline;
            viewCamera = camera;
            painterly = effect;
            controls = canvas;
            stateLabel = label;
            intensitySlider = slider;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || benchmarkPipeline == null) return;
            // 用独立管线运行实验；保留原值，停止实验时按所有权检查恢复。
            oldDefault = GraphicsSettings.defaultRenderPipeline;
            oldQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = benchmarkPipeline;
            QualitySettings.renderPipeline = benchmarkPipeline;
            pipelineInstalled = true;
        }

        private void OnDisable()
        {
            if (!pipelineInstalled) return;
            if (GraphicsSettings.defaultRenderPipeline == benchmarkPipeline) GraphicsSettings.defaultRenderPipeline = oldDefault;
            if (QualitySettings.renderPipeline == benchmarkPipeline) QualitySettings.renderPipeline = oldQuality;
            pipelineInstalled = false;
        }

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            UpdateCamera();
            UpdateLabel();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--painterly-capture") StartCoroutine(Capture(args[i+1]));
            }
        }

        private void Update()
        {
            if (captureRunning) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) ToggleOrbit();
                if (keyboard.pKey.wasPressedThisFrame) ShowPainted();
                if (keyboard.oKey.wasPressedThisFrame) ShowOriginal();
                if (keyboard.dKey.wasPressedThisFrame) ToggleDebug();
                if (keyboard.hKey.wasPressedThisFrame) controls.enabled = !controls.enabled;
                if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit();
                if (keyboard.leftArrowKey.isPressed) azimuth -= Time.unscaledDeltaTime * 15;
                if (keyboard.rightArrowKey.isPressed) azimuth += Time.unscaledDeltaTime * 15;
            }
            if (orbit)
            {
                orbitTime += Time.unscaledDeltaTime;
                azimuth = 8 + Mathf.Sin(orbitTime * 0.25f) * 21;
            }
            UpdateCamera();
        }

        /// <summary>显示完全普通的 3D 帧，保留相同材质、构图和光照。</summary>
        public void ShowOriginal() => SetIntensity(0);

        /// <summary>显示完整绘画处理，并清除上一个状态的 TAA 历史。</summary>
        public void ShowPainted() => SetIntensity(1);

        /// <summary>控制统一 PainterlyIntensity，重置 TAA 避免对照串帧。</summary>
        public void SetIntensity(float value)
        {
            painterly.SetPainterlyIntensity(value);
            if (intensitySlider != null) intensitySlider.SetValueWithoutNotify(value);
            viewCamera.GetUniversalAdditionalCameraData().resetHistory = true;
            UpdateLabel();
        }

        /// <summary>开始／冻结慢速环绕，不修改任何物体变换。</summary>
        public void ToggleOrbit()
        {
            orbit = !orbit;
            UpdateLabel();
        }

        /// <summary>显示固定世界格子的颜色，用于观察笔触是否随视角滑动。</summary>
        public void ToggleDebug()
        {
            painterly.DebugWorldCells = !painterly.DebugWorldCells;
            viewCamera.GetUniversalAdditionalCameraData().resetHistory = true;
            UpdateLabel();
        }

        private void UpdateCamera()
        {
            float angle = azimuth * Mathf.Deg2Rad;
            viewCamera.transform.position = Target + new Vector3(Mathf.Sin(angle)*Distance,2.45f,Mathf.Cos(angle)*Distance);
            viewCamera.transform.LookAt(Target);
        }

        private void UpdateLabel()
        {
            if (stateLabel == null) return;
            stateLabel.text = painterly.DebugWorldCells ? "世界空间锚点 · 移动镜头检查" :
                painterly.PainterlyIntensity < 0.01f ? "普通材质 · 相同光照与构图" : "实时绘画 · 世界空间笔触";
            stateLabel.text += orbit ? "    /    镜头环绕中" : "    /    镜头已冻结";
        }

        /// <summary>记录真实构建的对照帧和环绕帧；只在显式命令行参数下执行。</summary>
        private IEnumerator Capture(string directory)
        {
            captureRunning = true;
            Directory.CreateDirectory(directory);
            controls.enabled = false;
            azimuth = 8;
            UpdateCamera();
            ShowOriginal();
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"01_普通3D.png"));
            yield return new WaitForSecondsRealtime(0.4f);
            ShowPainted();
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"02_实时绘画.png"));
            controls.enabled = true;
            yield return new WaitForSecondsRealtime(0.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"03_控制面板.png"));
            controls.enabled = false;
            var frames = Path.Combine(directory,"orbit");
            Directory.CreateDirectory(frames);
            float start = Time.realtimeSinceStartup;
            float next = start;
            int frame = 0;
            using (var times = new StreamWriter(Path.Combine(frames,"times.csv")))
            {
                times.WriteLine("frame,time");
                // 以真实时间运行摄像机，再按实际时间戳编码；不把慢帧加速伪装成实时。
                while (Time.realtimeSinceStartup-start < 12)
                {
                    float elapsed = Time.realtimeSinceStartup-start;
                    azimuth = 8 + Mathf.Sin(elapsed/12*Mathf.PI*2)*20;
                    UpdateCamera();
                    yield return new WaitForEndOfFrame();
                    if (Time.realtimeSinceStartup >= next)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(frames,frame.ToString("D4")+".png"));
                        times.WriteLine(frame+","+(Time.realtimeSinceStartup-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
                        frame++;
                        next += 1f/24;
                        // 记录真实帧间隔；GPU 或截图变慢时不补画假帧、不加速相机。
                        if (next < Time.realtimeSinceStartup) next = Time.realtimeSinceStartup + 1f/24;
                    }
                }
            }
            painterly.DebugWorldCells = true;
            ShowPainted();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"04_世界锚点调试.png"));
            yield return new WaitForSecondsRealtime(1);
            Debug.Log("实时绘画 Benchmark 截图完成："+directory);
            Application.Quit();
        }
    }
}
