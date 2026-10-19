// 文件：AtelierBrushResultAdapter.cs
// 职责：把真实笔刷形成事件接到最小结果页。边界：只读规则和已知材料，不自动开画架。
using System.Linq;
using Game.Application;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEngine;
namespace Game.Unity.UI.FourStage
{
    /// <summary>组合根注入引用，绑定和解绑对称执行。</summary>
    public sealed class AtelierBrushResultAdapter : MonoBehaviour
    {
        public AtelierFlowGameplay Gameplay;
        public AtelierBrushResultView View;
        private GameSession session;
        private void Update()
        {
            if (session != null || Gameplay == null || Gameplay.Session == null) return;
            session = Gameplay.Session;
            session.EventRaised += OnEvent;
            Gameplay.References.Notebook.OpenChanged += OnNotebook;
            View.CloseNotebookRequested += CloseNotebook;
        }
        private void OnDisable()
        {
            if (session == null) return;
            session.EventRaised -= OnEvent;
            if (Gameplay != null) Gameplay.References.Notebook.OpenChanged -= OnNotebook;
            View.CloseNotebookRequested -= CloseNotebook;
            View.Hide();
            session = null;
        }
        private void OnNotebook(bool open) { if (!open) View.Hide(); }
        private void CloseNotebook() => Gameplay.References.Notebook.SetOpen(false);
        private void OnEvent(DomainEvent raised)
        {
            if (!(raised is CombinationSucceeded formed)) return;
            // 只认稳定笔刷身份，不能把所有Concept或中文名字当笔刷。
            for (int layer = 1; layer <= 4; layer++)
            {
                var id = AtelierFlowRules.Brush(layer);
                if (!formed.ProducedClues.Contains(id) || !session.State.Knows(id)) continue;
                ShowKnownBrush(layer);
                break;
            }
        }
        /// <summary>展示已经获得的笔刷；来源只读真实配方输入，不暴露未来配方。</summary>
        public void ShowKnownBrush(int layer)
        {
            if (session == null || layer < 1 || layer > 4 || !Gameplay.References.Notebook.IsOpen) return;
            var id = AtelierFlowRules.Brush(layer);
            if (!session.State.Knows(id)) return;
            var definition = session.Rules.Clues.First(x => x.Id == id);
            var recipe = session.Rules.Recipes.FirstOrDefault(x => x.ProducesClues.Contains(id) && session.State.HasCompleted(x.Id));
            string sources = recipe == null ? "来源已记录在笔记中。" : "构成来源：" + string.Join("、",
                recipe.Inputs.Where(session.State.Knows).Select(input => session.Rules.Clues.First(x => x.Id == input).DisplayName));
            View.Show(definition.DisplayName, Gameplay.CanvasPort.Brushes[layer - 1], sources);
        }
    }
}
