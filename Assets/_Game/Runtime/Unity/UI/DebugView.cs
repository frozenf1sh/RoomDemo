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
        private GameSession session;
        // 保留少量事件便于排查；不以日志或界面文字作为世界真相。
        private readonly Queue<string> recent = new Queue<string>();
        private bool showHint;
        /// <summary>玩家当前看到的中文引导文案。</summary>
        public string Objective => objectiveText.text;
        /// <summary>注入状态区、事件区与玩家引导文本。</summary>
        public void Configure(GameObject body, Text state, Text events, Text objective) { panel = body; stateText = state; eventText = events; objectiveText = objective; }
        /// <summary>注入调试按钮、事实输入框和预设按钮。</summary>
        public void ConfigureTools(Text observations, Button restart, Button reset, Button grant, Button add, InputField input, Button[] presets)
        { observationText = observations; restartButton = restart; resetButton = reset; grantButton = grant; addFactButton = add; factInput = input; presetButtons = presets; }
        /// <summary>重绑当前会话与显式操作回调，刷新中文状态显示。</summary>
        public void Bind(GameSession game, Action restart, Action<DebugPreset> jump, Action<FactId> addFact)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game; session.EventRaised += OnEvent; recent.Clear(); eventText.text = ""; showHint = false;
            observationText.text = "环顾房间，收集观察到的碎片，再将意义赋予这里。按 H 查看提示。";
            // 所有操作都走显式应用层回调或 Session，UI 不写 GameObject 状态。
            Wire(restartButton, () => restart()); Wire(resetButton, () => restart()); Wire(grantButton, () => session.DebugGrantAllClues());
            Wire(addFactButton, () => { try { addFact(new FactId(factInput.text.Trim())); } catch (ArgumentException e) { eventText.text = e.Message; } });
            for (int i = 0; i < presetButtons.Length; i++) { var preset = DebugPresets.All[i]; Wire(presetButtons[i], () => jump(preset)); }
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
            if (e is CombinationSucceeded) observationText.text = "你的解释已成为房间的一部分。找找发生了什么变化。";
            // 玩家引导只是对已发生事实的解释，不能触发下一步规则。
            if (e is FactAdded fact && fact.Fact == DemoRules.WindEntering) observationText.text = "你没有解释风。长大的盆栽与通往远方的窗户开始相互回应，一张纸滑了出来。";
            if (e is FactAdded end && end.Fact == DemoRules.Complete) observationText.text = "海触及了你先前建立的意义。你没有再次解释，盆栽与窗户却自行改变了。";
            // 详细日志按运行环境开关，Release 中不输出冗余事件。
            if ((UnityEngine.Application.isEditor || Debug.isDebugBuild) &&
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
            if (UnityEngine.Application.isEditor || Debug.isDebugBuild)
                stateText.text += "\n\n当前可用的配方\n" + string.Join("\n", session.Rules.AvailableRecipes(s).Select(RecipeName));
            objectiveText.text = s.HasFact(DemoRules.Complete) ? "房间记得。三次解释，一个开始回应的世界。"
                : s.Knows(DemoRules.Sea) ? (showHint ? "提示：在笔记本中组合「海 + 画」。" : "纸上有一片海。什么能够容纳它？")
                : s.HasFact(DemoRules.PaperRevealed) ? "风吹出了一张纸。调查地毯上的纸张。"
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
            var keyboard = Keyboard.current; if (keyboard == null || factInput.isFocused) return;
            if (keyboard.f3Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
            if (keyboard.hKey.wasPressedThisFrame) { showHint = !showHint; Render(); }
        }
        /// <summary>解除会话事件监听。</summary>
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
