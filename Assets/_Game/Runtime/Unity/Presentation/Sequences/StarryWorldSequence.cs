// =============================================================================
// 文件：StarryWorldSequence.cs
// 职责：导演这一个“房间打开成为星空世界”的建筑、外景、生成物和主题演出。
// 边界：只消费 world.starry，所有延迟都属于视觉；不授予线索、不添加领域事实。
// 恢复：重开取消旧协程并归还建筑、相机、光照与声音，最终预设不重播演出。
// =============================================================================
using System.Collections;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>具体星空样板的协程导演；不引入通用场景图、电影或建筑规则框架。</summary>
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
        [SerializeField, Min(10)] private float duration = 15;
        private GameSession session;
        private Coroutine playing;
        private float startedAt, elapsed, cameraFar;
        private bool ownsPresentation, windowWasEnabled, preview;
        private Vector3 roofPosition, roofScale, backScale, leftPosition, rightPosition, leftScale, rightScale, windowPosition, windowScale;

        /// <summary>正在播放表现；即使为 true，领域事实也已经成立。</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>是否已显示最终外景；只是表现进度，不用于任何配方判断。</summary>
        public bool IsFinalPresenting { get; private set; }
        /// <summary>当前节拍的中文名称，供开发面板显示。</summary>
        public string CurrentBeat { get; private set; } = "房间仍有边界";
        /// <summary>设计时长与当前进度，用于复测超时和技术录像。</summary>
        public float Duration => duration;
        public float Elapsed => IsPlaying ? Time.unscaledTime - startedAt : elapsed;

        /// <summary>由编辑器显式绑定建筑、外景、主题和提示资源，不在每帧寻找组件。</summary>
        public void Configure(RoomModules modules, WorldExterior field, WorldMutation operations,
            VisualThemeController ambience, VisualThemeProfile profile, EndingPresenter existingLighting,
            WindPresenter existingWind, Camera camera, Collider boundary, AudioSource opening, AudioSource outside, Text endingCaption)
        {
            room = modules; exterior = field; mutation = operations; theme = ambience; starryTheme = profile;
            lighting = existingLighting; wind = existingWind; roomCamera = camera; windowCollider = boundary;
            openingAudio = opening; exteriorAudio = outside; caption = endingCaption;
        }

        /// <summary>重新绑定只读会话；预设中的世界事实直接恢复，不从头播放。</summary>
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
            // 同一个事实只产生一次事件，导演不反向决定世界是否接受了这次解释。
            if (value is FactAdded added && added.Fact == ExplorationRules.WorldStarry) PlayAnimated();
        }

        /// <summary>播放已经成立的事实；没有该事实时只能走明确的表现预览入口。</summary>
        public void PlayAnimated()
        {
            if (session == null || !session.State.HasFact(ExplorationRules.WorldStarry) || IsPlaying || IsFinalPresenting) return;
            Prepare(false);
            playing = StartCoroutine(Run());
        }

        /// <summary>只播放视觉样板，不执行配方、不提交事实。</summary>
        public void PlayBenchmarkPreview() { Prepare(true); playing = StartCoroutine(Run()); }

        private void Prepare(bool visualPreview)
        {
            Cancel();
            preview = visualPreview;
            // 在当前事实对应的房间上借用表现，远方的亮度可以成为正确的起点。
            roofPosition = room.Ceiling.localPosition; roofScale = room.Ceiling.localScale; backScale = room.WallBack.localScale;
            leftPosition = room.WallLeft.localPosition; rightPosition = room.WallRight.localPosition;
            leftScale = room.WallLeft.localScale; rightScale = room.WallRight.localScale;
            windowPosition = room.WindowBoundary.localPosition; windowScale = room.WindowBoundary.localScale;
            cameraFar = roomCamera.farClipPlane; windowWasEnabled = windowCollider.enabled;
            theme.Capture(); lighting.SetLightingOverride(true); ownsPresentation = true;
            IsPlaying = true; IsFinalPresenting = false; startedAt = Time.unscaledTime; elapsed = 0;
            caption.text = ""; CurrentBeat = "声音退去，房间安静下来";
            wind.SetAudioDuck(.15f);
        }

        private IEnumerator Run()
        {
            bool roofSound = false, outsideSound = false;
            while (Time.unscaledTime - startedAt < duration)
            {
                float time = Time.unscaledTime - startedAt;
                if (!roofSound && time >= 1) { openingAudio.Play(); roofSound = true; }
                if (!outsideSound && time >= 6) { exteriorAudio.Play(); outsideSound = true; }
                ApplyFrame(time);
                yield return null;
            }
            ApplyFrame(duration); elapsed = duration; IsPlaying = false; IsFinalPresenting = true; playing = null;
            CurrentBeat = preview ? "星空预览完成（领域未改变）" : "熟悉的房间，已经向世界敞开";
        }

        /// <summary>所有相对时间只调视觉；建筑、地板、外景和篝火分别具有独立节拍。</summary>
        private void ApplyFrame(float time)
        {
            if (time >= 1)
            {
                mutation.Show(exterior.SkyDome); roomCamera.farClipPlane = Mathf.Max(cameraFar, 120);
                float roof = Progress(time, 1, 3.8f);
                mutation.Move(room.Ceiling, roofPosition, roofPosition + Vector3.up * 2.5f, roof);
                mutation.Scale(room.Ceiling, roofScale, Vector3.Scale(roofScale, new Vector3(1, .04f, 1)), roof);
                if (roof >= 1) mutation.Hide(room.Ceiling.gameObject);
                CurrentBeat = "天花板退去，星空显露";
            }
            if (time >= 4)
            {
                float walls = Progress(time, 4, 6.7f);
                mutation.Scale(room.WallBack, backScale, Vector3.Scale(backScale, new Vector3(1, .02f, 1)), walls);
                if (walls >= 1) mutation.Hide(room.WallBack.gameObject);
                mutation.Move(room.WallLeft, leftPosition, leftPosition + Vector3.left * 6, walls);
                mutation.Move(room.WallRight, rightPosition, rightPosition + Vector3.right * 6, walls);
                mutation.Scale(room.WallLeft, leftScale, Vector3.Scale(leftScale, new Vector3(1, .18f, 1)), walls);
                mutation.Scale(room.WallRight, rightScale, Vector3.Scale(rightScale, new Vector3(1, .18f, 1)), walls);
                CurrentBeat = "墙体退开，熟悉的家具留在原处";
            }
            if (time >= 6)
            {
                float boundary = Progress(time, 6, 8);
                mutation.Scale(room.WindowBoundary, windowScale, Vector3.Scale(windowScale, new Vector3(1.8f, 1.3f, 1)), boundary);
                mutation.Move(room.WindowBoundary, windowPosition, windowPosition + Vector3.forward * 2, boundary);
                windowCollider.enabled = false;
                if (boundary >= 1) mutation.Hide(room.WindowBoundary.gameObject);
                wind.SetAudioDuck(Mathf.Lerp(.15f, 1, boundary));
                CurrentBeat = "窗失去边界，外面的风靠近";
            }
            if (time >= 8)
            {
                mutation.ReplaceVisual(room.Floor.gameObject, exterior.FloorPatch);
                mutation.Hide(room.FloorDetails.gameObject); mutation.Show(exterior.Environment);
                float nature = Progress(time, 8, 10);
                mutation.Scale(exterior.Environment.transform, Vector3.one * .02f, Vector3.one, nature);
                CurrentBeat = "木地板之外，草地与远景显现";
            }
            if (time >= 10)
            {
                var fire = mutation.Spawn("StarryCampfire", exterior.CampfirePrefab, exterior.CampfireAnchor);
                mutation.Scale(fire.transform, Vector3.one * .02f, Vector3.one, Progress(time, 10, 11.5f));
                CurrentBeat = "新的篝火在房间之外燃起";
            }
            if (time >= 11.5f)
            {
                mutation.TransitionTheme(starryTheme, Progress(time, 11.5f, duration));
                CurrentBeat = "夜色、火光与房间重新融合";
                caption.text = "墙退去了。\n熟悉的东西，仍在星空下。";
                var color = caption.color; color.a = Progress(time, 12, 13.5f); caption.color = color;
            }
        }

        private static float Progress(float time, float start, float end) => Mathf.Clamp01((time - start) / (end - start));

        /// <summary>中断演出并清理自己拥有的资源，调用方随后可恢复新领域状态。</summary>
        public void Cancel()
        {
            if (playing != null) StopCoroutine(playing);
            playing = null; IsPlaying = false; IsFinalPresenting = false; elapsed = 0;
            if (ownsPresentation)
            {
                if (mutation != null) mutation.ResetCapturedState();
                if (roomCamera != null) roomCamera.farClipPlane = cameraFar;
                if (windowCollider != null) windowCollider.enabled = windowWasEnabled;
                if (lighting != null) lighting.SetLightingOverride(false);
                ownsPresentation = false;
            }
            // 场景卸载时子组件可能先销毁；避免销毁顺序制造假错误。
            if (wind != null) wind.SetAudioDuck(1, true);
            if (openingAudio != null) openingAudio.Stop();
            if (exteriorAudio != null) exteriorAudio.Stop();
            if (caption != null) caption.text = "";
            CurrentBeat = "房间仍有边界";
        }

        /// <summary>以实际事实恢复最终世界；没有该事实时归还原房间。</summary>
        public void ApplyFinalStateImmediately()
        {
            Cancel();
            if (session == null || !session.State.HasFact(ExplorationRules.WorldStarry)) return;
            Prepare(false); ApplyFrame(duration); IsPlaying = false; IsFinalPresenting = true; elapsed = duration;
            CurrentBeat = "星空最终状态（即时恢复）";
        }
        /// <summary>明确的最终视觉预览，不把预览结果记入世界。</summary>
        public void ApplyFinalPreview()
        {
            Prepare(true); ApplyFrame(duration); IsPlaying = false; IsFinalPresenting = true; elapsed = duration;
            CurrentBeat = "最终效果预览（领域未改变）";
        }
        /// <summary>归还演出借用的节点；世界事实原样保留。</summary>
        public void ResetPresentation() { Cancel(); }
        private void OnDestroy() { Cancel(); if (session != null) session.EventRaised -= OnEvent; }
    }
}
