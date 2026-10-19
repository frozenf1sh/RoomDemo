// 文件：AtelierTAProbeReview.cs
// 职责：自动启动独立TA副本，截取各状态并收集编译、Shader和运行错误。
// 边界：只操作视觉验证，不模拟真人操作；截图只含世界，中文面板另需构建查看。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Unity.TechnicalArt.RealtimePainterly.Experiments;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>在明确的演示场景内驱动状态，不调用任何玩法会话或调试授予。</summary>
public static class AtelierTAProbeReview
{
    private static readonly string[] Names = { "TA01_局部草稿遮挡", "TA02_洗笔反馈", "TA03_手镜比较", "TA04_刮刀干湿表面", "TA05_屋面试刮" };

    /// <summary>逐个检查测试副本，任何异常仍恢复Play设置和日志订阅。</summary>
    public static async Task<string> Observe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景。");
        Directory.CreateDirectory("Logs/TAProbes20261019/Captures");
        bool oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        var oldOptions = EditorSettings.enterPlayModeOptions;
        var errors = new List<string>();
        var states = new List<object>();
        Application.LogCallback collect = (message, stack, kind) =>
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + stack);
        };
        Application.logMessageReceived += collect;
        try
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            for (int index = 0; index < Names.Length; index++)
            {
                EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Experiments/TechnicalArt/AtelierProbes/" + Names[index] + ".unity");
                EditorApplication.isPlaying = true;
                await Until(() => EditorApplication.isPlaying);
                await Task.Delay(2300);
                var probe = Object.FindAnyObjectByType<AtelierTAProbe>();
                if (probe == null) throw new InvalidOperationException("副本缺少验证控制器。");
                probe.ShowHud = false;
                Camera camera = probe.GetComponent<Camera>();
                if (index == 0)
                {
                    foreach (int stage in new[] { 0, 1, 2, 3 })
                    {
                        probe.SetStage(stage, true);
                        await Task.Delay(150);
                        Capture(camera, "草稿_阶段" + stage);
                    }
                }
                else if (index == 1)
                {
                    Capture(camera, "洗笔_接触前");
                    probe.BeginWash();
                    await Task.Delay(3100);
                    Capture(camera, "洗笔_散色近景");
                    probe.SetClose(false);
                    await Task.Delay(150);
                    Capture(camera, "洗笔_散色主镜头");
                }
                else if (index == 2)
                {
                    Capture(camera, "手镜_真实反射");
                    probe.SetMirrorAngle(35);
                    if (probe.MirrorAngle != AtelierTAProbe.MirrorAngleLimit) throw new InvalidOperationException("手镜转角限制未生效。");
                    await Task.Delay(300);
                    Capture(camera, "手镜_限制15度");
                }
                else if (index == 3)
                {
                    Capture(camera, "刀具_接触前");
                    probe.AddContact(new Vector2(.13f, .25f), new Vector2(.40f, .75f), false);
                    probe.AddContact(new Vector2(.77f, .45f), new Vector2(.65f, .82f), true);
                    await Task.Delay(150);
                    Capture(camera, "刀具_刮开与推色");
                }
                else
                {
                    Capture(camera, "屋面_主镜头细痕");
                    probe.RoofStep();
                    await Task.Delay(200);
                    Capture(camera, "屋面_第一次小孔");
                    probe.RoofStep();
                    probe.SetClose(true);
                    await Task.Delay(300);
                    Capture(camera, "屋面_扩大近景");
                }
                probe.ResetProbe();
                states.Add(new { scene = Names[index], controller = true, reset = true });
                EditorApplication.isPlaying = false;
                await Until(() => !EditorApplication.isPlayingOrWillChangePlaymode);
                await Task.Delay(250);
            }
            var shaderErrors = new List<string>();
            foreach (var shader in new[] { "Hidden/RoomDemo/TAProbe/LocalDraft", "RoomDemo/TAProbe/WashContact", "RoomDemo/TAProbe/ToolSurface", "RoomDemo/TAProbe/RealMirror" })
                foreach (var issue in ShaderUtil.GetShaderMessages(Shader.Find(shader)))
                    if (issue.severity.ToString() == "Error") shaderErrors.Add(shader + ":" + issue.message);
            string json = JsonConvert.SerializeObject(new { success = errors.Count == 0 && shaderErrors.Count == 0,
                states, errors, shaderErrors, boundary = "自动表现检查与离屏截图；不是真人鼠标或真实构建UI验收。" }, Formatting.Indented);
            File.WriteAllText("Logs/TAProbes20261019/运行检查.json", json);
            EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Experiments/TechnicalArt/AtelierProbes/" + Names[0] + ".unity");
            return json;
        }
        finally
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPlaying = false;
                    await Until(() => !EditorApplication.isPlayingOrWillChangePlaymode);
                }
            }
            finally
            {
                Application.logMessageReceived -= collect;
                EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
                EditorSettings.enterPlayModeOptions = oldOptions;
            }
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(40);
        while (!condition())
        {
            if (DateTime.UtcNow > limit) throw new TimeoutException("TA验证等待编辑器状态超时。");
            await Task.Delay(80);
        }
    }

    private static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D texture = null;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Logs/TAProbes20261019/Captures/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (texture != null) Object.DestroyImmediate(texture);
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
