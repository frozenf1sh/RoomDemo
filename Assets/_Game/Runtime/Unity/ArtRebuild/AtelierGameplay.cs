// =============================================================================
// 文件：AtelierGameplay.cs
// 职责：独立新画室的会话装配、指针观察、明确动作和画作近看输入。
// 边界：事实只经 GameSession 命令产生；镜头、提示和选中项均为临时 Unity 状态。
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Unity.ArtRebuild
{
    /// <summary>第一阶段可游玩的画室入口，不连接归档场景的启动器或调试流程。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class AtelierGameplay : MonoBehaviour
    {
        public Camera ViewCamera;
        public NotebookView Notebook;
        public NarrationSystem Narrator;
        public OneLineSubtitle Captions;
        public AtelierWashPresenter Wash;
        public AtelierPaintingPresenter Painting;
        public AtelierLampPresenter Lamp;
        public Transform FocusPose, DeskFocusPose, LampFocusPose;
        public GameObject ManuscriptPanel;
        public Text HoverText, ObservationText;
        public GameObject ContextPanel, PausePanel;
        public Button ContextAction, ReturnButton, ResumeButton, RestartButton, QuitButton, Reminder;
        public LayerMask PickingMask;
        private readonly HashSet<string> spoken = new HashSet<string>();
        private AtelierInteractionTarget selected;
        private Vector3 homePosition;
        private Quaternion homeRotation, lookRotation;
        private float homeFov, yaw, pitch;
        private bool focused, transitioning, paused;
        private AtelierTargetKind focusKind;
        private bool strokeActive;
        private Coroutine cameraMotion, delayedVoice;
        /// <summary>世界唯一状态入口；只为调试证据与表现消费者提供只读查询。</summary>
        public GameSession Session { get; private set; }
        /// <summary>画作聚焦是输入状态，不是世界事实。</summary>
        public bool IsPaintingFocused => focused && focusKind == AtelierTargetKind.Painting;

        private void Start()
        {
            homePosition = ViewCamera.transform.position;
            homeRotation = lookRotation = ViewCamera.transform.rotation;
            homeFov = ViewCamera.fieldOfView;
            Session = new GameSession(AtelierRules.Create());
            Notebook.EscapeClosesNotebook = false;
            Notebook.Bind(Session); Wash.Bind(Session); Painting.Bind(Session); Narrator.Bind(Session);
            if (Lamp != null) Lamp.Bind(Session);
            Session.EventRaised += OnEvent;
            Notebook.OpenChanged += OnNotebook;
            ContextAction.gameObject.SetActive(false);
            ReturnButton.onClick.AddListener(ReturnToRoom);
            ResumeButton.onClick.AddListener(() => SetPaused(false));
            RestartButton.onClick.AddListener(Restart);
            QuitButton.onClick.AddListener(() => UnityEngine.Application.Quit());
            ContextPanel.SetActive(false); PausePanel.SetActive(false); ReturnButton.gameObject.SetActive(false);
            if (ManuscriptPanel != null) ManuscriptPanel.SetActive(false);
            Say("start");
        }

        private void Update()
        {
            if (Session == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Notebook.IsOpen) Notebook.SetOpen(false);
                else if (focused && !transitioning) ReturnToRoom();
                else SetPaused(!paused);
            }
            Notebook.ExternalInputBlocked = paused || transitioning || (focused && focusKind == AtelierTargetKind.Manuscript);
            Reminder.gameObject.SetActive(Notebook.HasUnreadDiscoveries && !Notebook.IsOpen && !paused);
            ReturnButton.gameObject.SetActive(focused && !Notebook.IsOpen && !paused && !transitioning);
            if (paused || Notebook.IsOpen || transitioning || Mouse.current == null) { HoverText.text = ""; return; }
            if (focused && Mouse.current.rightButton.wasPressedThisFrame) { ReturnToRoom(); return; }
            if (!focused)
            {
                if (Mouse.current.rightButton.isPressed || (keyboard != null && keyboard.spaceKey.isPressed))
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    yaw = Mathf.Clamp(yaw + delta.x * .12f, -76, 76);
                    pitch = Mathf.Clamp(pitch - delta.y * .12f, -55, 68);
                    ViewCamera.transform.rotation = homeRotation * Quaternion.Euler(pitch, yaw, 0);
                    lookRotation = ViewCamera.transform.rotation;
                }
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f) ViewCamera.fieldOfView = Mathf.Clamp(ViewCamera.fieldOfView - Mathf.Clamp(wheel * .12f, -4, 4), 32, 64);
            }
            if (focused && focusKind == AtelierTargetKind.Manuscript) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { HoverText.text = ""; return; }
            var ray = ViewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Lamp != null) Lamp.SetPointer(ray, focused && focusKind == AtelierTargetKind.OilLamp);
            if (!Physics.Raycast(ray, out var hit, 20, PickingMask, QueryTriggerInteraction.Collide)) { HoverText.text = ""; return; }
            var target = hit.collider.GetComponent<AtelierInteractionTarget>();
            HoverText.text = target != null ? target.DisplayName : "";
            if (target != null) PositionHover(Mouse.current.position.ReadValue());
            if (target != null && target.Kind == AtelierTargetKind.Painting && IsPaintingFocused && Painting.SelectedMotif >= 0 && !Painting.IsSketched(Painting.SelectedMotif))
            {
                HoverText.text = "落笔 · 拖画一笔，或点画三笔";
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    strokeActive = true; Painting.BeginStroke(hit.textureCoord); ContextPanel.SetActive(false);
                }
                if (strokeActive && Mouse.current.leftButton.isPressed) Painting.ContinueStroke(hit.textureCoord);
            }
            else if (target != null && Mouse.current.leftButton.wasPressedThisFrame) Observe(target, hit.textureCoord);
        }

        private void LateUpdate()
        {
            // 按下和松开可能同帧送达；必须在本帧的落笔采样之后收笔。
            // 射线离开画布也会收尾，暂停或打开笔记则取消本次提交。
            if (!strokeActive || Mouse.current == null || !Mouse.current.leftButton.wasReleasedThisFrame) return;
            strokeActive = false;
            bool complete = Painting.EndStroke();
            if (complete && !paused && !transitioning && !Notebook.IsOpen) CommitSketch();
        }

        private void PositionHover(Vector2 screen)
        {
            // 物体名跟随指针，给底部旁白留下独立空间，并在屏幕边缘收回。
            var area = (RectTransform)HoverText.transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var point);
            var rect = HoverText.rectTransform;
            point += new Vector2(rect.sizeDelta.x * .5f + 20, -25);
            Vector2 edge = area.rect.size * .5f - rect.sizeDelta * .5f - Vector2.one * 8;
            rect.anchoredPosition = new Vector2(Mathf.Clamp(point.x, -edge.x, edge.x), Mathf.Clamp(point.y, -edge.y, edge.y));
        }

        /// <summary>从一次真实射线命中观察对象；不会自动奖励概念。</summary>
        public void Observe(AtelierInteractionTarget target, Vector2 uv)
        {
            selected = target;
            ContextPanel.SetActive(true); ContextAction.gameObject.SetActive(false);
            if (target.Kind == AtelierTargetKind.OilLamp || target.Kind == AtelierTargetKind.Wick)
            {
                Session.Inspect(StudioRules.LampBody);
                if (!focused || focusKind != AtelierTargetKind.OilLamp)
                {
                    Session.ObserveDetail(StudioRules.LampBody, Session.State.HasFact(StudioRules.LampTurned) ? "bright" : "dark");
                    focused = true; focusKind = AtelierTargetKind.OilLamp;
                    StartCamera(LampFocusPose.position, LampFocusPose.rotation, 43);
                    return;
                }
                if (target.Kind == AtelierTargetKind.Wick && Lamp.HasBurningMatch && !Session.State.HasFact(StudioRules.LampTurned))
                {
                    if (!Lamp.CanTouchWick)
                    { ObservationText.text = "把火柴凑近灯芯，再点一下。"; return; }
                    // 指针真正命中裸露灯芯并将火柴头送到接触点，才提交正常点灯命令。
                    Session.ObserveDetail(StudioRules.LampBody, "dark");
                    Lamp.HoldAtWick();
                    Session.PerformAction(StudioRules.LampBody, "light_wick");
                    ObservationText.text = "火从火柴头接到了灯芯上。";
                    return;
                }
                if (Lamp.IgnitionPlaying) { ObservationText.text = "火苗还没稳住，稍等一下。"; return; }
                bool lit = Session.State.HasFact(StudioRules.LampTurned);
                Session.ObserveDetail(StudioRules.LampBody, lit ? "bright" : "dark");
                ObservationText.text = lit ? "火苗稳在灯芯上，瓷座和桌面也暖了一点。\n按 N，比较点火前后的灯芯。" : "灯芯露在外面，玻璃罩搁在旁边。\n先取一根火柴，沿盒侧砂纸划一下。";
                return;
            }
            if (target.Kind == AtelierTargetKind.Matches || target.Kind == AtelierTargetKind.MatchStriker)
            {
                Session.Inspect(AtelierRules.Matches);
                if (!focused || focusKind != AtelierTargetKind.OilLamp)
                {
                    focused = true; focusKind = AtelierTargetKind.OilLamp;
                    StartCamera(LampFocusPose.position, LampFocusPose.rotation, 43);
                    return;
                }
                if (target.Kind == AtelierTargetKind.Matches)
                {
                    Lamp.TakeMatch();
                    ObservationText.text = "取了一根火柴。\n点盒子朝前的砂纸边，让火柴头划过去。";
                }
                else if (Lamp.HasMatch)
                {
                    if (Session.State.HasFact(AtelierRules.MatchStruck)) Lamp.StrikeAgain();
                    else Session.PerformAction(AtelierRules.Matches, "strike_match");
                    ObservationText.text = "沿砂纸擦亮，再把火送到灯芯边。";
                }
                else ObservationText.text = "先点纸盒里露出的火柴，取一根。";
                return;
            }
            if (target.Kind == AtelierTargetKind.Manuscript)
            {
                Session.Inspect(AtelierRules.Manuscript);
                Notebook.SetOpen(false); ContextPanel.SetActive(false);
                HoverText.text = "";
                focused = true; focusKind = AtelierTargetKind.Manuscript;
                ManuscriptPanel.SetActive(true); Say("manuscript"); return;
            }
            if (target.Kind == AtelierTargetKind.Brush)
            {
                bool alreadySeen = Session.State.Knows(new ClueId(AtelierRules.Brush.Value));
                Session.Inspect(AtelierRules.Brush);
                if (focused && focusKind == AtelierTargetKind.WashWater && alreadySeen && Session.State.HasFact(AtelierRules.StillLifePainted))
                {
                    if (!Session.State.Knows(new ClueId(StudioRules.Cup.Value)))
                    { ObservationText.text = "先看看杯里的水，再把笔蘸进去。"; return; }
                    if (Session.State.HasFact(StudioRules.Washed)) Wash.RinseAgain();
                    else Session.PerformAction(StudioRules.Cup, "wash");
                    ObservationText.text = "笔毛浸进水里，蓝色慢慢散开。";
                    return;
                }
                SelectMotif(1);
                ObservationText.text = "笔毛上还沾着蓝色。\n回到画布上，先把眼前的笔画下来。";
                if (!focused) FocusDesk();
                return;
            }
            if (target.Kind == AtelierTargetKind.WashWater)
            {
                Session.Inspect(StudioRules.Cup); SelectMotif(0);
                if (!Session.State.HasFact(StudioRules.Washed)) Session.ObserveDetail(StudioRules.Cup, "still");
                if (!focused) { FocusDesk(); return; }
                if (Wash.ContactPlaying) { ObservationText.text = "笔尖还在水里，等它抬起来再看。"; return; }
                bool washed = Session.State.HasFact(StudioRules.Washed);
                Session.ObserveDetail(StudioRules.Cup, washed ? "spread" : "still");
                ObservationText.text = washed ? "蓝色沿着水纹散开了。" : "水面很平，笔尖还没有碰到水。";
                if (Session.State.HasFact(AtelierRules.StillLifePainted))
                {
                    ObservationText.text += washed ? "\n按 N，把前后的水面记在一起。" : "\n点杯沿上的笔，把笔毛蘸进水里。";
                    Say(washed ? "spread_seen" : "water_seen");
                }
                else ObservationText.text += "\n先把它画下来：右键返回，再点画布落笔。";
                return;
            }
            if (target.Kind == AtelierTargetKind.StillLamp || target.Kind == AtelierTargetKind.Window)
            {
                Session.Inspect(target.Kind == AtelierTargetKind.StillLamp ? AtelierRules.StillLamp : StudioRules.Window);
                SelectMotif(target.Kind == AtelierTargetKind.StillLamp ? 2 : 3);
                ObservationText.text = target.Kind == AtelierTargetKind.StillLamp ? "灯光落在桌上。\n选好了，点画布把它画下来。" : "窗外有几盏亮着的灯。\n选好了，点画布把这片夜色带进去。";
                return;
            }
            Session.Inspect(AtelierRules.Painting);
            if (!IsPaintingFocused) { FocusPainting(); return; }
            Vector2 local = new Vector2((uv.x - Painting.WaterUV.x) / Painting.WaterRadius.x,
                (uv.y - Painting.WaterUV.y) / Painting.WaterRadius.y);
            if (local.sqrMagnitude < 2.5f && Session.State.HasFact(AtelierRules.CupSketched))
            {
                Session.Inspect(StudioRules.PictureWater);
                ObservationText.text = Session.State.HasFact(StudioRules.PictureFlow) ? "画里的水纹在动。" : "杯子画上去了，水面还是静的。\n按 N，试着添上刚才看到的水纹。";
            }
            else if (!Session.State.HasFact(AtelierRules.StillLifePainted))
                ObservationText.text = "先选一样眼前的东西。\n杯子、笔、灯和窗，都可以画。";
            else if (Vector2.Distance(uv, Painting.LampUV) < .11f && Session.State.HasFact(AtelierRules.LampSketched))
            {
                Session.Inspect(StudioRules.PictureLamp);
                ObservationText.text = Session.State.HasFact(StudioRules.PictureLit) ? "画里的灯芯亮了，暖色沿着瓷座铺开。" : "灯已经画上了，还差灯芯的一点亮色。";
            }
            else ObservationText.text = "杯子、笔、灯，还有窗外的夜色，都画上去了。";
        }

        private void SelectMotif(int index)
        {
            Painting.SelectMotif(index);
        }
        private void CommitSketch()
        {
            int index = Painting.SelectedMotif;
            if (!Session.State.Knows(new ClueId(AtelierRules.Brush.Value)))
            { ObservationText.text = "先近看杯沿上的画笔。"; ContextPanel.SetActive(true); Say("brush_needed"); return; }
            ObjectId source = index == 0 ? StudioRules.Cup : index == 1 ? AtelierRules.Brush : index == 2 ? AtelierRules.StillLamp : StudioRules.Window;
            var inputs = source == AtelierRules.Brush
                ? new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(AtelierRules.Brush.Value) }
                : new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(source.Value), new ClueId(AtelierRules.Brush.Value) };
            // 真实落笔手势确认以后才提交中性配方；短笔触动画只消费结果。
            Session.Combine(inputs);
        }
        private void FocusDesk()
        {
            if (transitioning) return;
            focused = true; focusKind = AtelierTargetKind.WashWater;
            StartCamera(DeskFocusPose.position, DeskFocusPose.rotation, 43);
        }

        /// <summary>沿已检查的桌面上方镜头路径接近实体画，不移动或缩放画架。</summary>
        public void FocusPainting()
        {
            if (focused || transitioning) return;
            focused = true; focusKind = AtelierTargetKind.Painting;
            StartCamera(FocusPose.position, FocusPose.rotation, 47);
        }
        /// <summary>返回之前的观察方向与机位，画作解释不自动关书或返回。</summary>
        public void ReturnToRoom()
        {
            if (!focused || transitioning) return;
            if (ManuscriptPanel != null) ManuscriptPanel.SetActive(false);
            if (focusKind == AtelierTargetKind.Manuscript)
            {
                // 纸面浮层不曾移动相机，合上它也不播放无缘由的镜头运动。
                focused = false; ContextPanel.SetActive(false); return;
            }
            focused = false; strokeActive = false;
            if (Lamp != null) Lamp.PutMatchDown();
            StartCamera(homePosition, lookRotation, homeFov);
        }
        private void StartCamera(Vector3 destination, Quaternion rotation, float fov)
        {
            if (cameraMotion != null) StopCoroutine(cameraMotion);
            ContextPanel.SetActive(false);
            cameraMotion = StartCoroutine(MoveCamera(destination, rotation, fov));
        }
        private IEnumerator MoveCamera(Vector3 destination, Quaternion rotation, float fov)
        {
            transitioning = true;
            Vector3 start = ViewCamera.transform.position;
            Quaternion turn = ViewCamera.transform.rotation;
            float oldFov = ViewCamera.fieldOfView;
            for (float time = 0; time < 1.65f; time += Time.deltaTime)
            {
                float t = Mathf.Clamp01(time / 1.65f); t = t * t * t * (t * (6 * t - 15) + 10);
                // 路径保持在桌面和物件上方，轻微弧线只属于镜头。
                ViewCamera.transform.position = Vector3.Lerp(start, destination, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .08f;
                ViewCamera.transform.rotation = Quaternion.Slerp(turn, rotation, t);
                ViewCamera.fieldOfView = Mathf.Lerp(oldFov, fov, t);
                yield return null;
            }
            ViewCamera.transform.SetPositionAndRotation(destination, rotation); ViewCamera.fieldOfView = fov;
            transitioning = false; cameraMotion = null;
        }

        private void OnNotebook(bool open)
        {
            ContextPanel.SetActive(false);
            if (open && Session.State.Knows(AtelierRules.Observation("spread")) && !Session.State.Knows(StudioRules.Flow)) Say("book_open");
        }
        private void OnEvent(DomainEvent value)
        {
            if (value is FactAdded fact && fact.Fact == AtelierRules.StillLifePainted) StartCoroutine(AfterStillLife());
            if (!(value is CombinationSucceeded result)) return;
            if (result.RecipeId.StartsWith("atelier.sketch.") && !Session.State.HasFact(AtelierRules.StillLifePainted)) Say("sketch_first");
            if (result.RecipeId == "atelier.idea.flow") Say("flow_learned");
            if (result.RecipeId == "atelier.meaning.picture_water") { Say("water_painted"); StartCoroutine(AfterWaterPainting()); }
            if (result.RecipeId == "atelier.idea.light") Say("light_learned");
            if (result.RecipeId == "atelier.meaning.picture_lamp") Say("lamp_painted");
            if (result.RecipeId == "atelier.meaning.water_light") StartCoroutine(AfterWrongLight());
            if (result.RecipeId == "atelier.meaning.water_flow")
            {
                if (delayedVoice != null) StopCoroutine(delayedVoice);
                delayedVoice = StartCoroutine(AfterVisibleResult());
            }
        }
        private IEnumerator AfterStillLife()
        {
            // 世界事实已经成立；这里只等最后一层短笔触收齐，才评论“东西都在了”。
            yield return new WaitForSeconds(4.6f);
            if (!Session.State.HasFact(AtelierRules.RealityTouched)) Say("still_life");
        }
        private IEnumerator AfterWaterPainting()
        {
            // 等局部水色和前句说完，只发旁白请求，不生成新观察或物体。
            yield return new WaitForSeconds(6);
            if (!Session.State.HasFact(StudioRules.LampTurned)) Say("lamp_next");
        }
        private IEnumerator AfterWrongLight()
        {
            yield return new WaitForSeconds(1.4f);
            Say("outside_light");
        }
        private IEnumerator AfterVisibleResult()
        {
            yield return new WaitForSeconds(1.4f);
            Say("outside_first"); delayedVoice = null;
        }
        private void Say(string key)
        {
            if (!spoken.Add(key)) return;
            Narrator.Enqueue(AtelierNarration.Line(key, Session.State));
        }
        /// <summary>暂停只冻结表现与输入，不改变领域日序或配方结果。</summary>
        public void SetPaused(bool value)
        {
            paused = value; Time.timeScale = value ? 0 : 1; PausePanel.SetActive(value);
        }
        private void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        private void OnDestroy()
        {
            Time.timeScale = 1;
            if (Session != null) Session.EventRaised -= OnEvent;
            if (Notebook != null) Notebook.OpenChanged -= OnNotebook;
        }
    }
}
