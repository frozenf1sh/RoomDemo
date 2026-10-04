// =============================================================================
// 文件：InspectionController.cs
// 职责：在独立展示空间创建近景复制体，处理拖转、缩放、热点与返回房间。
// 边界：同一相机暂时切到独立 Layer；不使用物理抓取，不修改房间物体姿态。
//       临时页状态来自应用层模型；观察只能通过会话发现入口进入领域。
// =============================================================================
using System.Linq;
using Game.Application;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>两件主物体共用的轻量检视适配器，拥有复制体和输入切换的生命周期。</summary>
    public sealed class InspectionController : MonoBehaviour
    {
        /// <summary>近景复制体专用层，同一相机临时只渲染这一层。</summary>
        public const int InspectionLayer = 30;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform pivot;
        [SerializeField] private GameObject overlay;
        [SerializeField] private Text title, hint, feedback;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject cubePrefab;
        [SerializeField] private AudioSource pickupAudio, pageAudio, observeAudio, exitAudio;
        private GameSession session;
        private NotebookView notebook;
        private GameObject clone;
        private InspectionView view;
        private InspectionHotspot[] hotspots;
        private InspectionHotspot hovered;
        private InspectableObject source;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private int cameraMask;
        private CameraClearFlags cameraClear;
        private Color cameraBackground;
        private float cameraFar, yaw, pitch, zoom = 3, pressTravel;
        private Vector2 pressAt;
        private string sourceId;
        /// <summary>复制体存活即处于检视模式；退出先清空它，防止延迟销毁期间继续响应。</summary>
        public bool IsOpen => clone != null;
        /// <summary>当前书页或检视动作的临时状态；不写入世界事实。</summary>
        public InspectionModel Model { get; private set; }
        /// <summary>控制器拥有的当前复制体，仅供测试和明确开发工具观察。</summary>
        public GameObject CurrentVisual => clone;
        /// <summary>相机距展示中心的距离，用于验证滚轮限制。</summary>
        public float ZoomDistance => zoom;
        /// <summary>通过 Editor 显式绑定相机、展示中心、UI 与音源，不在每帧寻找依赖。</summary>
        /// <remarks>只设置引用；场景验证器负责拒绝缺失绑定，Bind 才连接游戏会话。</remarks>
        public void Configure(Camera camera, Transform displayPivot, GameObject panel, Text name, Text hover, Text response,
            Button back, GameObject sample, AudioSource pickup, AudioSource page, AudioSource observation, AudioSource exit)
        {
            viewCamera = camera; pivot = displayPivot; overlay = panel; title = name; hint = hover; feedback = response;
            exitButton = back; cubePrefab = sample; pickupAudio = pickup; pageAudio = page; observeAudio = observation; exitAudio = exit;
        }
        /// <summary>重开或预设跳转先退出旧检视，再绑定新会话；不会留下输入锁。</summary>
        public void Bind(GameSession game, NotebookView book)
        {
            Exit(false); session = game; notebook = book;
            exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(() => Exit()); overlay.SetActive(false);
        }
        /// <summary>先由领域确认可调查性并认识本物体，再展示独立近景模型。</summary>
        /// <param name="item">真实房间实体的检视适配器，包含独立展示 Prefab。</param>
        /// <returns>调查许可成立且已进入检视时返回 true，输入锁或世界条件不足时返回 false。</returns>
        /// <remarks>正常入口仅认识当前物体；观察细节必须随后单独点击热点。</remarks>
        public bool Enter(InspectableObject item)
        {
            if (session == null || notebook.ExternalInputBlocked || !session.IsInspectable(item.Entity.Id)) return false;
            session.Inspect(item.Entity.Id);
            source = item;
            Open(item.InspectVisual, item.Entity.Id.Value, session.Rules.Discoveries.Single(x => x.Source == item.Entity.Id).DisplayName);
            return true;
        }
        /// <summary>开发入口：技术样板不授予物体或观察，也不改变领域状态。</summary>
        public void InspectDebugCube() { if (IsOpen) Exit(false); source = null; Open(cubePrefab, "test_cube", "检视技术样板"); }
        private void Open(GameObject prefab, string id, string name)
        {
            notebook.SetOpen(false); notebook.ExternalInputBlocked = true;
            sourceId = id; Model = new InspectionModel(InspectionContent.For(id));
            cameraPosition = viewCamera.transform.localPosition; cameraRotation = viewCamera.transform.localRotation;
            cameraMask = viewCamera.cullingMask; cameraClear = viewCamera.clearFlags; cameraBackground = viewCamera.backgroundColor; cameraFar = viewCamera.farClipPlane;
            clone = Instantiate(prefab, pivot, false);
            foreach (var child in clone.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = InspectionLayer;
            view = clone.GetComponent<InspectionView>(); hotspots = clone.GetComponentsInChildren<InspectionHotspot>(true);
            yaw = pitch = 0; zoom = 3; hovered = null;
            viewCamera.cullingMask = 1 << InspectionLayer; viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(.055f, .06f, .065f); viewCamera.farClipPlane = 15;
            title.text = name + " · 仔细看"; hint.text = "拖动旋转，滚轮拉近。细节藏在不同角度。";
            feedback.text = ""; overlay.SetActive(true); view.ApplyState(Model.State, false); PositionCamera(); pickupAudio.Play();
            RefreshHotspots();
        }
        /// <summary>使用局部相机快照恢复，原来固定观察点和转头角度保持不变。</summary>
        public void Exit(bool playSound = true)
        {
            if (!IsOpen) return;
            // 立即清空状态并移除旧复制体，延迟销毁期间也不能再响应射线。
            clone.SetActive(false); Destroy(clone); clone = null; Model = null; source = null; hovered = null;
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = cameraPosition; viewCamera.transform.localRotation = cameraRotation;
                viewCamera.cullingMask = cameraMask; viewCamera.clearFlags = cameraClear; viewCamera.backgroundColor = cameraBackground; viewCamera.farClipPlane = cameraFar;
            }
            // 场景卸载期间相机、UI 或音源可能先销毁；归还仍存活的资源即可。
            if (overlay != null) overlay.SetActive(false);
            if (notebook != null) notebook.ExternalInputBlocked = false;
            if (pickupAudio != null) pickupAudio.Stop(); if (pageAudio != null) pageAudio.Stop(); if (observeAudio != null) observeAudio.Stop();
            if (exitAudio != null) { if (playSound) exitAudio.Play(); else exitAudio.Stop(); }
        }
        /// <summary>只旋转复制体，限制俯仰以便近距离阅读；不读取旋转推导世界状态。</summary>
        public void Rotate(Vector2 delta)
        {
            if (!IsOpen) return;
            yaw += delta.x * .35f; pitch = Mathf.Clamp(pitch - delta.y * .25f, -60, 60);
            clone.transform.localRotation = Quaternion.Euler(pitch, yaw, 0); RefreshHotspots();
        }
        /// <summary>把滚轮增量映射到有限展示距离，不缩放真实房间物体。</summary>
        /// <param name="delta">新版输入系统的垂直滚轮增量，正数拉近。</param>
        public void Zoom(float delta)
        {
            if (!IsOpen) return;
            zoom = Mathf.Clamp(zoom - delta * .002f, 1.6f, 5.2f); PositionCamera();
        }
        private void PositionCamera()
        {
            viewCamera.transform.position = pivot.position + Vector3.back * zoom;
            viewCamera.transform.rotation = Quaternion.identity;
        }
        /// <summary>热点必须满足页状态；拿近和翻页永远不会自动发现短语。</summary>
        /// <param name="id">已命名热点动作，普通输入还需先通过 ObserveAt 射线与角度检查。</param>
        /// <returns>临时动作可执行时返回 true；领域发现是否成功由反馈事件另行说明。</returns>
        public bool ActOnHotspot(string id)
        {
            if (!IsOpen || !Model.TryAct(id, out var action)) return false;
            view.ApplyState(Model.State, action.NextState != null); pageAudio.Play();
            if (action.ObservationId != null)
            {
                // 输入只提交来源与细节，绝不在适配器中写 KnownClues。
                var events = session.ObserveDetail(source.Entity.Id, action.ObservationId);
                var failed = events.OfType<Game.Domain.InspectionFailed>().FirstOrDefault();
                feedback.text = failed != null ? failed.Reason : events.OfType<Game.Domain.ObjectInspected>().Last().Description;
                if (failed == null)
                {
                    observeAudio.Play();
                    // 轻勾短语而非弹“奖励 +1”；标记只存在于这次近景复制体。
                    foreach (var hotspot in hotspots.Where(x => x.ActionId == id))
                    { var mark = hotspot.transform.Find("ObservedUnderline"); if (mark != null) mark.gameObject.SetActive(true); }
                }
            }
            else feedback.text = action.Hint;
            RefreshHotspots(); return true;
        }
        /// <summary>真实射线入口；按当前角度和页状态寻找具体细节。</summary>
        /// <param name="point">当前相机画面中的屏幕像素位置。</param>
        /// <returns>射中当前可见、可执行的近景热点时返回 true。</returns>
        public bool ObserveAt(Vector2 point)
        {
            if (!IsOpen || !Physics.Raycast(viewCamera.ScreenPointToRay(point), out var hit, 12, 1 << InspectionLayer)) return false;
            var hotspot = hit.collider.GetComponent<InspectionHotspot>();
            return hotspot != null && hotspot.Faces(viewCamera) && ActOnHotspot(hotspot.ActionId);
        }
        private void RefreshHotspots()
        {
            foreach (var hotspot in hotspots)
                hotspot.GetComponent<Collider>().enabled = hotspot.gameObject.activeInHierarchy && Model.CanAct(hotspot.ActionId) && hotspot.Faces(viewCamera);
        }
        private void Update()
        {
            if (!IsOpen || Mouse.current == null) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (mouse.rightButton.wasPressedThisFrame || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)) { Exit(); return; }
            float scroll = mouse.scroll.ReadValue().y; if (scroll != 0) Zoom(scroll);
            if (mouse.leftButton.wasPressedThisFrame) { pressAt = mouse.position.ReadValue(); pressTravel = 0; }
            if (mouse.leftButton.isPressed)
            {
                pressTravel = Mathf.Max(pressTravel, Vector2.Distance(pressAt, mouse.position.ReadValue()));
                if (pressTravel > 6) Rotate(mouse.delta.ReadValue());
            }
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUi && mouse.leftButton.wasReleasedThisFrame && pressTravel <= 6) ObserveAt(mouse.position.ReadValue());
            InspectionHotspot next = null;
            if (!overUi && Physics.Raycast(viewCamera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 12, 1 << InspectionLayer)) next = hit.collider.GetComponent<InspectionHotspot>();
            if (next != hovered)
            {
                if (hovered != null) hovered.SetHover(false); hovered = next;
                if (hovered != null) { hovered.SetHover(true); hint.text = Model.Definition.Actions.Single(x => x.Id == hovered.ActionId).Hint + " · 点击"; }
                else hint.text = "左键拖动旋转 · 滚轮缩放 · 点击细节 · 右键／Esc 返回";
            }
        }
        private void OnDestroy() { if (IsOpen) Exit(false); }
    }
}
