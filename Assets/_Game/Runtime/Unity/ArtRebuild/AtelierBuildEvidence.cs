// 文件：AtelierBuildEvidence.cs
// 职责：在显式命令行取证模式下保存独立构建的真实摄像机画面与流动对照。
// 架构边界：显式取证时切换美术预览状态；不修改 GameState、交互、碰撞或正常运行流程。
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;

namespace Game.Unity.ArtRebuild
{
    /// <summary>正常运行时无行为；仅供本地构建画面取证。</summary>
    [DisallowMultipleComponent]
    public sealed class AtelierBuildEvidence : MonoBehaviour
    {
        /// <summary>只在明确传入输出目录时启动，普通预览不显示或创建调试内容。</summary>
        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--atelier-evidence");
            if (index < 0 || index + 1 >= args.Length) return;
            string folder = Path.GetFullPath(args[index + 1]);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("构建取证目录不存在：" + folder);
            UnityEngine.Application.runInBackground = true;
            StartCoroutine(CapturePair(folder));
        }

        /// <summary>以真实帧更新等待并取两帧；不手动推进天空，完成后退出本次取证进程。</summary>
        private IEnumerator CapturePair(string folder)
        {
            yield return new WaitForSecondsRealtime(4);
            var state = FindAnyObjectByType<AtelierArtState>();
            Capture(Path.Combine(folder, "70_独立构建_开局黑夜HDRI.png"));
            float initialCoverage = state.LivingSky.Coverage;
            state.SetLateSky(true);
            yield return new WaitForSecondsRealtime(4);
            float start = Time.realtimeSinceStartup;
            Capture(Path.Combine(folder, "71_独立构建_后期程序化星夜.png"));
            var sky = FindAnyObjectByType<LivingSkyController>();
            float firstVisual = sky.Pigment.VisualTime;
            yield return new WaitForSecondsRealtime(8);
            Capture(Path.Combine(folder, "72_独立构建_后期流动八秒对照.png"));
            string report = FormattableString.Invariant($"开局覆盖={initialCoverage}\n开局天空={state.NormalHdri.GetTexture("_MainTex").name}\n真实间隔={Time.realtimeSinceStartup - start:F3}s\n颜料时间={firstVisual:F3} → {sky.Pigment.VisualTime:F3}\n覆盖={sky.Coverage}\n流动={sky.Flow}\n颜料尺寸={sky.Pigment.Pigment.width}x{sky.Pigment.Pigment.height}\n");
            File.WriteAllText(Path.Combine(folder, "最终返工独立构建运行记录.txt"), report);
            Debug.Log("独立画室：构建取证完成。\n" + report);
            // 局部镜头只用于检查接触、材质与窗外纵深，不保存为新的正式主镜头。
            var camera = Camera.main;
            camera.transform.position = new Vector3(-.2f, 1.7f, -2.35f);
            camera.transform.LookAt(new Vector3(1, 1.12f, -.7f));
            camera.fieldOfView = 52;
            yield return new WaitForSecondsRealtime(1);
            Capture(Path.Combine(folder, "73_独立构建_厚木桌面与干颜料.png"));
            state.SetLateSky(false);
            camera.transform.position = new Vector3(-.75f, 1.79f, -2.8f);
            camera.transform.LookAt(new Vector3(1.35f, 2.15f, 2.55f));
            camera.fieldOfView = 54;
            yield return new WaitForSecondsRealtime(1);
            Capture(Path.Combine(folder, "74_独立构建_窗边蓝光与村落纵深.png"));
            UnityEngine.Application.Quit();
        }

        /// <summary>从本程序摄像机离屏渲染，屏幕锁定不影响证据；恢复并释放临时资源。</summary>
        private static void Capture(string path)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("独立画室缺少主镜头。");
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                if (texture != null) Destroy(texture);
            }
        }
    }
}
