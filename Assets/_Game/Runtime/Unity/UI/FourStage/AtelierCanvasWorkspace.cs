// 文件：AtelierCanvasWorkspace.cs
// 职责：控件展示、一次手势捕获和意图提交。边界：不计算预算、不合成正式作品。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Game.Unity.UI.FourStage
{
    /// <summary>在全局输入后更新预览，避免占位输入覆盖当前预览。</summary>
    [DefaultExecutionOrder(200)]
    public sealed class AtelierCanvasWorkspace : MonoBehaviour
    {
        public AtelierCanvasWorkspaceReferences References;
        private IAtelierPaintingPort port;
        private AtelierPaintingSnapshot current;
        private AtelierStrokeIntent gesture;
        private int cost, angle, revision = -1;
        private string epoch, feedback = "";
        /// <summary>取消旧捕获后替换端口；不销毁外部纹理。</summary>
        public void Bind(IAtelierPaintingPort source)
        {
            CancelGesture();
            port = source;
            epoch = null;
            revision = -1;
        }
        private void OnEnable()
        {
            References.Size.onValueChanged.AddListener(OnSize);
            References.Angle.onValueChanged.AddListener(OnAngle);
            References.Undo.onClick.AddListener(Undo);
            References.Finish.onClick.AddListener(Finish);
            References.Return.onClick.AddListener(Return);
            References.Knife.onClick.AddListener(Knife);
        }
        private void OnDisable()
        {
            CancelGesture();
            References.Size.onValueChanged.RemoveListener(OnSize);
            References.Angle.onValueChanged.RemoveListener(OnAngle);
            References.Undo.onClick.RemoveListener(Undo);
            References.Finish.onClick.RemoveListener(Finish);
            References.Return.onClick.RemoveListener(Return);
            References.Knife.onClick.RemoveListener(Knife);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
        private void OnSize(float value) { cost = Mathf.RoundToInt(value); feedback = ""; }
        private void OnAngle(float value) { angle = Mathf.RoundToInt(value) % 360; }
        private void LateUpdate()
        {
            current = port?.Read();
            bool visible = current != null && current.IsVisible;
            References.Group.alpha = visible ? 1 : 0;
            References.Group.interactable = visible;
            References.Group.blocksRaycasts = visible;
            if (!visible || !UnityEngine.Application.isFocused) { CancelGesture(); return; }
            if (epoch != current.SessionEpoch || revision != current.CanvasRevision)
            {
                // 版本改变终止旧按下，不把旧手势替换成新画面上的命令。
                CancelGesture();
                bool fresh = epoch != current.SessionEpoch;
                epoch = current.SessionEpoch;
                revision = current.CanvasRevision;
                cost = fresh ? current.SelectedCost : Mathf.Clamp(cost, current.MinCost, current.MaxCost);
                if (fresh) angle = current.SelectedAngle;
                feedback = "";
            }
            Refresh();
            var mouse = Mouse.current;
            if (mouse == null || current.IsSubmitting) { CancelGesture(); return; }
            var keys = Keyboard.current;
            if ((keys != null && (keys.escapeKey.wasPressedThisFrame || keys.nKey.wasPressedThisFrame ||
                keys.tabKey.wasPressedThisFrame)) || mouse.rightButton.wasPressedThisFrame)
            { CancelGesture(); return; }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 uv = default;
            bool mapped = !overUI && port.TryMap(mouse.position.ReadValue(), out uv);
            bool canPaint = current.PreviewTexture != null && current.RemainingPaint > 0;
            if (gesture != null)
            {
                // 离开表面或进入控件即取消，不在返回画布时恢复旧拖动。
                if (!mapped || !canPaint) { CancelGesture(); return; }
                port.Preview(uv, true, gesture.Cost, gesture.Angle);
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    var request = gesture;
                    gesture = null;
                    feedback = port.RequestCommitStroke(request, uv);
                    port.Preview(uv, false, 0, 0);
                }
                else if (!mouse.leftButton.isPressed) CancelGesture();
            }
            else
            {
                port.Preview(uv, mapped && canPaint, cost, angle);
                if (mapped && canPaint && mouse.leftButton.wasPressedThisFrame)
                    gesture = new AtelierStrokeIntent(current, cost, angle);
            }
        }
        private void Refresh()
        {
            References.Artwork.texture = current.CanvasTexture;
            References.Artwork.enabled = References.ShowArtworkInUI && current.CanvasTexture != null;
            References.Brush.texture = current.PreviewTexture;
            References.Brush.enabled = current.PreviewTexture != null;
            if (current.PreviewTexture != null)
                References.BrushAspect.aspectRatio = current.PreviewTexture.width / (float)current.PreviewTexture.height;
            References.Brush.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            References.Size.minValue = current.MinCost;
            References.Size.maxValue = current.MaxCost;
            References.Size.SetValueWithoutNotify(cost);
            References.Angle.SetValueWithoutNotify(angle);
            bool editable = gesture == null && !current.IsSubmitting;
            References.Size.interactable = editable && current.MinCost < current.MaxCost;
            References.Angle.interactable = editable && current.RemainingPaint > 0;
            References.Undo.interactable = editable && current.CanUndo;
            References.Finish.interactable = editable && current.CanFinish;
            References.Return.interactable = editable && current.CanReturn;
            References.Knife.gameObject.SetActive(current.CanUseKnife);
            References.Paint.SetValueWithoutNotify(current.RemainingPaint);
            References.SizeLabel.text = "笔印大小 · " + cost;
            References.AngleLabel.text = "旋转 · " + angle + "°";
            References.CountLabel.text = "已落 " + current.CommittedStrokeCount + " 笔 / 计划 " + current.PlannedStrokeCount + " 笔";
            References.Status.text = !string.IsNullOrEmpty(feedback) ? feedback : current.IsSubmitting ? "正在提交…" :
                !current.CanFinish ? current.FinishBlockedReason : current.RemainingPaint == 0 ?
                "颜料用完了，这一层可以收笔了" : "还有颜料，可以继续\n完成绘画后，本层剩余颜料不再使用";
        }
        private void Undo() { CancelGesture(); if (current != null && current.CanUndo) feedback = port.RequestUndo(current); }
        private void Finish() { CancelGesture(); if (current != null && current.CanFinish) feedback = port.RequestFinishLayer(current); }
        private void Return() { CancelGesture(); if (current != null && current.CanReturn) port.RequestReturn(); }
        private void Knife() { CancelGesture(); feedback = port.RequestToolMode(AtelierToolMode.Knife); }
        /// <summary>终止本地捕获并取消预览，不撤销已提交笔触。</summary>
        public void CancelGesture()
        {
            gesture = null;
            port?.Preview(Vector2.zero, false, 0, 0);
        }
    }
}
