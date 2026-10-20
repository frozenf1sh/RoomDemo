// 文件：AtelierFlowGameplay.DebugStageThree.cs
// 职责：提供显式开发入口，把新流程快进到第三阶段开始的真实会话状态。
// 边界：只补前两阶段已经发生的材料、事实和合法画布笔触；不授予第三阶段观察或笔刷。
using System.Collections.Generic;
using Game.Domain;
using UnityEngine;
namespace Game.Unity.ArtRebuild
{
    /// <summary>第三阶段快进仍是正常GameSession；后续调查和配方全部由玩家重新完成。</summary>
    public sealed partial class AtelierFlowGameplay
    {
        /// <summary>前两阶段来源、观察、概念与前两枚笔刷；第三阶段窗景、板、刀不从调试发放。</summary>
        private static readonly ClueId[] OpeningClues =
        {
            new ClueId(AtelierRules.Manuscript.Value),
            new ClueId(StudioRules.Cup.Value),
            new ClueId(AtelierRules.Brush.Value),
            new ClueId(AtelierFlowRules.Crayon.Value),
            new ClueId(AtelierRules.Painting.Value),
            AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue"),
            AtelierFlowRules.C("flow"),AtelierFlowRules.C("free"),
            new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl"),
            AtelierFlowRules.Brush(1),
            new ClueId(AtelierFlowRules.Plaster.Value),
            new ClueId(AtelierFlowRules.HandMirror.Value),
            new ClueId(AtelierFlowRules.DryBrush.Value),
            new ClueId(AtelierFlowRules.MirrorShards.Value),
            AtelierFlowRules.C("fixed"),AtelierFlowRules.C("flip"),
            AtelierFlowRules.C("black"),AtelierFlowRules.C("sharp"),
            new ClueId("atelier.flow.twisted"),new ClueId("atelier.flow.rough"),
            AtelierFlowRules.Brush(2)
        };

        /// <summary>快进到第三阶段开始：前两层已收笔，N04已跳过，灯、屋顶和第四阶段均未推进。</summary>
        /// <remarks>显式开发入口，不是正常玩家流程，也不写入磁盘存档。</remarks>
        public void FastForwardToThirdStage()
        {
            if (Session == null || IsEnding) return;
            // 前两阶段走正常线索/事实与画布命令；Finished事实仍由画布收层产生。
            if (!Session.State.HasFact(AtelierFlowRules.Finished(1)))
            {
                Session.DebugGrantClues(OpeningClues);
                Session.DebugAddFact(AtelierFlowRules.IntroRead);
                Session.DebugAddFact(AtelierFlowRules.Washed);
                Session.DebugAddFact(AtelierFlowRules.MirrorCompared);
                PaintFirstLayer();
            }
            if (!Session.State.HasFact(AtelierFlowRules.Finished(2))) PaintSecondLayer();
            SetThirdStageEntry();
        }

        /// <summary>第一层按正常三笔与收层完成；Finished(1)仍由画布命令发布。</summary>
        private void PaintFirstLayer()
        {
            if (CanvasPort == null || CanvasPort.Document == null || CanvasPort.Document.Current.Id != 1) return;
            foreach (var point in new[]
                     {
                         new Vector2(.30f,.58f),new Vector2(.64f,.48f),new Vector2(.48f,.73f)
                     })
                CanvasPort.Commit(point,CanvasPort.DefaultCost,0);
            CanvasPort.Finish();
        }

        /// <summary>第二层按正常五笔完成，保留第一层并推进Finished(2)。</summary>
        private void PaintSecondLayer()
        {
            if (CanvasPort == null || CanvasPort.Document == null || CanvasPort.Document.Current.Id != 2) return;
            for (int index = 0; index < 5; index++)
                CanvasPort.Commit(new Vector2(.18f+index*.15f,.30f+index*.06f),CanvasPort.DefaultCost,index*3);
            CanvasPort.Finish();
        }

        /// <summary>收掉快进过程中触发的N03/N04与镜头过渡，恢复到第三阶段固定主镜头。</summary>
        private void SetThirdStageEntry()
        {
            // 先清旁白队列，再覆盖阶段显示，避免Finished事件排入的旧过场继续播放。
            if (Voice != null)
            {
                Voice.gameObject.SetActive(false);
                Voice.gameObject.SetActive(true);
            }
            if (movement != null) { StopCoroutine(movement); movement = null; }
            if (observationPaging != null) { StopCoroutine(observationPaging); observationPaging = null; }
            transitioning = false; paused = false; captured = false; focus = "";
            IsOpening = false; IsStageTransition = false;
            pendingNarration = ""; pendingStage = 3; waitingForReveal = false;
            blockedThroughFrame = Time.frameCount + 1;
            Time.timeScale = 1;

            CancelPointer();
            ClearHover();
            HideWaterObservation();
            if (Wash != null) Wash.CancelContact();
            if (Mirror != null) Mirror.End();
            if (References.Lamp != null) References.Lamp.PutMatchDown();
            if (StageThree != null && StageThree.Palette != null) StageThree.Palette.CancelGesture();
            if (StageFour != null && StageFour.Tool != null) StageFour.Tool.Cancel();

            References.ViewCamera.transform.SetPositionAndRotation(home,homeTurn);
            References.ViewCamera.fieldOfView = homeFov;
            References.PausePanel.SetActive(false);
            References.ManuscriptPanel.SetActive(false);
            References.ContextPanel.SetActive(false);
            References.ContextAction.gameObject.SetActive(false);
            References.ReturnButton.gameObject.SetActive(false);
            References.Reminder.gameObject.SetActive(true);
            References.Notebook.SetOpen(false);
            References.Notebook.ExternalInputBlocked = false;

            Draft.RestoreStage(3,true);
            Draft.SkipOpening();
        }
    }
}
