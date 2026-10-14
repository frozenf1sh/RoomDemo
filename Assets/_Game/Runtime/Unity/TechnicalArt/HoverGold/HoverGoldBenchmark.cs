// 文件：HoverGoldBenchmark.cs
// 职责：提供隔离实验的临时管线、遮挡对照和可重复的运行截图。
// 边界：只用于本测试场景；恢复运行前管线，不修改项目资产或正式构建场景列表。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>无常驻 HUD 的悬停实验；快捷键说明放在配套文档。</summary>
    public sealed class HoverGoldBenchmark : MonoBehaviour
    {
        public RenderPipelineAsset Pipeline;
        public HoverGoldPointer Pointer;
        public HoverGoldTarget[] Targets;
        public GameObject Occluder;
        private RenderPipelineAsset previousDefault, previousQuality;
        private bool ownsPipeline, checking;
        private bool previousBackground;
        private int previousCaptureRate;
        private bool capturing;
        private readonly List<string> observations = new();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            previousBackground = Application.runInBackground;
            // 仅运行本实验时临时切换；资产及正式场景中的管线绑定保持不变。
            GraphicsSettings.defaultRenderPipeline = Pipeline;
            QualitySettings.renderPipeline = Pipeline;
            Application.runInBackground = true;
            ownsPipeline = true;
        }

        private void OnDisable()
        {
            if (capturing) Time.captureFramerate = previousCaptureRate;
            if (!ownsPipeline) return;
            GraphicsSettings.defaultRenderPipeline = previousDefault;
            QualitySettings.renderPipeline = previousQuality;
            Application.runInBackground = previousBackground;
            ownsPipeline = false;
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--hover-gold-check") BeginVerification(args[i + 1]);
                if (args[i] == "--hover-gold-film") BeginPreview(args[i + 1]);
            }
        }

        private void Update()
        {
            if (checking || Keyboard.current == null) return;
            if (Keyboard.current.oKey.wasPressedThisFrame) Occluder.SetActive(!Occluder.activeSelf);
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                foreach (var target in Targets) target.ResetEffect();
                Pointer.ResetEffect();
                Occluder.SetActive(false);
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame && !Application.isEditor) Application.Quit();
        }

        /// <summary>录制时间节点和遮挡对照；自动坐标只验证输入路径与视觉连续性。</summary>
        /// <param name="directory">本地忽略目录的绝对或相对路径。</param>
        public void BeginVerification(string directory)
        {
            if (checking) return;
            checking = true;
            StartCoroutine(Verify(directory));
        }

        /// <summary>逐帧保存真实渲染的十六秒预览，停留足够久以展示完整呼吸周期。</summary>
        /// <param name="directory">仅用于本机取证的帧目录。</param>
        public void BeginPreview(string directory)
        {
            if (checking) return;
            checking = true;
            StartCoroutine(Preview(directory));
        }

        private IEnumerator Preview(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var target in Targets) target.ResetEffect();
            Occluder.SetActive(false);
            Pointer.SetVerificationPoint(Vector2.zero);
            // 等待播放器启动淡化结束，避免启动遮罩混入悬停渐变的预览。
            yield return new WaitForSecondsRealtime(.8f);
            previousCaptureRate = Time.captureFramerate;
            capturing = true;
            Time.captureFramerate = 24;
            // 每个物品都经相同射线拾取；实际 Shader 和渐变逐帧运行，不对静帧做后期淡化。
            for (int frame = 0; frame < 384; frame++)
            {
                int index = frame < 12 || frame >= 360 ? -1 : frame < 204 ? 0 : frame < 276 ? 1 : 2;
                Vector2 point = Vector2.zero;
                if (index >= 0 && Targets[index].TryGetBounds(out var bounds))
                    point = Pointer.View.WorldToScreenPoint(bounds.center);
                Pointer.SetVerificationPoint(point);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, frame.ToString("D4") + ".png"));
            }
            Time.captureFramerate = previousCaptureRate;
            capturing = false;
            Pointer.ResetEffect();
            yield return new WaitForSecondsRealtime(.25f);
            checking = false;
            Debug.Log("悬停金光预览帧采集完成：" + directory);
            if (!Application.isEditor) Application.Quit();
        }

        private IEnumerator Save(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            observations.Add(name + "：" + string.Join(" / ", Array.ConvertAll(Targets,
                x => x.name + "=" + x.Opacity.ToString("F3"))));
            yield return new WaitForSecondsRealtime(.12f);
        }

        private IEnumerator Verify(string directory)
        {
            Directory.CreateDirectory(directory);
            observations.Clear();
            foreach (var target in Targets) target.ResetEffect();
            Occluder.SetActive(false);
            Pointer.SetVerificationPoint(Vector2.zero);
            yield return new WaitForSecondsRealtime(.8f);
            yield return Save(directory, "00_无悬停");
            for (int i = 0; i < Targets.Length; i++)
            {
                if (!Targets[i].TryGetBounds(out var bounds)) throw new InvalidOperationException("实验物品缺少可见模型。");
                var point = (Vector2)Pointer.View.WorldToScreenPoint(bounds.center);
                if (Pointer.Pick(point) != Targets[i]) throw new InvalidOperationException("实验射线未命中物品：" + Targets[i].name);
                Pointer.SetVerificationPoint(point);
                yield return new WaitForSecondsRealtime(.25f);
                yield return Save(directory, (i + 1) + "a_移入初段");
                yield return new WaitForSecondsRealtime(.28f);
                yield return Save(directory, (i + 1) + "b_柔和浮现");
                yield return new WaitForSecondsRealtime(.85f);
                yield return Save(directory, (i + 1) + "c_完整细线");
                yield return new WaitForSecondsRealtime(2);
                yield return Save(directory, (i + 1) + "d_流光移动");
                Pointer.SetVerificationPoint(Vector2.zero);
                yield return new WaitForSecondsRealtime(.24f);
                yield return Save(directory, (i + 1) + "e_移开消退");
                yield return new WaitForSecondsRealtime(.55f);
            }
            // 先悬停后加立板：已有编织光覆盖前景后消退，但新悬停的拾取仍受立板阻挡。
            Targets[2].TryGetBounds(out var lampBounds);
            Vector2 lampPoint = Pointer.View.WorldToScreenPoint(lampBounds.center);
            Pointer.SetVerificationPoint(lampPoint);
            yield return new WaitForSecondsRealtime(1.5f);
            Occluder.SetActive(true);
            yield return Save(directory, "04_前景遮挡");
            yield return new WaitForSecondsRealtime(.7f);
            yield return Save(directory, "05_遮挡后消退");
            observations.Add("遮挡射线=" + (Pointer.Pick(lampPoint) == null ? "阻断" : "未阻断"));
            Occluder.SetActive(false);
            Pointer.SetVerificationPoint(Vector2.zero);
            yield return new WaitForSecondsRealtime(.7f);
            Targets[0].TryGetBounds(out var cupBounds);
            Pointer.SetVerificationPoint(Pointer.View.WorldToScreenPoint(cupBounds.center));
            yield return new WaitForSecondsRealtime(.06f);
            Pointer.SetVerificationPoint(Vector2.zero);
            yield return new WaitForSecondsRealtime(.1f);
            yield return Save(directory, "06_快速划过");
            Pointer.SetVerificationPoint(Pointer.View.WorldToScreenPoint(cupBounds.center));
            yield return new WaitForSecondsRealtime(1.5f);
            Pointer.InputBlocked = true;
            yield return new WaitForSecondsRealtime(.7f);
            yield return Save(directory, "07_输入阻断");
            Pointer.InputBlocked = false;
            // 切换时旧目标继续消退，新目标从零浮现；关闭目标后静态登记立即移除。
            Pointer.SetVerificationPoint(Pointer.View.WorldToScreenPoint(cupBounds.center));
            yield return new WaitForSecondsRealtime(1.5f);
            Targets[1].TryGetBounds(out var bookBounds);
            Pointer.SetVerificationPoint(Pointer.View.WorldToScreenPoint(bookBounds.center));
            yield return new WaitForSecondsRealtime(.2f);
            yield return Save(directory, "08_目标切换");
            Targets[1].enabled = false;
            yield return new WaitForSecondsRealtime(.7f);
            yield return Save(directory, "09_禁用目标");
            Targets[1].enabled = true;
            Pointer.SetVerificationPoint(null);
            observations.Add("验证性质=自动坐标与真实运行截图；尚无真人审美及鼠标验收。");
            File.WriteAllLines(Path.Combine(directory, "运行观察.txt"), observations);
            checking = false;
            Debug.Log("悬停金光实验采集完成：" + directory);
            if (!Application.isEditor) Application.Quit();
        }
    }
}
