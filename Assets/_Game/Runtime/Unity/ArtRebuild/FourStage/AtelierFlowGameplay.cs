// 文件：AtelierFlowGameplay.cs
// 职责：装配四阶段会话，处理真实调查、洗笔、转镜、窗景、刮刀与有限落笔。
// 边界：只通过GameSession改变世界；不加载旧静物规则，不拥有UI同学的Workspace实现。
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Game.Application;
using Game.Domain;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>四阶段P00至P26闭环；正式UI直接绑定CanvasPort，不复制颜料状态。</summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class AtelierFlowGameplay : MonoBehaviour
    {
        public AtelierGameplay References;
        public AtelierDraftCamera Draft;
        public AtelierFlowWash Wash;
        public AtelierFlowCanvas CanvasPort;
        public AtelierFlowNarration Voice;
        public Collider CrayonPicking;
        public Transform CrayonFocus;
        public Transform PlasterFocus, MirrorFocus, DryBrushFocus, ShardsFocus;
        public AtelierFlowMirror Mirror;
        public AtelierFlowStageThree StageThree;
        public AtelierFlowStageFour StageFour;
        /// <summary>第三阶段世界适配和可替换控件共用输入许可。</summary>
        public bool CanUseStageThreeInput => GameplayInputEnabled && !paused && !transitioning && !References.Notebook.IsOpen;
        public bool IsPaletteFocused => focus == "palette";
        public bool IsWindowFocused => focus == "window";
        [Tooltip("洗笔杯与画笔的左上角世界说明；独立于旁白字幕")] public Text WaterObservation;
        /// <summary>阶段旁白和显色期间统一阻断世界及UI输入。</summary>
        public bool IsStageTransition { get; private set; }
        private int pendingStage = 1;
        private string pendingNarration = "N01";
        private bool waitingForReveal;
        /// <summary>场景唯一的新流程会话，UI读取快照并调用用例。</summary>
        public GameSession Session { get; private set; }
        /// <summary>当前机位是否接受画布输入，不代表任何领域事实。</summary>
        public bool IsCanvasFocused => focus == "canvas";
        /// <summary>暂停时关闭世界输入并冻结视觉计时。</summary>
        public bool IsPaused => paused;
        private string focus = "";
        private string lastObservation = "";
        /// <summary>开场旁白播放完前，世界、笔记和画布控件共同关闭输入。</summary>
        public bool IsOpening { get; private set; } = true;
        /// <summary>UI只读的玩法输入许可；UI打开状态仍由各模块自行消费。</summary>
        public bool GameplayInputEnabled => !IsOpening && !IsStageTransition && !IsEnding && Time.frameCount > blockedThroughFrame;
        /// <summary>正式Workspace接管时停用默认盖章和旧画布按钮，其他世界交互继续工作。</summary>
        public bool UsesExternalCanvasWorkspace { get; private set; }
        /// <summary>UI优先收起笔记内的导读或详情，返回true表示已消费本次Esc。</summary>
        public System.Func<bool> NotebookBackHandler { get; set; }
        private bool transitioning, paused, brushSelected, captured;
        private int blockedThroughFrame;
        private Vector3 home;
        private Quaternion homeTurn;
        private float homeFov;
        private Coroutine movement, observationPaging;
        private Vector2 pendingUV;
        private int pendingCost, pendingRevision;
        private string pendingEpoch;
        private void Start()
        {
            home = References.ViewCamera.transform.position;
            homeTurn = References.ViewCamera.transform.rotation;
            homeFov = References.ViewCamera.fieldOfView;
            Session = CreateFlowSession();
            References.Notebook.EscapeClosesNotebook = false;
            References.Notebook.Bind(Session);
            NotebookBackHandler=()=>References.Notebook.GetComponent<AtelierNotebookHelp>()?.CloseFromBack()??false;
            References.Notebook.PreviewCoverQuery=()=>References.Collection!=null&&References.Collection.IsOpen;
            // 藏品页与笔记本共用同一会话，只读世界碎片；入口便签由藏品页在 Bind 内注册，不开笔记本就无法点开。
            if (References.Collection != null)
            {
                References.Collection.Bind(Session, References.Notebook);
                // 四阶段笔刷原图由画布在场景中持有；藏品页按层读取，运行时直接注入，避免依赖编辑器内存赋值而丢图。
                References.Collection.ConfigureFourStageBrushArtwork(CanvasPort != null ? CanvasPort.Brushes : null);
            }
            References.Notebook.ExternalInputBlocked = true;
            if(StageFour!=null)StageFour.Bind(Session);
            Wash.Bind(Session);
            CanvasPort.Bind(Session);
            if (StageThree!=null) StageThree.Bind(Session);
            // 笔刷图卡位于可手调的笔记 Prefab 内；场景入口在会话创建后只注入读取端口。
            foreach (var display in References.Notebook.GetComponentsInChildren<Game.Unity.UI.FourStage.AtelierBrushAcquisitionDisplay>(true))
                display.Gameplay = this;
            Session.EventRaised += OnEvent;
            References.ReturnButton.onClick.AddListener(ReturnToRoom);
            References.ContextAction.onClick.AddListener(OnContextAction);
            References.ResumeButton.onClick.AddListener(() => Pause(false));
            References.RestartButton.onClick.AddListener(RestartGame);
            References.QuitButton.onClick.AddListener(() => UnityEngine.Application.Quit());
            References.PausePanel.SetActive(false);
            References.ContextPanel.SetActive(false);
            HideWaterObservation();
            References.ManuscriptPanel.SetActive(false);
            References.ContextAction.gameObject.SetActive(false);
            References.ReturnButton.gameObject.SetActive(false);
            References.Notebook.OpenChanged += OnNotebook;
            Voice.LineCompleted += OnNarrationCompleted;
            References.Reminder.gameObject.SetActive(false);
            if(restoringPractice)
            {
                IsOpening=IsStageTransition=false;
                pendingNarration="";
                References.Notebook.ExternalInputBlocked=false;
                Draft.RestoreStage(4,true);
            }
            else
            {
                Draft.PlayOpening();
                Voice.Play("N01");
            }
        }

        private void OnNarrationCompleted(string key)
        {
            if (key != pendingNarration) return;
            // 完整组结束后才开始显色；输入继续锁到渐显完成，不借此写任何领域事实。
            Draft.RestoreStage(pendingStage,false);
            waitingForReveal = true;
            pendingNarration = "";
        }
        private void Update()
        {
            if (Session == null) return;
            if (waitingForReveal && !Draft.RevealAnimating)
            {
                waitingForReveal = false;
                IsOpening = IsStageTransition = false;
                blockedThroughFrame = Time.frameCount + 1;
            }
            if(IsEnding)
            {
                if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&!StageFour.MenuVisible)Pause(!paused);
                CancelPointer();ClearHover();
                References.Notebook.ExternalInputBlocked=true;
                References.Reminder.gameObject.SetActive(false);
                References.ReturnButton.gameObject.SetActive(false);
                References.ContextPanel.SetActive(false);
                return;
            }
            if (!GameplayInputEnabled)
            {
                HideWaterObservation();
                References.Notebook.ExternalInputBlocked = true;
                References.Reminder.gameObject.SetActive(false);
                References.ReturnButton.gameObject.SetActive(false);
                References.ContextPanel.SetActive(false);
                CancelPointer();
                ClearHover();
                return;
            }
            if (WaterObservation != null)
            {
                // 笔记关闭还有两帧输入隔离，之后由当前近景恢复说明，避免被隔离帧永久藏掉。
                bool visible = (focus == "water"||Time.unscaledTime<worldObservationUntil) && !paused && !transitioning && !References.Notebook.IsOpen
                    && !string.IsNullOrEmpty(lastObservation);
                if (WaterObservation.gameObject.activeSelf != visible) WaterObservation.gameObject.SetActive(visible);
                if (visible) WaterObservation.rectTransform.anchoredPosition = new Vector2(36,Voice.ActiveKey == "" ? -28 : -150);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (paused) Pause(false);
                else if (References.Notebook.IsOpen)
                {
                    if (NotebookBackHandler == null || !NotebookBackHandler())
                        References.Notebook.SetOpen(false);
                }
                else if (transitioning) Pause(!paused);
                else if (focus != "") ReturnToRoom();
                else Pause(!paused);
            }
            References.Notebook.ExternalInputBlocked = paused || transitioning || captured;
            References.Reminder.gameObject.SetActive(!References.Notebook.IsOpen && !paused);
            References.ReturnButton.gameObject.SetActive(focus != "" && !paused && !References.Notebook.IsOpen && !transitioning
                && !(IsCanvasFocused && UsesExternalCanvasWorkspace));
            if (paused || transitioning || References.Notebook.IsOpen || !UnityEngine.Application.isFocused || Mouse.current == null)
            { CancelPointer(); ClearHover(); return; }
            var mouse = Mouse.current;
            if (mouse.rightButton.wasPressedThisFrame && focus != "") { ReturnToRoom();return; }
            if (focus == "note") return;
            if (focus == "mirror")
            {
                Mirror.HandleInput();
                return;
            }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (IsPaletteFocused && StageThree!=null && StageThree.Palette.HandlePointer(overUI))
            { ClearHover();return; }
            if(StageFour!=null&&StageFour.Tool.HandlePointer(overUI)) {ClearHover();return;}
            var ray = References.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if(StageFour!=null&&IsLampFocused)References.Lamp.SetPointer(ray,true);
            var hits = Physics.RaycastAll(ray,40,References.PickingMask,QueryTriggerInteraction.Collide).OrderBy(x => x.distance);
            bool has = false; RaycastHit hit = default;
            foreach (var candidate in hits) { if(StageFour!=null&&StageFour.SkipPicking(candidate))continue;hit = candidate; has = true; break; }
            var target = has ? hit.collider.GetComponent<AtelierInteractionTarget>() : null;
            bool crayon = has && hit.collider == CrayonPicking;
            bool canvas = target != null && target.Kind == AtelierTargetKind.Painting;
            if (captured)
            {
                if (!mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame) { CancelPointer(); return; }
                if (canvas && !overUI) { pendingUV = hit.textureCoord; CanvasPort.Preview(pendingUV,true); }
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    bool valid = canvas && !overUI && pendingEpoch == Session.SessionEpoch && pendingRevision == CanvasPort.Document.Revision;
                    captured = false;
                    CanvasPort.Preview(Vector2.zero,false);
                    if (valid) Result(CanvasPort.Commit(pendingUV,pendingCost,0));
                }
                return;
            }
            if (overUI) { ClearHover();CanvasPort.Preview(Vector2.zero,false);return; }
            bool allowed = crayon && Session.IsInspectable(AtelierFlowRules.Crayon) || target != null && Allowed(target);
            References.HoverText.text = allowed ? crayon ? "蜡笔画" : target.DisplayName : "";
            if (References.HoverGold != null) References.HoverGold.SetCurrent(allowed && target != null && !Wash.Moving ? target.HoverVisual : null);
            if (IsCanvasFocused && !UsesExternalCanvasWorkspace)
            {
                bool canPaint = canvas && Session.State.Knows(AtelierFlowRules.Brush(CanvasPort.Document.Current.Id)) && CanvasPort.DefaultCost > 0;
                CanvasPort.Preview(hit.textureCoord,canPaint);
                if (canPaint && mouse.leftButton.wasPressedThisFrame)
                {
                    // 捕获按下时的版本与成本，不能在松开时默默换成新画面上的请求。
                    captured = true;
                    pendingUV = hit.textureCoord;
                    pendingCost = CanvasPort.DefaultCost;
                    pendingRevision = CanvasPort.Document.Revision;
                    pendingEpoch = Session.SessionEpoch;
                }
            }
            if (!mouse.leftButton.wasPressedThisFrame || captured || !has) return;
            if (crayon) { ObserveCrayon();return; }
            if (target != null && allowed) Observe(target);
        }
        /// <summary>玩家亲点小画的独立入口；只认识此来源，不代授杯、窗或屋面。</summary>
        public void ObserveCrayon()
        {
            if (!GameplayInputEnabled || paused || transitioning || References.Notebook.IsOpen) return;
            if (!Session.IsInspectable(AtelierFlowRules.Crayon)) return;
            Session.Inspect(AtelierFlowRules.Crayon);
            Focus("crayon",CrayonFocus,46);
        }

        private bool Allowed(AtelierInteractionTarget target)
        {
            if (!GameplayInputEnabled) return false;
            if (target.Kind==AtelierTargetKind.Window || target.Kind==AtelierTargetKind.WindowNear || target.Kind==AtelierTargetKind.WindowFar
                || target.Kind==AtelierTargetKind.DryPalette || target.Kind==AtelierTargetKind.PaletteKnife)
                return StageThree!=null && Draft.VisibleStage>=3 && Session.IsInspectable(new ObjectId(target.ObjectId));
            if (target.Kind == AtelierTargetKind.Plaster || target.Kind == AtelierTargetKind.HandMirror ||
                target.Kind == AtelierTargetKind.DryBrush || target.Kind == AtelierTargetKind.MirrorShards)
                return Draft.VisibleStage >= 2 && Session.IsInspectable(new ObjectId(target.ObjectId));
            if(StageFour!=null && StageFour.Allowed(target))return true;
            if (target.Kind == AtelierTargetKind.Manuscript) return true;
            if (target.Kind == AtelierTargetKind.Brush || target.Kind == AtelierTargetKind.WashWater || target.Kind == AtelierTargetKind.Painting)
                return Session.IsInspectable(new ObjectId(target.ObjectId));
            return false;
        }
        /// <summary>显式调查入口同时供现场点击和自动命令取证，不凭便条赠送实体来源。</summary>
        public void Observe(AtelierInteractionTarget target)
        {
            if (!Allowed(target) || paused || transitioning || References.Notebook.IsOpen) return;
            lastObservation = "";
            if(StageFour!=null&&StageFour.Allowed(target)) {StageFour.Observe(target);return;}
            if (target.Kind==AtelierTargetKind.Window)
            {
                Session.Inspect(StudioRules.Window);
                Focus("window",StageThree.WindowFocus,58);
                Voice.Play("G_WINDOW_NEAR");
            }
            else if (target.Kind==AtelierTargetKind.WindowNear || target.Kind==AtelierTargetKind.WindowFar)
                StageThree.ObserveWindowDetail(target.Kind==AtelierTargetKind.WindowFar);
            else if (target.Kind==AtelierTargetKind.DryPalette)
            {
                Session.Inspect(AtelierFlowRules.DryPalette);
                if (!IsPaletteFocused) Focus("palette",StageThree.PaletteFocus,44);
            }
            else if (target.Kind==AtelierTargetKind.PaletteKnife)
            {
                Session.Inspect(AtelierFlowRules.PaletteKnife);
                StageThree.Palette.Equip();
                if(StageFour!=null&&Session.State.HasFact(AtelierFlowRules.Finished(3)))StageFour.Tool.SelectMode(AtelierFinaleToolMode.Edge);
                if (!IsPaletteFocused) Focus("palette",StageThree.PaletteFocus,44);
            }
            else if (target.Kind == AtelierTargetKind.Manuscript)
            {
                Session.Inspect(AtelierRules.Manuscript);
                focus = "note";
                References.ManuscriptPanel.SetActive(true);
                References.ContextPanel.SetActive(false);
            }
            else if (target.Kind == AtelierTargetKind.Brush)
            {
                Session.Inspect(AtelierRules.Brush);
                brushSelected = true;
                if (focus != "water") Focus("water",References.DeskFocusPose,43);
            }
            else if (target.Kind == AtelierTargetKind.WashWater)
            {
                Session.Inspect(StudioRules.Cup);
                if (focus != "water") Focus("water",References.DeskFocusPose,43);
                else if (brushSelected && !Wash.RequestContact()) Show("先亲点杯水和洗笔画笔。");
            }
            else if (target.Kind == AtelierTargetKind.Plaster || target.Kind == AtelierTargetKind.HandMirror ||
                target.Kind == AtelierTargetKind.DryBrush || target.Kind == AtelierTargetKind.MirrorShards)
            {
                Session.Inspect(new ObjectId(target.ObjectId));
                if (target.Kind == AtelierTargetKind.HandMirror)
                {
                    Mirror.Begin();
                    Focus("mirror",MirrorFocus,51);
                }
                else Focus("object",target.Kind == AtelierTargetKind.Plaster ? PlasterFocus :
                    target.Kind == AtelierTargetKind.DryBrush ? DryBrushFocus : ShardsFocus,46);
            }
            else if (target.Kind == AtelierTargetKind.Painting)
            {
                Session.Inspect(AtelierRules.Painting);
                if(Session.State.HasFact(AtelierFlowRules.WorldCommitted))
                {ShowWorldObservation(AtelierFlowNarration.TextFor("V_REFUSE"));return;}
                if(StageFour!=null&&!IsCanvasFocused)StageFour.Tool.SelectMode(AtelierFinaleToolMode.Paint);
                if (!IsCanvasFocused) Focus("canvas",References.FocusPose,47);
                RefreshCanvasControls();
            }
        }
        private void OnContextAction()
        {
            if (!GameplayInputEnabled || UsesExternalCanvasWorkspace || !IsCanvasFocused || paused || transitioning || References.Notebook.IsOpen) return;
            Result(CanvasPort.Finish());
        }
        private void Result(AtelierCanvasResult result)
        {
            if (!result.Accepted) Show(result.Reason);
            else RefreshCanvasControls();
        }
        private void RefreshCanvasControls()
        {
            if (!IsCanvasFocused || UsesExternalCanvasWorkspace) return;
            var layer = CanvasPort.Document.Current;
            bool brush = Session.State.Knows(AtelierFlowRules.Brush(layer.Id));
            Show(!brush ? "先在笔记中形成这一层笔刷。" : layer.Count>=3 && !CanvasPort.CanFinish ? CanvasPort.FinishBlockedReason
                : layer.RemainingPaint == 0 ? "颜料用完了，这一层可以收笔了。"
                : layer.Count < 3 ? "再落" + (3-layer.Count) + "笔后可完成。" : "还有颜料，可以继续。" );
            References.ContextAction.GetComponentInChildren<Text>().text = "完成绘画";
            References.ContextAction.gameObject.SetActive(brush);
            References.ContextAction.interactable = CanvasPort.CanFinish;
        }
        private void OnEvent(DomainEvent value)
        {
            // 世界输入和后续Workspace调用相同用例，按钮都从命令后的真实状态刷新。
            if (value is AtelierCanvasChanged) RefreshCanvasControls();
            if (value is ObjectInspected inspected && !string.IsNullOrEmpty(inspected.Description))
            {
                lastObservation = inspected.Description;
                Show(inspected.Description);
            }
            if (value is FactAdded fact)
            {
                if (fact.Fact == AtelierFlowRules.Finished(1))
                {
                    BeginStageTransition(2,"N03");
                }
                if (fact.Fact == AtelierFlowRules.Finished(2)) BeginStageTransition(3,"N04");
                if (fact.Fact == AtelierFlowRules.Finished(3))
                {
                    practice=Session.CaptureAtelierPractice();
                    BeginStageTransition(4,"N05");
                }
                if (fact.Fact == AtelierFlowRules.Washed) brushSelected = false;
            }
            if (value is CombinationSucceeded combination)
            {
                // 两个中间概念都由正常配方形成后才播放原稿N02，字幕不成为输入门槛。
                if (combination.RecipeId.StartsWith("atelier.flow.recipe.") &&
                    (combination.RecipeId.EndsWith("ice_blue")||combination.RecipeId.EndsWith("swirl")||combination.RecipeId.EndsWith("brush.1")) &&
                    Session.State.Knows(new ClueId("atelier.flow.ice_blue")) &&
                    Session.State.Knows(new ClueId("atelier.flow.swirl")))
                    Voice.Play("N02");
                if (combination.RecipeId.StartsWith("atelier.flow.recipe.brush."))
                    Show("关掉笔记本，再点画布。");
            }
        }
        /// <summary>先退出近景，再锁输入并等待整组旁白；不提前改变底稿清单。</summary>
        private void BeginStageTransition(int stage, string narration)
        {
            ReturnToRoom();
            References.Notebook.SetOpen(false);
            pendingStage = stage;
            pendingNarration = narration;
            waitingForReveal = false;
            IsStageTransition = true;
            References.Notebook.ExternalInputBlocked = true;
            Voice.Play(narration);
        }
        /// <summary>由实际转镜输入提交观察；摆放动画、开场或被UI捕获时不会调用动作。</summary>
        public void CompareMirror()
        {
            if (!GameplayInputEnabled || paused || transitioning || focus != "mirror" || References.Notebook.IsOpen) return;
            if (Mathf.Abs(Mirror.Angle) < 8) return;
            if (Session.State.HasFact(AtelierFlowRules.MirrorCompared)) return;
            Session.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");
            if (Session.State.Knows(AtelierFlowRules.Plaster)) Show("再规矩的形，翻过来就不压我了。");
        }
        private void OnNotebook(bool open)
        {
            HideWaterObservation();
            blockedThroughFrame = Time.frameCount + 1;
            CancelPointer();
            if (Mirror != null) Mirror.CancelDrag();
            if (StageThree!=null) StageThree.Palette.CancelGesture();
            StopObservationPaging();
            ClearHover();
            References.ContextPanel.SetActive(false);
            if (!open && IsCanvasFocused) RefreshCanvasControls();
            else if (!open && focus == "water" && !string.IsNullOrEmpty(lastObservation)) Show(lastObservation);
        }

        /// <summary>由场景接线或UI生命周期注册接管；只取消临时输入，不改变作品与颜料。</summary>
        /// <param name="active">true使用UI同学的正式控件，false恢复当前功能占位。</param>
        public void SetExternalCanvasWorkspaceActive(bool active)
        {
            UsesExternalCanvasWorkspace = active;
            CancelPointer();
            References.ContextPanel.SetActive(false);
            References.ContextAction.gameObject.SetActive(false);
            if (!active && IsCanvasFocused) RefreshCanvasControls();
        }
        /// <summary>返回不清除账本；首次读完便条在这里提交明确的阅读完成。</summary>
        public void ReturnToRoom()
        {
            if (!GameplayInputEnabled || transitioning) return;
            HideWaterObservation();
            CancelPointer();
            StopObservationPaging();
            Wash.CancelContact();
            if(StageFour!=null)References.Lamp.PutMatchDown();
            if (StageThree!=null) StageThree.Palette.CancelGesture();
            if (Mirror != null) Mirror.End();
            References.ManuscriptPanel.SetActive(false);
            if (focus == "note")
            {
                focus = "";
                Session.PerformAction(AtelierRules.Manuscript,"read_note");
                References.ContextPanel.SetActive(false);
                return;
            }
            if (focus == "") return;
            focus = "";
            StartMove(home,homeTurn,homeFov);
        }
        private void Focus(string kind, Transform pose, float fov)
        {
            if (transitioning) return;
            focus = kind;
            StartMove(pose.position,pose.rotation,fov);
        }
        private void StartMove(Vector3 position, Quaternion rotation, float fov)
        {
            HideWaterObservation();
            CancelPointer();
            StopObservationPaging();
            ClearHover();
            References.ContextPanel.SetActive(false);
            if (movement != null) StopCoroutine(movement);
            movement = StartCoroutine(Move(position,rotation,fov));
        }
        private IEnumerator Move(Vector3 destination, Quaternion rotation, float fov)
        {
            transitioning = true;
            var start = References.ViewCamera.transform.position;
            var turn = References.ViewCamera.transform.rotation;
            float fromFov = References.ViewCamera.fieldOfView;
            for(float t=0;t<1;t+=Time.deltaTime/1.2f)
            {
                float smooth=Mathf.SmoothStep(0,1,t);
                References.ViewCamera.transform.SetPositionAndRotation(Vector3.Lerp(start,destination,smooth)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.08f,Quaternion.Slerp(turn,rotation,smooth));
                References.ViewCamera.fieldOfView=Mathf.Lerp(fromFov,fov,smooth);
                yield return null;
            }
            References.ViewCamera.transform.SetPositionAndRotation(destination,rotation);
            References.ViewCamera.fieldOfView=fov;
            transitioning=false;
            movement=null;
            if(IsCanvasFocused) RefreshCanvasControls();
            // 回到主视角不重新弹出上一个物件说明，只有当前近景可恢复自己的文字。
            else if(focus != "" && !string.IsNullOrEmpty(lastObservation)) Show(lastObservation);
        }
        private float worldObservationUntil;
        private void Show(string text)
        {
            StopObservationPaging();
            HideWaterObservation();
            if (WaterObservation != null && (!IsCanvasFocused||UsesExternalCanvasWorkspace))
            {
                // 物件说明独占自己的节点；N02播放时下移，避免与旁白重叠。
                References.ContextPanel.SetActive(false);
                worldObservationUntil=Time.unscaledTime+Mathf.Max(5,text.Length*.10f);
                WaterObservation.text = text;
                WaterObservation.gameObject.SetActive(true);
                return;
            }
            References.ObservationText.text=text;
            References.ContextPanel.SetActive(true);
            References.ContextAction.gameObject.SetActive(false);
            // 新阶段长介绍按完整句翻页，沿用临时调查视图，不把整段挤成两行后截断。
            if (text.Length > 32) observationPaging = StartCoroutine(PageObservation(text));
        }
        /// <summary>第三阶段功能提示复用世界说明节点，不覆盖旁白字幕。</summary>
        public void ShowWorldObservation(string text)
        {
            lastObservation=text;
            Show(text);
        }
        /// <summary>玩家点定位按钮只进入板面，不替玩家调查板或获得工具。</summary>
        public void OpenPaletteForPractice()
        {
            if (!CanUseStageThreeInput || StageThree==null || !Session.State.HasFact(AtelierFlowRules.Finished(2))) return;
            Focus("palette",StageThree.PaletteFocus,44);
        }
        /// <summary>窗景比较的预设后退镜头，不开放自由拖拽或滚轮。</summary>
        public void CompareWindowView() => Focus("window",StageThree.WindowCompareFocus,51);
        private void HideWaterObservation()
        {
            if (WaterObservation != null) WaterObservation.gameObject.SetActive(false);
        }
        private IEnumerator PageObservation(string text)
        {
            var pages = new List<string>();
            // 保留原句，只在口语停顿处分行，避免旧Text把“哦，”拆到两行。
            text = text.Replace("……哦，","……\n哦，");
            string page = "";
            foreach (string sentence in Regex.Split(text,@"(?<=[。！？])"))
            {
                string next = sentence.Trim();
                if (next.Length == 0) continue;
                if (page.Length > 0 && page.Length + next.Length > 32)
                {
                    pages.Add(page);
                    page = "";
                }
                page += next;
            }
            if (page.Length > 0) pages.Add(page);
            foreach (string content in pages)
            {
                References.ObservationText.text = content;
                yield return new WaitForSeconds(2+content.Length*.07f);
            }
            observationPaging = null;
        }
        private void StopObservationPaging()
        {
            if (observationPaging == null) return;
            StopCoroutine(observationPaging);
            observationPaging = null;
        }
        private void CancelPointer()
        {
            captured=false;
            if(StageFour!=null)StageFour.Tool.Cancel();
            if (StageThree!=null) StageThree.Palette.CancelGesture();
            if(CanvasPort.Document!=null) CanvasPort.Preview(Vector2.zero,false);
        }
        private void ClearHover()
        {
            References.HoverText.text="";
            if(References.HoverGold!=null) References.HoverGold.ResetEffect();
        }
        private void Pause(bool value)
        {
            CancelPointer();
            Wash.CancelContact();
            if (Mirror != null) Mirror.CancelDrag();
            paused=value;
            if (value) HideWaterObservation();
            else if (focus == "water" && !string.IsNullOrEmpty(lastObservation)) Show(lastObservation);
            References.PausePanel.SetActive(value);
            Time.timeScale=value?0:1;
        }
        private void OnApplicationFocus(bool active) { if(!active) { CancelPointer();if(Wash!=null) Wash.CancelContact();if(Mirror!=null) Mirror.CancelDrag(); } }

        private void OnDestroy()
        {
            StopObservationPaging();
            Time.timeScale=1;
            if (Voice != null) Voice.LineCompleted -= OnNarrationCompleted;
            if(Session!=null) Session.EventRaised-=OnEvent;
            if(References!=null && References.Notebook!=null) References.Notebook.OpenChanged-=OnNotebook;
        }
    }
}
