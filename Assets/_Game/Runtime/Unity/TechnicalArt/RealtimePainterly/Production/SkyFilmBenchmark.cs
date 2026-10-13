// 文件：SkyFilmBenchmark.cs
// 职责：独立复刻场景的管线隔离、暂停与重置预览。
// 边界：只处理视觉预览，不安装主场景录像入口，不读写游戏事实。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>使用原 Hero 管线的隔离预览；进入和退出时恢复临时全局设置。</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class SkyFilmBenchmark : MonoBehaviour
    {
        public RenderPipelineAsset Pipeline;
        public LivingSkyController Sky;
        private RenderPipelineAsset previousDefault, previousQuality;
        private int previousVsync, previousRate;
        private bool previousBackground;
        private bool ownsSettings;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            previousVsync = QualitySettings.vSyncCount;
            previousRate = UnityEngine.Application.targetFrameRate;
            previousBackground = UnityEngine.Application.runInBackground;
            if (Pipeline != null)
            {
                GraphicsSettings.defaultRenderPipeline = Pipeline;
                QualitySettings.renderPipeline = Pipeline;
            }
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 60;
            UnityEngine.Application.runInBackground = true;
            ownsSettings = true;
        }
        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) Sky.SetPaused(!Sky.Paused);
            if (Keyboard.current.rKey.wasPressedThisFrame) Sky.ResetEffect();
            if (Keyboard.current.escapeKey.wasPressedThisFrame) UnityEngine.Application.Quit();
        }

        private void OnDisable()
        {
            if (!ownsSettings) return;
            if (Pipeline != null && GraphicsSettings.defaultRenderPipeline == Pipeline) GraphicsSettings.defaultRenderPipeline = previousDefault;
            if (Pipeline != null && QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previousQuality;
            QualitySettings.vSyncCount = previousVsync;
            UnityEngine.Application.targetFrameRate = previousRate;
            UnityEngine.Application.runInBackground = previousBackground;
            ownsSettings = false;
        }
    }
}
