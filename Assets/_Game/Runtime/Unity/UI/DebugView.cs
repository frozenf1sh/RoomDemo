// =============================================================================
// 文件：DebugView.cs
// 职责：显示中文规则状态与引导文字，提供调试操作按钮。
// 边界：通过显式回调与 Session 进入应用层，不直接修改场景或状态集合。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>显示中文规则状态与引导文字，提供调试操作按钮。</summary>
    public sealed class DebugView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text stateText;
        [SerializeField] private Text eventText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text observationText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button grantButton;
        [SerializeField] private Button addFactButton;
        [SerializeField] private InputField factInput;
        [SerializeField] private Button[] presetButtons;
        [SerializeField] private Text controlsText;
        [SerializeField] private OneLineSubtitle transientHint;
        /// <summary>提示与解释共用单行显示，不创建常驻任务区。</summary>
        public void ConfigureHint(OneLineSubtitle caption) { transientHint = caption; }
        private bool developmentMode;
        private GameSession session;
        // 保留少量事件便于排查；不以日志或界面文字作为世界真相。
        private readonly Queue<string> recent = new Queue<string>();
        private bool showHint, waitedForBenchmark;
        private PaintingSeaSequence presentation;
        private Action<DebugPreset> jumpTo;
        /// <summary>玩家当前看到的中文引导文案。</summary>
        public string Objective => objectiveText.text;
        /// <summary>注入状态区、事件区与玩家引导文本。</summary>
        public void Configure(GameObject body, Text state, Text events, Text objective) { panel = body; stateText = state; eventText = events; objectiveText = objective; }
        /// <summary>注入调试按钮、事实输入框和预设按钮。</summary>
        public void ConfigureTools(Text observations, Button restart, Button reset, Button grant, Button add, InputField input, Button[] presets)
        { observationText = observations; restartButton = restart; resetButton = reset; grantButton = grant; addFactButton = add; factInput = input; presetButtons = presets; }
        /// <summary>绑定玩家操作说明，正式模式不显示开发快捷键。</summary>
        public void ConfigureControls(Text controls) { controlsText = controls; }
        /// <summary>隐藏开发状态与 F 键；H 仍是玩家可选提示。</summary>
        public void SetDevelopmentMode(bool enabled)
        {
            developmentMode = enabled; if (!enabled) panel.SetActive(false);
            if (controlsText != null) controlsText.text = enabled
                ? "右键转头 · 左键观察 · N 笔记本 · F3 状态 · F4 海画 · F7 世界／检视 · F6 海之前 · F8 窗口 · F9 复测"
                : "右键拖动转头 · 左键拿近／观察 · N 笔记本 · H 轻提示";
        }
        /// <summary>重绑当前会话与显式操作回调，刷新中文状态显示。</summary>
        public void Bind(GameSession game, Action restart, Action<DebugPreset> jump, Action<FactId> addFact, PaintingSeaSequence director = null)
        {
            presentation = director; jumpTo = jump; waitedForBenchmark = false;
            if (session != null) session.EventRaised -= OnEvent;
            session = game; session.EventRaised += OnEvent; recent.Clear(); eventText.text = ""; showHint = false;
            observationText.text = "环顾房间，收集观察到的碎片，再将意义赋予这里。按 H 查看提示。";
            // 所有操作都走显式应用层回调或 Session，UI 不写 GameObject 状态。
            Wire(restartButton, () => restart()); Wire(resetButton, () => restart()); Wire(grantButton, () => session.DebugGrantAllClues());
            Wire(addFactButton, () => { try { addFact(new FactId(factInput.text.Trim())); } catch (ArgumentException e) { eventText.text = e.Message; } });
            var presets = session.Rules.Observations.Count > 0
                ? ExplorationPresets.All.Concat(new[] { DebugPresets.BeforeSea, DebugPresets.Emergence }).ToArray()
                : DebugPresets.All.ToArray();
            for (int i = 0; i < presetButtons.Length; i++)
            {
                // 同一场景也承载旧 MVP 回归，额外探索按钮在旧内容表下隐藏。
                presetButtons[i].gameObject.SetActive(i < presets.Length);
                if (i >= presets.Length) continue;
                var preset = presets[i]; presetButtons[i].GetComponentInChildren<Text>().text = preset.DisplayName;
                Wire(presetButtons[i], () => jump(preset));
            }
            Render();
        }
        /// <summary>替换按钮监听，避免 Restart 后重复执行。</summary>
        private static void Wire(Button button, UnityEngine.Events.UnityAction action) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        /// <summary>显示观察与组合反馈，维护最近事件队列。</summary>
        private void OnEvent(DomainEvent e)
        {
            recent.Enqueue(e.ToString()); while (recent.Count > 5) recent.Dequeue(); eventText.text = string.Join("\n", recent);
            if (e is ObjectInspected inspected) observationText.text = inspected.Description;
            if (e is InspectionFailed failed) observationText.text = failed.Reason;
            if (e is CombinationSucceeded) { waitedForBenchmark = presentation != null && presentation.IsBenchmarkPending; }
            if (e is CombinationSucceeded succeeded) observationText.text = succeeded.ProducedClues.Count > 0
                ? "两个具体的观察，联系成了一个新的想法。"
                : "你的解释已成为房间的一部分。找找发生了什么变化。";
            // 玩家引导只是对已发生事实的解释，不能触发下一步规则。
            if (e is FactAdded fact && fact.Fact == DemoRules.WindEntering) observationText.text = "你没有解释风。长大的盆栽与通往远方的窗户开始相互回应，一张纸滑了出来。";
            if (e is FactAdded end && end.Fact == DemoRules.Complete && (presentation == null || !presentation.IsBenchmarkPending)) observationText.text = "静止的房间，已经有了自己的呼吸。";
            // 详细日志按运行环境开关，Release 中不输出冗余事件。
            if (developmentMode && (UnityEngine.Application.isEditor || Debug.isDebugBuild) &&
                (e is ClueDiscovered || e is CombinationSucceeded || e is CombinationFailed || e is FactAdded || e is ReactionTriggered)) Debug.Log(e.ToString());
            Render();
        }
        /// <summary>把领域只读状态转换为中文界面，并保留排查所需 ID。</summary>
        private void Render()
        {
            // 每次从当前会话读取快照，名称中文化，括号内 ID 便于排查。
            var s = session.State;
            stateText.text = "已知碎片\n" + string.Join("  ", s.KnownClues.OrderBy(x => x.Value).Select(x => ClueName(x) + "（" + x.Value + "）")) +
                "\n\n世界事实\n" + string.Join("\n", s.Facts.OrderBy(x => x.Value).Select(x => DemoText.Fact(x) + "（" + x.Value + "）")) +
                "\n\n已完成的配方\n" + string.Join("\n", session.Rules.Recipes.Where(x => s.HasCompleted(x.Id)).Select(x => RecipeName(x) + "（" + x.Id + "）")) +
                "\n\n已触发的反应\n" + string.Join("\n", s.FiredReactions.OrderBy(x => x).Select(x => DemoText.Reaction(x) + "（" + x + "）"));
            stateText.text += "\n\n日序：第 " + s.DayIndex + " 天\n等待次日\n" + string.Join("\n", s.PendingDeferredEffects.Select(x => DemoText.Reaction(x.ReactionId) + " → 第 " + x.DueDay + " 天"));
            if (UnityEngine.Application.isEditor || Debug.isDebugBuild)
                stateText.text += "\n\n当前可用的配方\n" + string.Join("\n", session.Rules.AvailableRecipes(s).Select(RecipeName));
            // 主线后半段的临时帮助；只有玩家按 H 时才实际显示。
            bool narrative = session.Rules.Recipes.Any(x=>x.Disposition!=NarrativeDisposition.Neutral);
            if (narrative && !s.HasFact(MainlineRules.CeilingStarry) && !s.HasFact(MainlineRules.Complete))
            {
                objectiveText.text = s.HasFact(NarrativeRules.BanalComplete) ? "作品已经完成。"
                    : s.HasFact(MainlineRules.NightArrived) ? (s.Knows(MainlineRules.Starry) ? "天空可以留在画里。也可以再看看头顶。" : "抬头看看夜色，再去窗边看一眼尽头。")
                    : session.CanAdvanceDay ? "今天的解释留下了。按 Esc，结束今天，再回来看看。"
                    : s.Knows(DemoRules.Sea) && !s.HasFact(DemoRules.PaintingSea) ? "画架还在等那片海。先亲自看一眼画。"
                    : s.HasFact(DemoRules.PaperRevealed) ? "画架前显露了一张旧草稿。拿近看看。"
                    : s.Knows(DemoRules.Far) ? "背景需要距离。你会把远方留在哪里？"
                    : s.HasFact(NarrativeRules.DayTwo) ? "再翻一遍旧书，读读后面的地平线。"
                    : s.Knows(DemoRules.Growth) ? "把生长留给画布。或重新看待别的东西。"
                    : s.Knows(ExplorationRules.Sprout) && s.Knows(ExplorationRules.TowardLight) ? "新芽，向光。让这两个细节在纸面靠近。"
                    : "拿近看看盆栽和旧书，亲自读到值得记下的细节。";
                return;
            }
            if (s.HasFact(MainlineRules.Complete)) { objectiveText.text="营火留在星空下，熟悉的房间已经没有边界。"; return; }
            if (s.HasFact(MainlineRules.CeilingStarry))
            {
                objectiveText.text=s.Knows(MainlineRules.Forest) ? "把森林与亲自观察过的房间放在一起。"
                    : s.Knows(MainlineRules.TreeShadow) && s.Knows(MainlineRules.WindSound) ? "树影和风声，可以联系成一个新的想法。"
                    : "重新看看窗外，再看看脚下的房间。星空带来了新的细节。";
                return;
            }
            if(s.HasFact(MainlineRules.NightArrived))
            {
                objectiveText.text=s.Knows(MainlineRules.Starry) ? "用星空重新解释头顶的天花板。"
                    : s.Knows(MainlineRules.Night) && s.Knows(MainlineRules.Boundless) ? "夜色与无边。让它们在笔记中靠近。"
                    : "第三天，抬头观察天花板，再看看窗外的尽头。";
                return;
            }
            if(s.HasFact(DemoRules.PaintingSea) && session.CanAdvanceDay)
            { objectiveText.text="让海留在画里。按 Esc，选择结束今天，明天再看房间。"; return; }
            if (session.Rules.Observations.Count > 0 && !s.HasFact(DemoRules.BookFallen))
            {
                objectiveText.text = s.HasFact(ExplorationRules.PlantStartsGrowing) ? "解释已留下。今天先到这里，明天再看看它。"
                    : s.Knows(DemoRules.Growth) ? "你想到了生长。用这个意义，重新看待盆栽。"
                    : s.Knows(ExplorationRules.Sprout) && s.Knows(ExplorationRules.TowardLight) ? "土边的新芽，书中的向光。按 N 将细节联系起来。"
                    : s.Knows(DemoRules.Plant) ? "旋转盆栽看看土边；旧书中也有值得细读的短句。"
                    : "选择一件事物，拿近看看。按 H 查看提示。";
                return;
            }
            if (s.HasFact(ExplorationRules.WorldStarry))
            {
                objectiveText.text = "房间的边界正在退去。熟悉的东西会留在星空下。";
                return;
            }
            objectiveText.text = presentation != null && presentation.IsBenchmarkPending ? "你将海赋予画。房间正在回应。"
                : s.HasFact(DemoRules.Complete) ? "房间记得。三次解释，一个开始回应的世界。"
                : s.Knows(DemoRules.Sea) && !s.Knows(DemoRules.Painting) ? "已收集海。请先点击观察墙上的画。"
                : s.Knows(DemoRules.Sea) ? (showHint ? "提示：在笔记本中组合「海 + 画」。" : "纸上有一片海。什么能够容纳它？")
                : s.HasFact(DemoRules.PaperRevealed) ? "风吹出了一张纸。调查地毯上的纸张。"
                : s.Knows(DemoRules.Far) && !s.Knows(DemoRules.Window) ? "已收集远方。请先点击观察窗户。"
                : s.Knows(DemoRules.Far) ? (showHint ? "提示：在笔记本中组合「远方 + 窗户」。" : "旅行书描绘着远方。看看房间之外。")
                : s.HasFact(DemoRules.BookFallen) ? "有东西落下了。调查地面上的红色书本。"
                : s.Knows(DemoRules.Growth) ? (showHint ? "提示：在笔记本中组合「盆栽 + 生长」。" : "已收集两个意义。按 N 打开笔记本，试着组合。")
                : "观察盆栽，探索房间。按 H 查看提示。";
        }
        // 显示词表独立于配方判断，翻译不改变匹配。

        /// <summary>从内容定义取得中文碎片名称。</summary>
        private string ClueName(ClueId id) => session.Rules.Clues.Single(x => x.Id == id).DisplayName;
        /// <summary>使用输入碎片的中文名称生成组合描述。</summary>
        private string RecipeName(SemanticRecipe recipe) => string.Join(" + ", recipe.Inputs.Select(ClueName));
        /// <summary>处理调试开关与提示键；事实输入框聚焦时不抢按键。</summary>
        private void Update()
        {
            // 等视觉完成再给玩家结束文字；调试事实列表仍即时反映领域真相。
            if (waitedForBenchmark && presentation != null && !presentation.IsBenchmarkPending)
            {
                waitedForBenchmark = false;
                observationText.text = "静止的房间，已经有了自己的呼吸。";
                Render();
            }
            var keyboard = Keyboard.current; if (keyboard == null || factInput.isFocused) return;
            if (developmentMode && keyboard.f6Key.wasPressedThisFrame) jumpTo(DebugPresets.BeforeSea);
            if (developmentMode && keyboard.f3Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
            if (keyboard.hKey.wasPressedThisFrame) { showHint = true; Render(); if (transientHint != null) { transientHint.Clear(); transientHint.Show(objectiveText.text); } }
        }
        /// <summary>解除会话事件监听。</summary>
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
