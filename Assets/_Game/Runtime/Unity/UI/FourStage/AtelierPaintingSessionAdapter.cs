// 文件：AtelierPaintingSessionAdapter.cs
// 职责：将已有会话与画布端口投影为正式UI合同。边界：不改全局输入或底层画布。
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEngine;
namespace Game.Unity.UI.FourStage
{
    /// <summary>由场景组合根注入Gameplay，不使用Find搜索外部对象。</summary>
    public sealed class AtelierPaintingSessionAdapter : MonoBehaviour, IAtelierPaintingPort
    {
        public AtelierFlowGameplay Gameplay;
        public AtelierCanvasWorkspace Workspace;
        private bool attached;
        private void LateUpdate()
        {
            if (attached || Gameplay == null || Gameplay.Session == null || Gameplay.CanvasPort.Document == null) return;
            Gameplay.SetExternalCanvasWorkspaceActive(true);
            Workspace.Bind(this);
            attached = true;
        }
        private void OnDisable()
        {
            if (!attached) return;
            Workspace.Bind(null);
            if (Gameplay != null) Gameplay.SetExternalCanvasWorkspaceActive(false);
            attached = false;
        }
        /// <summary>每次从唯一账本读取，三四层缺项与领域收层规则保持一致。</summary>
        public AtelierPaintingSnapshot Read()
        {
            if (!attached || Gameplay == null || Gameplay.Session == null) return null;
            var session = Gameplay.Session;
            var canvas = Gameplay.CanvasPort;
            var layer = canvas.Document.Current;
            bool known = session.State.Knows(AtelierFlowRules.Brush(layer.Id));
            bool locked = session.State.CanvasLocked || session.State.HasFact(AtelierFlowRules.WorldCommitted);
            string reason=!known?"先在笔记中形成这一层笔刷。":AtelierCanvasOperations.FinishBlockedReason(session.State,session.Rules,canvas.Document.Revision)??"";
            bool knife=Gameplay.StageFour!=null&&Gameplay.StageFour.Tool.Mode==AtelierFinaleToolMode.Face;
            bool visible = Gameplay.GameplayInputEnabled && !Gameplay.IsPaused && Gameplay.IsCanvasFocused && !Gameplay.References.Notebook.IsOpen;
            return new AtelierPaintingSnapshot(session.SessionEpoch, canvas.Document.Revision, layer.Id,
                AtelierFlowRules.Brush(layer.Id).Value, known ? canvas.Brushes[layer.Id - 1] : null, canvas.CanvasTexture,
                layer.RemainingPaint, layer.Count, layer.PlannedStrokes, layer.MinCost, layer.MaxCost, canvas.DefaultCost, 0,
                !locked && !layer.Sealed && layer.Count > 0, reason == "", reason, visible,true,knife?AtelierToolMode.Knife:AtelierToolMode.Brush,layer.Id==4&&layer.Count>=3&&!locked);
        }
        /// <summary>沿用真实世界最近射线命中，不能拿UI矩形冒充世界UV。</summary>
        public bool TryMap(Vector2 screen, out Vector2 uv)
        {
            uv = default;
            if (!Allowed()) return false;
            var refs = Gameplay.References;
            var hits = Physics.RaycastAll(refs.ViewCamera.ScreenPointToRay(screen), 40, refs.PickingMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            if (hits.Length == 0) return false;
            var target = hits[0].collider.GetComponent<AtelierInteractionTarget>();
            if (target == null || target.Kind != AtelierTargetKind.Painting) return false;
            uv = hits[0].textureCoord;
            return true;
        }
        private bool Allowed() => attached && Read()?.IsVisible == true;
        /// <summary>预览归已有CanvasPort所有，取消不改变作品。</summary>
        public void Preview(Vector2 uv, bool visible, int cost, int angle)
        {
            if (Gameplay != null && Gameplay.CanvasPort != null && Gameplay.CanvasPort.Document != null)
                Gameplay.CanvasPort.Preview(uv, visible && Allowed(), cost, angle);
        }
        /// <summary>应用层使用按下时世代与版本，拒绝过期请求且不扣量。</summary>
        public string RequestCommitStroke(AtelierStrokeIntent intent, Vector2 uv)
        {
            if (!Allowed()) return "当前不能落笔。";
            var snapshot = intent.Snapshot;
            var now = Read();
            if (snapshot.SessionEpoch != now.SessionEpoch || snapshot.CanvasRevision != now.CanvasRevision)
                return "画面已经更新，请重新落笔。";
            if (!Gameplay.CanvasPort.IsPlacementValid(uv, intent.Cost, intent.Angle)) return "笔印需要更多地落在画布内。";
            var stroke = new AtelierStroke(intent.StrokeId, snapshot.LayerId,
                Mathf.RoundToInt(uv.x * 10000), Mathf.RoundToInt(uv.y * 10000), intent.Angle, intent.Cost);
            return Gameplay.Session.CommitAtelierStroke(snapshot.SessionEpoch, snapshot.CanvasRevision, stroke).Reason;
        }
        /// <summary>撤销复用真实命令；UI不退款。</summary>
        public string RequestUndo(AtelierPaintingSnapshot snapshot) => !Allowed() ? "当前不能撤销。" :
            Gameplay.Session.UndoAtelierStroke(snapshot.SessionEpoch, snapshot.CanvasRevision).Reason;
        /// <summary>收层复用应用层复查，不自行推进阶段。</summary>
        public string RequestFinishLayer(AtelierPaintingSnapshot snapshot) => !Allowed() ? "当前不能完成。" :
            Gameplay.Session.FinishAtelierLayer(snapshot.SessionEpoch, snapshot.CanvasRevision).Reason;
        /// <summary>归还机位，不清除作品。</summary>
        public void RequestReturn() { if (Allowed()) Gameplay.ReturnToRoom(); }
        /// <summary>后续刀面动作尚未提供，第一批不假装可用。</summary>
        public string RequestToolMode(AtelierToolMode mode)
        {
            if(!Allowed()||Gameplay.StageFour==null)return "当前不能使用刮刀。";
            Gameplay.StageFour.Tool.SelectMode(mode==AtelierToolMode.Knife?AtelierFinaleToolMode.Face:AtelierFinaleToolMode.Paint);
            return "";
        }
    }
}
