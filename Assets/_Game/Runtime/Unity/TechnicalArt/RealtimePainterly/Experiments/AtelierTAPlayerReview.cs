// 文件：AtelierTAPlayerReview.cs
// 职责：只在显式命令行参数存在时截取独立演示Player的真实画面与中文面板。
// 边界：正常打开演示不会自动运行；自动取证不等于真人鼠标或美术验收。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Experiments
{
    /// <summary>有限构建取证，结束后退出自建Player，结果保存在调用方指定本机目录。</summary>
    public sealed class AtelierTAPlayerReview : MonoBehaviour
    {
        private string output;
        private readonly List<string> errors = new List<string>();
        private static readonly string[] Names = { "TA01_局部草稿遮挡", "TA02_洗笔反馈", "TA03_手镜比较", "TA04_刮刀干湿表面", "TA05_屋面试刮" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--ta-review-output");
            if (index < 0 || index + 1 >= args.Length) return;
            var root = new GameObject("TA构建取证_仅显式参数启用");
            DontDestroyOnLoad(root);
            root.AddComponent<AtelierTAPlayerReview>().output = Path.GetFullPath(args[index + 1]);
        }

        private IEnumerator Start()
        {
            // 自动取证必须在失去焦点后继续，不能等用户把演示窗口置前。
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            Application.logMessageReceivedThreaded += Collect;
            for (int index = 0; index < Names.Length; index++)
            {
                SceneManager.LoadScene(Names[index]);
                yield return new WaitForSecondsRealtime(2.5f);
                var probe = FindAnyObjectByType<AtelierTAProbe>();
                if (probe == null) { errors.Add("未找到验证控制器：" + Names[index]); continue; }
                if (index == 1) { probe.BeginWash(); yield return new WaitForSecondsRealtime(3); }
                if (index == 2)
                {
                    probe.SetMirrorAngle(35);
                    if (probe.MirrorAngle != AtelierTAProbe.MirrorAngleLimit) errors.Add("手镜正向转角没有夹紧。");
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return new WaitForEndOfFrame();
                    var endpoint = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(output,"手镜_正向15度.png"),endpoint.EncodeToPNG());
                    Destroy(endpoint);
                    probe.SetMirrorAngle(-35);
                    if (probe.MirrorAngle != -AtelierTAProbe.MirrorAngleLimit) errors.Add("手镜反向转角没有夹紧。");
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return new WaitForEndOfFrame();
                    endpoint = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(output,"手镜_反向15度.png"),endpoint.EncodeToPNG());
                    Destroy(endpoint);
                    probe.SetMirrorAngle(0);
                    yield return new WaitForSecondsRealtime(.2f);
                    var mirror = FindAnyObjectByType<AtelierMirrorProbe>();
                    if (mirror != null && mirror.ReviewTexture != null)
                    {
                        var previous = RenderTexture.active;
                        RenderTexture.active = mirror.ReviewTexture;
                        var diagnostic = new Texture2D(mirror.ReviewTexture.width,mirror.ReviewTexture.height,TextureFormat.RGB24,false);
                        diagnostic.ReadPixels(new Rect(0,0,diagnostic.width,diagnostic.height),0,0);
                        diagnostic.Apply();
                        File.WriteAllBytes(Path.Combine(output,"手镜原始RT.png"),diagnostic.EncodeToPNG());
                        Destroy(diagnostic);
                        RenderTexture.active = previous;
                    }
                }
                if (index == 3)
                {
                    probe.AddContact(new Vector2(.13f,.25f),new Vector2(.40f,.75f),false);
                    probe.AddContact(new Vector2(.77f,.45f),new Vector2(.65f,.82f),true);
                }
                if (index == 4) { probe.RoofStep(); probe.RoofStep(); probe.SetClose(true); }
                yield return new WaitForEndOfFrame();
                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output, Names[index] + ".png"),frame.EncodeToPNG());
                Destroy(frame);
            }
            // 只输出必要的错误；不收集命令行、账号或一般运行日志。
            string[] captured;
            lock (errors) captured = errors.ToArray();
            File.WriteAllText(Path.Combine(output,"构建运行检查.txt"),
                "五份场景实际Player自动取证完成。\n运行错误数：" + captured.Length + "\n" + string.Join("\n",captured)
                + "\n不代表真人操作、性能达标或美术验收通过。");
            Application.logMessageReceivedThreaded -= Collect;
            Application.Quit(captured.Length == 0 ? 0 : 1);
        }

        private void Collect(string message,string stack,LogType kind)
        {
            if (kind != LogType.Error && kind != LogType.Exception && kind != LogType.Assert) return;
            // 渲染线程也可能报告批次失效，不能只监听主线程而漏报。
            lock (errors) if (errors.Count < 30) errors.Add(message);
        }
    }
}
