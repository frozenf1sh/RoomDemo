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
            CompareButton.onClick.AddListener(CompareWindow);
            StudyButton.onClick.AddListener(StudyPalette);
            TryBrushButton.onClick.AddListener(Palette.TryBrush);
            EdgeButton.onClick.AddListener(()=>Palette.SelectMode(false));
            FaceButton.onClick.AddListener(()=>Palette.SelectMode(true));
            LocateButton.onClick.AddListener(()=>Flow.OpenPaletteForPractice());
            WindowNear.SetActive(false);
            WindowFar.SetActive(false);
            Panel.SetActive(false);
        }
        /// <summary>同一近景亲点两处真实画面；比较前不授星辰。</summary>
        public void ObserveWindowDetail(bool far)
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsWindowFocused) return;
            session.PerformAction(StudioRules.Window,far?"window_far":"window_near");
        }
        /// <summary>亲看两处后明确退远比较；原稿O10与星辰在该命令当次出现。</summary>
        public void CompareWindow()
        {
            if (!Flow.CanUseStageThreeInput || !Flow.IsWindowFocused) return;
            if (!session.IsActionAvailable(StudioRules.Window,"window_compare")) return;
            session.PerformAction(StudioRules.Window,"window_compare");
            Flow.CompareWindowView();
        }
        /// <summary>细看结壳才取得混乱，拿起板或工具本身不直接发灰词。</summary>
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
            bool locate=world && Flow.IsCanvasFocused && Flow.CanvasPort.Document.Current.Id==3
                && (!session.State.HasFact(AtelierFlowRules.DryLearned)||!session.State.HasFact(AtelierFlowRules.WetLearned));
            WindowNear.SetActive(window);
            WindowFar.SetActive(window);
            // 窗外热点在窗平面上，避免被整扇窗的入口碰撞盒抢走点击；离开立即恢复入口。
            WindowPicking.enabled=!Flow.IsWindowFocused;
            Panel.SetActive(window || palette || locate);
            CompareButton.gameObject.SetActive(window);
            CompareButton.interactable=session.IsActionAvailable(StudioRules.Window,"window_compare");
            StudyButton.gameObject.SetActive(palette);
            StudyButton.interactable=session.IsActionAvailable(AtelierFlowRules.DryPalette,"palette_inspect");
            TryBrushButton.gameObject.SetActive(palette);
            EdgeButton.gameObject.SetActive(palette);
            FaceButton.gameObject.SetActive(palette);
            EdgeButton.interactable=FaceButton.interactable=Palette.Equipped;
            LocateButton.gameObject.SetActive(locate);
            if (window) Instruction.text=session.State.HasFact(AtelierFlowRules.WindowCompared)?"窗灯也可以是星星。"
                : "点近处屋檐和远处灯火，再离远看看。";
            else if (palette)
            {
                bool dry=session.State.HasFact(AtelierFlowRules.DryLearned),wet=session.State.HasFact(AtelierFlowRules.WetLearned);
                Instruction.text=!session.State.Knows(AtelierFlowRules.DryPalette)?"先点颜料板，看看干掉的颜料。"
                    : !session.State.HasFact(AtelierFlowRules.PaletteStudied)?"细看结壳的颜料，再试试板旁的刮刀。"
                    : !Palette.Equipped?"拿起板旁的刮刀，试试刀刃和刀面。"
                    : dry&&wet?"两面都试过了，可以回画布继续画。"
                    : dry&&!wet&&!Palette.FaceMode?"干颜料刮开了。切到‘刀面推色’，试试右侧软色。"
                    : wet&&!dry&&Palette.FaceMode?"软色推开了。切到‘刀刃揭底’，试试左侧干色。"
                    : (Palette.FaceMode?"刀面：拖过右侧软色，把颜色推开。":"刀刃：拖过左侧干色，露出木底。")+"\n也可以在对应试区短点三次。";
            }
            else if (locate) Instruction.text="完成这一层前，先在颜料板试试刀刃和刀面。";
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
