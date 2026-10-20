// 文件：AtelierFlowStageThree.cs
// 职责：协调第三阶段窗景热点、板面工具与可替换功能控件。
// 边界：不拥有正式UI母版、颜料账本或第二套规则；所有事实通过同一GameSession提交。
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>本阶段的世界接口；正式UI可替换Panel并复用这些入口。</summary>
    public sealed class AtelierFlowStageThree : MonoBehaviour
    {
        public AtelierFlowGameplay Flow;
        public AtelierFlowPalette Palette;
        public Transform WindowFocus, WindowCompareFocus, PaletteFocus;
        public Collider WindowPicking;
        public GameObject WindowNear,WindowFar;
        public GameObject Panel;
        public Text Instruction;
        public Button CompareButton,StudyButton,TryBrushButton,EdgeButton,FaceButton,LocateButton;
        private GameSession session;
        /// <summary>绑定同一会话，按钮不直接修改工具经验或观察。</summary>
        public void Bind(GameSession game)
        {
            session=game;
            Palette.Bind(game);
            WindowNear.SetActive(false);
            WindowFar.SetActive(false);
            Panel.SetActive(false);
        }
        /// <summary>同一近景亲点两处真实画面；比较前不授星辰。</summary>
        public void ObserveWindowDetail(bool far)
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsWindowFocused) return;
            var source=far?AtelierFlowRules.FarWindowLight:AtelierFlowRules.NearWindowLight;
            session.Inspect(source);
            session.PerformAction(source,far?"window_far":"window_near");
            if(session.State.Knows(AtelierFlowRules.NearWindowLight)&&session.State.Knows(AtelierFlowRules.FarWindowLight))
                Flow.Voice.Play("G_WINDOW_BOTH");
            else Flow.Voice.Play(far?"G_WINDOW_NEAR":"G_WINDOW_FAR");
        }
        /// <summary>兼容旧入口，只播放两处均已亲看的比较台词，不授予材料。</summary>
        public void CompareWindow()
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsWindowFocused) return;
            if(session.State.Knows(AtelierFlowRules.NearWindowLight)&&session.State.Knows(AtelierFlowRules.FarWindowLight))
                Flow.Voice.Play("G_WINDOW_BOTH");
        }
        /// <summary>兼容旧细看入口；拿起板或工具本身不直接发干湿观察。</summary>
        public void StudyPalette()
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsPaletteFocused) return;
            session.PerformAction(AtelierFlowRules.DryPalette,"palette_inspect");
        }
        private void Update()
        {
            if (session==null) return;
            bool world=Flow.CanUseStageThreeInput;
            bool window=world && Flow.IsWindowFocused;
            bool palette=world && Flow.IsPaletteFocused;
            WindowNear.SetActive(window);WindowFar.SetActive(window);
            WindowPicking.enabled=!Flow.IsWindowFocused;
            // 世界交互只保留实际物体与短旁白，旧功能面板不再进入玩家画面。
            Panel.SetActive(false);
            if (!palette) Palette.CancelGesture();
        }
        private void OnDisable()
        {
            if (Panel!=null) Panel.SetActive(false);
            if (WindowNear!=null) WindowNear.SetActive(false);
            if (WindowFar!=null) WindowFar.SetActive(false);
            if (WindowPicking!=null) WindowPicking.enabled=true;
        }
    }
}
