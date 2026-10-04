// =============================================================================
// 文件：StarryWorldSequence.cs
// 职责：把既有房间导演成被星光侵蚀、边界失效、树林与篝火进入的连续视觉片段。
// 边界：只消费 world.starry，时间只决定视觉顺序；不添加或延迟领域事实。
// 恢复：取消归还属性块、建筑可见性、窗边界、相机、灯光、声音和按需生成物。
// =============================================================================
using System.Collections;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>具体十八秒样板，不引入通用电影框架或新的世界变换规则。</summary>
    public sealed class StarryWorldSequence : MonoBehaviour
    {
        [SerializeField] private RoomModules room;
        [SerializeField] private WorldExterior exterior;
        [SerializeField] private WorldMutation mutation;
        [SerializeField] private VisualThemeController theme;
        [SerializeField] private VisualThemeProfile starryTheme;
        [SerializeField] private EndingPresenter lighting;
        [SerializeField] private WindPresenter wind;
        [SerializeField] private Camera roomCamera;
        [SerializeField] private Collider windowCollider;
        [SerializeField] private AudioSource openingAudio, exteriorAudio;
        [SerializeField] private Text caption;
        [SerializeField, Min(10)] private float duration = 18;
        [SerializeField] private WorldBoundaryDissolve ceilingDissolve;
        [SerializeField] private WorldBoundaryDissolve backDissolve;
        [SerializeField] private WorldBoundaryDissolve leftDissolve;
        [SerializeField] private WorldBoundaryDissolve rightDissolve;
        [SerializeField] private WorldBoundaryDissolve windowDissolve;
        [SerializeField] private WorldBoundaryDissolve natureReveal;
        private GameSession session;
        private Coroutine playing;
        private float startedAt, elapsed, cameraFar;
        private bool ownsPresentation, windowWasEnabled, preview;
        private Vector3 windowScale;
        private CampfireVisual spawnedFire;

        /// <summary>演出正在进行；领域事实早已成立。</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>只表示最终视觉已恢复，不参与任何配方判定。</summary>
        public bool IsFinalPresenting { get; private set; }
        /// <summary>开发面板使用的短节拍名，正式屏幕不常驻说明文字。</summary>
        public string CurrentBeat { get; private set; } = "房间仍有边界";
        /// <summary>具体样板时长，供开发者录像与调试读取。</summary>
        public float Duration => duration;
        /// <summary>从开始到当前的实际演出时间，使用非缩放时间保证暂停恢复一致。</summary>
        public float Elapsed => IsPlaying ? Time.unscaledTime - startedAt : elapsed;

        /// <summary>保留原技术绑定入口；此方法不修改场景摆放或玩法数据。</summary>
        public void Configure(RoomModules modules, WorldExterior field, WorldMutation operations,
            VisualThemeController ambience, VisualThemeProfile profile, EndingPresenter existingLighting,
            WindPresenter existingWind, Camera camera, Collider boundary, AudioSource opening,
            AudioSource outside, Text endingCaption)
        {
            room = modules;
            exterior = field;
            mutation = operations;
            theme = ambience;
            starryTheme = profile;
            lighting = existingLighting;
            wind = existingWind;
            roomCamera = camera;
            windowCollider = boundary;
            openingAudio = opening;
            exteriorAudio = outside;
            caption = endingCaption;
        }

        /// <summary>美术安装器绑定实体表面与外景显影；不替换任何世界事实或物体 ID。</summary>
        public void ConfigureArt(WorldBoundaryDissolve roof, WorldBoundaryDissolve back,
            WorldBoundaryDissolve left, WorldBoundaryDissolve right, WorldBoundaryDissolve window,
            WorldBoundaryDissolve nature)
        {
            ceilingDissolve = roof;
            backDissolve = back;
            leftDissolve = left;
            rightDissolve = right;
            windowDissolve = window;
            natureReveal = nature;
            duration = 18;
        }

        /// <summary>只读绑定会话；已有最终事实直接恢复，不重复播放动画。</summary>
        public void Bind(GameSession game)
        {
            Cancel();
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            session.EventRaised += OnEvent;
            if (session.State.HasFact(ExplorationRules.WorldStarry)) ApplyFinalStateImmediately();
        }

        private void OnEvent(DomainEvent value)
        {
            // 领域负责去重与闭包；导演只接收已经发生的事实。
            if (value is FactAdded added && added.Fact == ExplorationRules.WorldStarry) PlayAnimated();
        }

        /// <summary>播放已成立事实的表现，重复事件不重新借用房间。</summary>
        public void PlayAnimated()
        {
            if (session == null || !session.State.HasFact(ExplorationRules.WorldStarry)
                || IsPlaying || IsFinalPresenting) return;
            Prepare(false);
            playing = StartCoroutine(Run());
        }

        /// <summary>明确的只读美术预览，不提交领域命令。</summary>
        public void PlayBenchmarkPreview()
        {
            Prepare(true);
            playing = StartCoroutine(Run());
        }

        private void Prepare(bool visualPreview)
        {
            Cancel();
            preview = visualPreview;
            windowScale = room.WindowBoundary.localScale;
            cameraFar = roomCamera.farClipPlane;
            windowWasEnabled = windowCollider.enabled;
            theme.Capture();
            lighting.SetLightingOverride(true);
            ownsPresentation = true;
            IsPlaying = true;
            IsFinalPresenting = false;
            startedAt = Time.unscaledTime;
            elapsed = 0;
            caption.text = "";
            CurrentBeat = "声音退去，星光第一次穿过天花板";
            wind.SetAudioDuck(.12f);
            ResetArt();
        }

        private IEnumerator Run()
        {
            bool roofSound = false;
            bool outsideSound = false;
            while (Time.unscaledTime - startedAt < duration)
            {
                float time = Time.unscaledTime - startedAt;
                if (!roofSound && time >= .65f)
                {
                    openingAudio.Play();
                    roofSound = true;
                }
                if (!outsideSound && time >= 6)
                {
                    exteriorAudio.Play();
                    outsideSound = true;
                }
                ApplyFrame(time, true);
                yield return null;
            }
            ApplyFrame(duration, true);
            elapsed = duration;
            IsPlaying = false;
            IsFinalPresenting = true;
            playing = null;
            CurrentBeat = preview ? "星空预览完成（领域未改变）" : "房间已经失去边界";
        }

        /// <summary>连续进度驱动表面、微粒与光源；不让建筑整体移动成为变化主体。</summary>
        private void ApplyFrame(float time, bool particles)
        {
            if (time >= .55f)
            {
                mutation.Show(exterior.SkyDome);
                roomCamera.farClipPlane = Mathf.Max(cameraFar, 180);
                float stars = Progress(time, .55f, 1.8f);
                float roof = Progress(time, 1.45f, 3.8f);
                ApplyDissolve(ceilingDissolve, roof, stars, particles);
                if (roof >= 1) mutation.Hide(room.Ceiling.gameObject);
                CurrentBeat = "星纹扩散，天花板成为天空";
            }
            if (time >= 3.8f)
            {
                float back = Progress(time, 3.8f, 6.8f);
                ApplyDissolve(backDissolve, back, Progress(time, 3.8f, 5), particles);
                if (back >= 1) mutation.Hide(room.WallBack.gameObject);
                // 左右两侧略有错开，避免墙同时像幕布一样退场。
                float left = Progress(time, 4.45f, 7.65f);
                float right = Progress(time, 5.0f, 8.3f);
                ApplyDissolve(leftDissolve, left, left, particles);
                ApplyDissolve(rightDissolve, right, right, particles);
                if (left >= 1) mutation.Hide(room.WallLeft.gameObject);
                if (right >= 1) mutation.Hide(room.WallRight.gameObject);
                CurrentBeat = "星光沿墙面侵入，家具留在原处";
            }
            if (time >= 5.3f)
            {
                float stretch = Progress(time, 5.3f, 6.5f);
                float boundary = Progress(time, 6.4f, 9.0f);
                // 仅窗框的轻微拉伸作为辅助：最终消失由表面溶解完成。
                mutation.Scale(room.WindowBoundary, windowScale,
                    Vector3.Scale(windowScale, new Vector3(1.19f, 1.12f, 1)), stretch);
                ApplyDissolve(windowDissolve, boundary, stretch, particles);
                windowCollider.enabled = false;
                if (boundary >= 1) mutation.Hide(room.WindowBoundary.gameObject);
                wind.SetAudioDuck(Mathf.Lerp(.12f, 1, stretch));
                CurrentBeat = "窗的边界失效，外面已是世界";
            }
            if (time >= 8)
            {
                mutation.ReplaceVisual(room.Floor.gameObject, exterior.FloorPatch);
                mutation.Hide(room.FloorDetails.gameObject);
                mutation.Show(exterior.Environment);
                float nature = Progress(time, 8, 11.2f);
                // 原地显影保留真实尺度；草地、石头和树不从一个极小世界放大。
                ApplyDissolve(natureReveal, 1 - nature, 0, false);
                CurrentBeat = "木台之外，树林从夜色中显现";
            }
            if (time >= 10.7f)
            {
                var fire = mutation.Spawn("StarryCampfire", exterior.CampfirePrefab, exterior.CampfireAnchor);
                if (spawnedFire == null) spawnedFire = fire.GetComponent<CampfireVisual>();
                if (spawnedFire != null) spawnedFire.SetIgnition(Progress(time, 10.7f, 13.4f));
                CurrentBeat = "木柴点燃，暖火照亮留下的房间";
            }
            if (time >= 6.8f)
            {
                // 月光先建立外部空间；暖火随后成为人仍在这里的中心。
                mutation.TransitionTheme(starryTheme, Progress(time, 6.8f, duration));
                if (time >= 13.4f) CurrentBeat = "冷月光、暖火与笔触夜色重新融合";
            }
            caption.text = "";
        }

        private static void ApplyDissolve(WorldBoundaryDissolve surface, float progress, float stars, bool particles)
        {
            // 独立灰盒可缺少专用表面；正式美术场景由安装器与场景验证保证绑定。
            if (surface != null) surface.Apply(progress, stars, particles);
        }

        private static float Progress(float time, float start, float end)
        {
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - start) / (end - start)));
        }

        private void ResetArt()
        {
            if (ceilingDissolve != null) ceilingDissolve.ResetPresentation();
            if (backDissolve != null) backDissolve.ResetPresentation();
            if (leftDissolve != null) leftDissolve.ResetPresentation();
            if (rightDissolve != null) rightDissolve.ResetPresentation();
            if (windowDissolve != null) windowDissolve.ResetPresentation();
            if (natureReveal != null) natureReveal.ResetPresentation();
        }

        /// <summary>归还演出拥有的所有借用资源；调用方随后恢复新事实对应的视觉。</summary>
        /// <param name="sceneUnloading">场景卸载时仅归还全局资源，跳过节点重新激活。</param>
        public void Cancel(bool sceneUnloading = false)
        {
            if (playing != null) StopCoroutine(playing);
            playing = null;
            IsPlaying = false;
            IsFinalPresenting = false;
            elapsed = 0;
            if (!sceneUnloading) ResetArt();
            if (ownsPresentation)
            {
                if (mutation != null) mutation.ResetCapturedState(!sceneUnloading);
                if (roomCamera != null) roomCamera.farClipPlane = cameraFar;
                if (windowCollider != null) windowCollider.enabled = windowWasEnabled;
                if (lighting != null) lighting.SetLightingOverride(false);
                ownsPresentation = false;
            }
            spawnedFire = null;
            // 卸载顺序不固定，资源归还必须容忍子组件已经销毁。
            if (wind != null) wind.SetAudioDuck(1, true);
            if (openingAudio != null) openingAudio.Stop();
            if (exteriorAudio != null) exteriorAudio.Stop();
            if (caption != null) caption.text = "";
            CurrentBeat = "房间仍有边界";
        }

        /// <summary>最终预设直接恢复完整外景与已点燃篝火，不产生过程碎屑。</summary>
        public void ApplyFinalStateImmediately()
        {
            Cancel();
            if (session == null || !session.State.HasFact(ExplorationRules.WorldStarry)) return;
            Prepare(false);
            ApplyFrame(duration, false);
            IsPlaying = false;
            IsFinalPresenting = true;
            elapsed = duration;
            CurrentBeat = "星空最终状态（即时恢复）";
        }

        /// <summary>直接展示最终美术而不更改世界，适合灯光调试。</summary>
        public void ApplyFinalPreview()
        {
            Prepare(true);
            ApplyFrame(duration, false);
            IsPlaying = false;
            IsFinalPresenting = true;
            elapsed = duration;
            CurrentBeat = "最终效果预览（领域未改变）";
        }

        /// <summary>表现重置不等于游戏撤销，领域事实原样保留。</summary>
        public void ResetPresentation() { Cancel(); }

        private void OnDestroy()
        {
            Cancel(true);
            if (session != null) session.EventRaised -= OnEvent;
        }
    }
}
