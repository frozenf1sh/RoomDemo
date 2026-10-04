// =============================================================================
// 文件：PresentationDebugView.cs
// 职责：提供独立节拍预览、完整预览、恢复与窗口评审入口。
// 边界：按钮只调用演出导演；不执行配方，不授予碎片，不修改领域状态。
// =============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>只调表现的开发面板，与领域调试面板明确分开。</summary>
    public sealed class PresentationDebugView : MonoBehaviour
    {
        [SerializeField] private PaintingSeaSequence sequence;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text status;
        [SerializeField] private Button[] buttons;
        private string previousBeat;
        private int previousSecond = -1;
        /// <summary>由编辑器显式配置全部按钮和导演。</summary>
        public void Configure(PaintingSeaSequence director, GameObject body, Text label, Button[] controls)
        { sequence = director; panel = body; status = label; buttons = controls; }
        /// <summary>仅初始化一次，重开会话不会叠加按钮监听。</summary>
        private void Awake()
        {
            UnityEngine.Events.UnityAction[] actions = { sequence.PlayPaintingPreview, sequence.PlayWaterPreview, sequence.PlayPlantPreview,
                sequence.PlayWindowPreview, sequence.PlayBenchmarkPreview, sequence.ResetPresentation, sequence.ApplyFinalPreview, sequence.ApplyFinalStateImmediately };
            for (int i = 0; i < buttons.Length; i++) { buttons[i].onClick.RemoveAllListeners(); buttons[i].onClick.AddListener(actions[i]); }
        }
        /// <summary>读取表现进度；只有节拍或整秒变化才刷新文字。</summary>
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
            // 窗口化只用于实际程序的截图和帧率评审，不改变游戏机位或世界状态。
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) Screen.SetResolution(1440, 900, FullScreenMode.Windowed);
            if (!panel.activeSelf) return;
            int second = Mathf.FloorToInt(sequence.Elapsed);
            if (previousBeat == sequence.CurrentBeat && previousSecond == second) return;
            previousBeat = sequence.CurrentBeat; previousSecond = second;
            status.text = sequence.CurrentBeat + "\n" + second + " 秒 / " + sequence.Duration.ToString("F0") + " 秒\n预览只改变表现，领域事实保持原样。";
        }
    }
}
