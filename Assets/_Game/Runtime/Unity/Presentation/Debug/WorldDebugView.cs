// =============================================================================
// 文件：WorldDebugView.cs
// 职责：提供星空样板、表现恢复和两件检视主物体的明确开发入口。
// 边界：UI 只调用启动组件或检视适配器；不直接改世界 GameObject 或 GameState。
// =============================================================================
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>一个具体开发面板；正式玩家模式不接受 F7 或显示状态。</summary>
    public sealed class WorldDebugView : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private StarryWorldSequence sequence;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private InspectableObject plant, book;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text status;
        [SerializeField] private Button[] buttons;
        private bool developmentMode;
        private string previousBeat;
        private int previousSecond = -1;
        /// <summary>显式注入应用入口与 UI 按钮，避免每帧全场景查找。</summary>
        public void Configure(GameBootstrap game, StarryWorldSequence director, InspectionController closeView,
            InspectableObject pottedPlant, InspectableObject oldBook, GameObject body, Text label, Button[] controls)
        { bootstrap = game; sequence = director; inspection = closeView; plant = pottedPlant; book = oldBook; panel = body; status = label; buttons = controls; }
        /// <summary>正式模式强制收起开发面板。</summary>
        public void SetDevelopmentMode(bool enabled) { developmentMode = enabled; if (!enabled) panel.SetActive(false); }
        private void Awake()
        {
            UnityEngine.Events.UnityAction[] actions = {
                bootstrap.PlayStarryBenchmark, bootstrap.PreviewStarryBenchmark, bootstrap.ResetStarryPresentation, bootstrap.RestorePresenters,
                () => bootstrap.JumpTo(ExplorationPresets.BeforeStarryWorld), () => bootstrap.JumpTo(ExplorationPresets.StarryWorldFinal),
                () => Inspect(plant), () => Inspect(book) };
            for (int i = 0; i < buttons.Length; i++) { buttons[i].onClick.RemoveAllListeners(); buttons[i].onClick.AddListener(actions[i]); }
        }
        private void Inspect(InspectableObject item)
        {
            // 开发入口也收起菜单再拿近，避免按钮遮住书页或盆栽细节。
            panel.SetActive(false); inspection.Enter(item);
        }
        private void Update()
        {
            if (!developmentMode) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
            if (!panel.activeSelf) return;
            int second = Mathf.FloorToInt(sequence.Elapsed);
            if (previousBeat == sequence.CurrentBeat && previousSecond == second) return;
            previousBeat = sequence.CurrentBeat; previousSecond = second;
            status.text = sequence.CurrentBeat + "\n" + second + " 秒 / " + sequence.Duration.ToString("F0") + " 秒\n预览不改世界事实；触发按钮经应用层提交事实。";
        }
    }
}
