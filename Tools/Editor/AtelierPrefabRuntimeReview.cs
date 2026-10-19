// =============================================================================
// 文件：AtelierPrefabRuntimeReview.cs
// 职责：对模块化后的已保存画室做启动、笔记绑定、离屏画面与Console运行检查。
// 边界：只验证现有玩法，不运行旧主线套件，不声称真人鼠标或新流程验收。
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Unity.ArtRebuild;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>临时使用不重载域的Play模式，以便异步审查持续到退出；结束时恢复编辑设置。</summary>
public static class AtelierPrefabRuntimeReview
{
    /// <summary>启动并观察当前可玩场景，不自动执行剧情路线。</summary>
    public static async Task<string> Observe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("运行检查要求先退出Play。");
        if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("存在未保存场景，已拒绝运行检查。");
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/AtelierPlayable.unity");
        bool oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        var oldOptions = EditorSettings.enterPlayModeOptions;
        var errors = new List<string>();
        Application.LogCallback collect = (message, stack, kind) =>
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                errors.Add(message + "\n" + stack);
        };
        Directory.CreateDirectory("Logs/FlowRedesign/Runtime");
        try
        {
            // 不重载域只用于此独立审查进程，原编辑器设置会在finally恢复。
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            Application.logMessageReceived += collect;
            EditorApplication.isPlaying = true;
            await Until(() => EditorApplication.isPlaying, 30000);
            await Task.Delay(6000);
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            if (game == null || game.Session == null) throw new InvalidOperationException("画室会话没有正常启动。");
            bool bound = game.ViewCamera != null && game.Notebook != null && game.Narrator != null &&
                game.Captions != null && game.Wash != null && game.Painting != null && game.Lamp != null &&
                game.Curtain != null && game.Finale != null && game.Trial != null && game.Reminder != null;
            if (!bound) throw new InvalidOperationException("运行时关键模块存在空引用。");
            game.Notebook.SetOpen(true);
            await Task.Delay(500);
            bool notebookOpened = game.Notebook.IsOpen;
            game.Notebook.SetOpen(false);
            await Task.Delay(500);
            Capture(game.ViewCamera, "Logs/FlowRedesign/Runtime/模块化后主镜头.png");
            // 近景参考来自现有输入入口的自动调用，不冒充真人鼠标检视。
            Vector3 roomPosition = game.ViewCamera.transform.position;
            foreach (var kind in new[] { AtelierTargetKind.WashWater, AtelierTargetKind.Painting })
            {
                var target = Object.FindObjectsByType<AtelierInteractionTarget>().First(x => x.Kind == kind);
                if (kind == AtelierTargetKind.Painting) game.FocusPainting();
                else game.Observe(target, new Vector2(.5f, .5f));
                Vector3 destination = kind == AtelierTargetKind.Painting ? game.FocusPose.position : game.DeskFocusPose.position;
                await Until(() => Vector3.Distance(game.ViewCamera.transform.position, destination) < .005f, 30000);
                await Task.Delay(250);
                Capture(game.ViewCamera, "Logs/FlowRedesign/Runtime/现有近景_" + kind + ".png");
                game.ReturnToRoom();
                await Until(() => Vector3.Distance(game.ViewCamera.transform.position, roomPosition) < .005f, 30000);
                await Task.Delay(250);
            }
            var report = new { success = errors.Count == 0 && notebookOpened, sessionStarted = true,
                keyBindingsValid = bound, notebookOpened, notebookClosed = !game.Notebook.IsOpen,
                targets = Object.FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include).Length,
                graphics = SystemInfo.graphicsDeviceType.ToString(), errors,
                capture = "离屏主镜头，只含世界，不包含Screen Space Overlay UI；不构成真实构建UI验收。",
                boundary = "自动启动与组件操作检查，没有真人鼠标试玩，也没有实现或验证新四阶段流程。" };
            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText("Logs/FlowRedesign/Runtime/result.json", json);
            if (!report.success) throw new InvalidOperationException("运行检查发现错误，请看Runtime/result.json。");
            return json;
        }
        finally
        {
            EditorApplication.isPlaying = false;
            await Until(() => !EditorApplication.isPlayingOrWillChangePlaymode, 30000);
            Application.logMessageReceived -= collect;
            EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }
    }

    /// <summary>等待编辑器状态改变，设明确终止时间，避免永远挂起。</summary>
    private static async Task Until(Func<bool> ready, int timeout)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeout);
        while (!ready())
        {
            if (DateTime.UtcNow > end) throw new TimeoutException("编辑器状态切换超时。");
            await Task.Delay(100);
        }
    }

    /// <summary>用项目URP支持的单镜头请求取证，GPU和CPU临时资源均由本方法释放。</summary>
    private static void Capture(Camera camera, string path)
    {
        var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
