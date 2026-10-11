// =============================================================================
// 文件：AtelierContinuationInput.cs
// 职责：接收擦布手势、屋顶与天空调查、整体调查和主动定稿输入。
// 边界：这是输入适配器；命令仍交给 GameSession，动画和延时不能代替玩家调查。
// =============================================================================
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Unity.ArtRebuild
{
    /// <summary>有限后半段输入，不增加新解析器或独立状态机框架。</summary>
    public sealed class AtelierContinuationInput : MonoBehaviour
    {
        public AtelierFinalePresenter Finale;
        public AtelierTrialPresenter Trial;
        public Transform TrialFocusPose;
        private AtelierGameplay game;
        private GameSession session;
        private bool nearTrial, clothSelected, wiping;
        private float startX, maxX;
        private BoxCollider clothPicking;
        /// <summary>绑定明确的控制器，不由 Presenter 反向获取控制器。</summary>
        public void Bind(AtelierGameplay controller)
        {
            game = controller;
            session = controller.Session;
            game.ContextAction.onClick.AddListener(Deliver);
            clothPicking = GetComponentsInChildren<AtelierInteractionTarget>()
                               .First(t => t.Kind == AtelierTargetKind.TrialCloth)
                               .GetComponent<BoxCollider>();
        }
        private void Update()
        {
            // 只更新本工作簇的指针拾取范围，跟随真实变形后的布面，不改变领域规则。
            if (wiping && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                wiping = false;
                Trial.Release();
            }
            if (clothPicking == null)
                return;
            var bounds = Trial.Cloth.GetComponent<Renderer>().bounds;
            clothPicking.center = clothPicking.transform.InverseTransformPoint(bounds.center);
            clothPicking.size = bounds.size + new Vector3(.012f, .018f, .012f);
        }
        /// <summary>擦除后不再命中已经不存在的屋顶；标记和整体调查只在实际显现后开放。</summary>
        public bool Pick(Ray ray, out RaycastHit selected)
        {
            foreach (var hit in Physics.RaycastAll(ray, 40, game.PickingMask, QueryTriggerInteraction.Collide)
                         .OrderBy(h => h.distance))
            {
                var target = hit.collider.GetComponent<AtelierInteractionTarget>();
                bool roof = hit.collider.name.Contains("屋顶");
                if (roof && Finale.RoofClear(hit.point))
                    continue;
                if (target != null)
                {
                    if (target.Kind == AtelierTargetKind.Sky && !Finale.RoofSettled)
                        continue;
                    if (target.Kind == AtelierTargetKind.SharedMark &&
                        (!session.State.HasFact(StudioRules.FullSky) ||
                         Shader.GetGlobalFloat("_AtelierMark") < .99f))
                        continue;
                    if (target.Kind == AtelierTargetKind.Room &&
                        !session.State.HasFact(AtelierRules.WholeObserved))
                        continue;
                    if (target.Kind == AtelierTargetKind.Window &&
                        session.State.HasFact(AtelierRules.FrameErased) &&
                        Shader.GetGlobalFloat("_AtelierFrame") > .99f)
                        continue;
                }
                selected = hit;
                return true;
            }
            selected = default;
            return false;
        }
        /// <summary>明确拖布接触；只有完整横向擦拭才提交一次动作，不自动取得擦后观察。</summary>
        public bool Pointer(AtelierInteractionTarget target, RaycastHit hit, bool down, bool held, bool up)
        {
            if (up && wiping)
            {
                wiping = false;
                Trial.Release();
            }
            if (!nearTrial || !clothSelected || target == null ||
                target.Kind != AtelierTargetKind.Trial && target.Kind != AtelierTargetKind.TrialCloth)
                return false;
            if (session.State.HasFact(AtelierRules.TrialWiped))
                return false;
            if (down)
            {
                if (hit.point.x > Trial.BoardCentre.x - .10f)
                {
                    game.ShowObservation("从湿色左边按住，贴着木板向右擦。", true);
                    return true;
                }
                wiping = true;
                startX = hit.point.x;
                maxX = startX;
            }
            if (wiping && held)
            {
                maxX = Mathf.Max(maxX, hit.point.x);
                float amount = Mathf.Clamp01((maxX - startX) / .38f);
                Trial.Contact(hit.point, amount);
                if (amount > .97f)
                {
                    // 充分的接触手势是正常动作来源，概念仍须比较两次亲点观察。
                    session.PerformAction(AtelierRules.Trial, "wipe_trial");
                    wiping = false;
                    Trial.Release();
                    clothSelected = false;
                    game.ShowObservation("湿颜料被布带走了。等布落下，再点木纹看看。", true);
                }
            }
            return down || held || wiping;
        }
        /// <summary>先处理新增来源，未处理的目标继续交给既有开场输入。</summary>
        public bool Observe(AtelierInteractionTarget target)
        {
            switch (target.Kind)
            {
            case AtelierTargetKind.Trial:
                session.Inspect(AtelierRules.Trial);
                if (!nearTrial)
                {
                    nearTrial = true;
                    game.FocusAt(TrialFocusPose, 47);
                    if (!session.State.HasFact(AtelierRules.TrialWiped))
                        session.ObserveDetail(AtelierRules.Trial, "wet");
                    game.ShowObservation("湿颜料盖住了木纹。旁边有块擦笔布。", true);
                    return true;
                }
                if (session.State.HasFact(AtelierRules.TrialWiped))
                {
                    if (!Trial.Settled)
                    {
                        game.ShowObservation("布还贴着木板，稍等一下。", true);
                        return true;
                    }
                    session.ObserveDetail(AtelierRules.Trial, "base");
                    game.ShowObservation("木纹露出来了，边沿还留着一点蓝灰色。\n按 N，比较擦前与擦后的颜色。",
                                         true);
                }
                else
                {
                    session.ObserveDetail(AtelierRules.Trial, "wet");
                    game.ShowObservation("点旁边的擦笔布，再按住左键，从湿色左边向右擦。", true);
                }
                return true;
            case AtelierTargetKind.TrialCloth:
                if (!nearTrial)
                {
                    nearTrial = true;
                    game.FocusAt(TrialFocusPose, 47);
                    return true;
                }
                if (!session.State.Knows(AtelierRules.Trial))
                {
                    game.ShowObservation("先看看木板上的湿颜料。", true);
                    return true;
                }
                clothSelected = true;
                game.ShowObservation("布拿好了。按住湿色左边，沿着木板向右擦。", true);
                return true;
            case AtelierTargetKind.Ceiling:
                session.Inspect(StudioRules.Ceiling);
                game.ShowObservation(session.State.HasFact(AtelierRules.RoofErased)
                                         ? "灰蓝色被擦开了。梁木还在，中央露出夜空。"
                                         : "屋顶中央的灰蓝色盖在底下，边沿也有刷痕。",
                                     true);
                return true;
            case AtelierTargetKind.Sky:
                if (!Finale.RoofSettled)
                    return true;
                session.Inspect(StudioRules.Sky);
                game.ShowObservation(session.State.HasFact(StudioRules.FullSky)
                                         ? "星光留在缓慢流动的笔触之间。"
                                         : "从屋顶擦开的地方，可以直接看见夜空。",
                                     true);
                return true;
            case AtelierTargetKind.SharedMark:
                if (!session.State.HasFact(StudioRules.FullSky) || !Finale.RoofSettled)
                    return true;
                session.Inspect(AtelierRules.SharedMark);
                session.PerformAction(AtelierRules.SharedMark, "observe_whole");
                game.ShowObservation(
                    "同一道蓝灰笔迹，经过木框、灰泥和帘布都没有断。\n退回来，点右边的墙，把整间画室看在一起。",
                    true);
                return true;
            case AtelierTargetKind.Room:
                session.Inspect(StudioRules.Room);
                game.ShowObservation("梁木、窗、帘子和桌面连成一整幅画。\n这次要擦去的，是整间画室。", true);
                return true;
            default:
                return false;
            }
        }
        /// <summary>仅在完成画内三次解释且没有现实偏离时，展示明确的定稿意图。</summary>
        public void OfferDelivery()
        {
            bool ready = session.State.HasFact(AtelierRules.ReadyToDeliver) &&
                         !session.State.HasFact(AtelierRules.RealityTouched) && !session.State.CanvasLocked;
            game.ContextAction.gameObject.SetActive(ready);
            if (ready)
                game.ContextAction.GetComponentInChildren<UnityEngine.UI.Text>().text = "就交这张";
        }
        private void Deliver()
        {
            if (!game.IsPaintingFocused)
                return;
            session.PerformAction(AtelierRules.Painting, "deliver");
            game.ContextAction.gameObject.SetActive(false);
            game.ShowObservation("画稿定下了。先让它在画架上晾干，明早交稿。", true);
        }
        /// <summary>返回主视角取消布的临时选择；不会撤回已经提交的真实动作。</summary>
        public void CancelFocus()
        {
            nearTrial = clothSelected = wiping = false;
            if (Trial != null)
                Trial.Release();
        }
        private void OnDestroy()
        {
            if (game != null)
                game.ContextAction.onClick.RemoveListener(Deliver);
        }
    }
}
