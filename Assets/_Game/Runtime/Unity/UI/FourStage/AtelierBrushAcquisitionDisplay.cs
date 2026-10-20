// 文件：AtelierBrushAcquisitionDisplay.cs
// 职责：在既有笔记组合区旁展示刚刚形成的四阶段笔刷原图。
// 边界：只读取配方成功事件与原始 PNG，不改变笔记输入、藏品条件或 GameState。
using System.Linq;
using Game.Application;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity.UI.FourStage
{
    /// <summary>笔刷形成图卡的显式控件引用，由场景接入代码创建。</summary>
    public sealed class AtelierBrushAcquisitionDisplay : MonoBehaviour
    {
        public AtelierFlowGameplay Gameplay;
        public CanvasGroup Group;
        public RawImage Preview;
        public AspectRatioFitter Aspect;
        public Text Title;
        private GameSession session;

        private void Update()
        {
            if (session != null || Gameplay == null || Gameplay.Session == null) return;
            session = Gameplay.Session;
            session.EventRaised += OnEvent;
            Gameplay.References.Notebook.OpenChanged += OnNotebookChanged;
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (session != null) session.EventRaised -= OnEvent;
            if (Gameplay != null && Gameplay.References != null && Gameplay.References.Notebook != null)
                Gameplay.References.Notebook.OpenChanged -= OnNotebookChanged;
            session = null;
        }

        private void OnNotebookChanged(bool open)
        {
            if (!open) SetVisible(false);
        }

        private void OnEvent(DomainEvent raised)
        {
            if (!(raised is CombinationSucceeded formed) || !Gameplay.References.Notebook.IsOpen) return;
            for (int layer = 1; layer <= 4; layer++)
            {
                var brush = AtelierFlowRules.Brush(layer);
                if (!formed.ProducedClues.Contains(brush) || !session.State.Knows(brush)) continue;
                var definition = session.Rules.Clues.First(item => item.Id == brush);
                var texture = Gameplay.CanvasPort.Brushes[layer - 1];
                Preview.texture = texture;
                Preview.enabled = texture != null;
                if (texture != null) Aspect.aspectRatio = texture.width / (float)texture.height;
                Title.text = "形成了" + definition.DisplayName;
                SetVisible(true);
                break;
            }
        }

        private void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1 : 0;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }
    }
}
