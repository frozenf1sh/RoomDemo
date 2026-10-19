// 文件：FakePaintingPreview.cs
// 职责：编辑器独立预览的固定快照与请求观察。边界：不进入Player，不实例化规则引擎或正式会话。
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.UI.FourStage
{
    /// <summary>固定用例供Inspector切换，不计算第二套预算，也不把假图写回世界。</summary>
    public sealed class FakePaintingPreview : MonoBehaviour, IAtelierPaintingPort
    {
        /// <summary>预览状态覆盖首批合同的可见边界。</summary>
        public enum Scenario { Empty, TwoStrokes, ThreeStrokes, LastStroke, Exhausted, KnifeBlocked, NoBrush, Rejected, LongCopy, Opening, Submitting }
        public Scenario Case;
        public Texture2D[] Brushes;
        public AtelierCanvasWorkspace Workspace;
        public AtelierBrushResultView Result;
        public RectTransform Surface;
        public RawImage Ghost;
        public Text Evidence;
        public Button Next, Previous, ShowResult;
        private Scenario previous;
        private int revision;
        private void OnEnable()
        {
            Workspace.Bind(this);
            Next.onClick.AddListener(NextCase);
            Previous.onClick.AddListener(PreviousCase);
            ShowResult.onClick.AddListener(OpenResult);
            Result.CloseNotebookRequested += CloseResult;
        }
        private void OnDisable()
        {
            Workspace.Bind(null);
            Next.onClick.RemoveListener(NextCase);
            Previous.onClick.RemoveListener(PreviousCase);
            ShowResult.onClick.RemoveListener(OpenResult);
            Result.CloseNotebookRequested -= CloseResult;
        }
        private void NextCase() { Case = (Scenario)(((int)Case + 1) % 11); }
        private void PreviousCase() { Case = (Scenario)(((int)Case + 10) % 11); }
        private void OpenResult() => Result.Show("卷涡笔刷", Brushes[0], "构成来源：冰蓝、卷涡");
        private void CloseResult() => Evidence.text = "预览：收到收起笔记意图；不进入画布。";
        /// <summary>固定数值用于布局检查，按钮不会模拟真实账本。</summary>
        public AtelierPaintingSnapshot Read()
        {
            if (previous != Case) { previous = Case; revision++; }
            int count = 0, remaining = 100, min = 12, max = 24, cost = 17;
            if (Case == Scenario.TwoStrokes) { count = 2; remaining = 64; cost = 16; }
            if (Case == Scenario.ThreeStrokes) { count = 3; remaining = 46; cost = 16; max = 22; }
            if (Case == Scenario.LastStroke) { count = 5; remaining = 12; min = max = cost = 12; }
            if (Case == Scenario.Exhausted) { count = 6; remaining = 0; min = max = cost = 0; }
            if (Case == Scenario.KnifeBlocked || Case == Scenario.LongCopy) { count = 3; remaining = 40; cost = 20; min = 16; }
            bool finish = count >= 3 && Case != Scenario.KnifeBlocked && Case != Scenario.LongCopy;
            string reason = Case == Scenario.NoBrush ? "先在笔记中形成这一层笔刷。" :
                Case == Scenario.KnifeBlocked ? "先在颜料板试过刮刀的两面。" : Case == Scenario.LongCopy ?
                "先在颜料板试过刮刀的两面，再返回画架继续。这里用于检查小窗口中的长中文换行。" :
                "再落" + Mathf.Max(0, 3 - count) + "笔后可完成。";
            return new AtelierPaintingSnapshot("独立预览", revision, Case == Scenario.KnifeBlocked ? 3 : 1,
                "atelier.flow.brush.1", Case == Scenario.NoBrush ? null : Brushes[0], null,
                remaining, count, Case == Scenario.KnifeBlocked || Case == Scenario.LongCopy ? 5 : 6,
                min, max, cost, 0, count > 0, finish, reason, Case != Scenario.Opening,
                submitting: Case == Scenario.Submitting);
        }
        /// <summary>仅预览场景使用矩形替身，真实适配器使用实际世界射线。</summary>
        public bool TryMap(Vector2 screen, out Vector2 uv)
        {
            uv = default;
            if (Result.Panel.activeSelf || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface, screen, null, out var local)) return false;
            var rect = Surface.rect;
            uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;
        }
        /// <summary>半透明图样仅是编辑器手势示意，不伪称正式面积标定。</summary>
        public void Preview(Vector2 uv, bool visible, int cost, int angle)
        {
            Ghost.gameObject.SetActive(visible);
            if (!visible) return;
            Ghost.texture = Brushes[0];
            Ghost.rectTransform.anchorMin = Ghost.rectTransform.anchorMax = uv;
            Ghost.rectTransform.anchoredPosition = Vector2.zero;
            Ghost.rectTransform.sizeDelta = new Vector2(160, 160 * Brushes[0].height / (float)Brushes[0].width) * Mathf.Sqrt(cost / 18f);
            Ghost.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }
        /// <summary>仅记录意图，不假扣颜料；真实操作由会话适配器验收。</summary>
        public string RequestCommitStroke(AtelierStrokeIntent intent, Vector2 uv)
        {
            Evidence.text = "收到一次落笔请求：成本 " + intent.Cost + "，角度 " + intent.Angle + "°。固定快照不扣量。";
            return Case == Scenario.Rejected ? "笔印需要更多地落在画布内。" : "预览已收到请求；真实扣量由会话执行。";
        }
        /// <summary>记录撤销意图，不伪造退款。</summary>
        public string RequestUndo(AtelierPaintingSnapshot snapshot) => Evidence.text = "收到撤销请求；用上一状态检查布局。";
        /// <summary>记录收层意图，不推进正式阶段。</summary>
        public string RequestFinishLayer(AtelierPaintingSnapshot snapshot) => Evidence.text = "收到完成绘画请求；固定快照不推进阶段。";
        /// <summary>独立预览没有世界机位。</summary>
        public void RequestReturn() { Evidence.text = "收到返回画室意图。"; }
        /// <summary>首批不提供假刀面功能。</summary>
        public string RequestToolMode(AtelierToolMode mode) => "当前预览不提供刀面动作。";
    }
}
#endif
