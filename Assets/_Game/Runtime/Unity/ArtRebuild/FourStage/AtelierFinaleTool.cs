// 文件：AtelierFinaleTool.cs
// 职责：把画布、已调查屋面和墙面的真实指针接触提交为末阶段有限工具命令。
// 边界：工具选择与捕获只是输入；已完成路径和终局保存在领域，不按帧授予事实。
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity.ArtRebuild
{
    /// <summary>沿用第三阶段的刀面、刀刃含义，画笔模式继续使用原有有限落笔。</summary>
    public enum AtelierFinaleToolMode { Paint,Face,Edge }
    /// <summary>一段拖动或三次释放短点；失焦、暂停、返回和模式切换都取消旧手势。</summary>
    public sealed class AtelierFinaleTool : MonoBehaviour
    {
        public AtelierFlowStageFour Stage;
        public Transform PointerKnife;
        private GameSession session;
        private readonly List<AtelierContactSegment> path=new List<AtelierContactSegment>();
        private int taps,revision;
        private string epoch;
        private bool held,moved;
        private Vector2 pressed,previous;
        private Vector3 contactNormal=Vector3.up;
        /// <summary>临时工具选择，切换模式不会获得经验或完成事实。</summary>
        public AtelierFinaleToolMode Mode { get; private set; }
        /// <summary>当前工具与近景表面匹配，尚需独立的输入和领域许可。</summary>
        public bool Active => Stage.Flow.IsCanvasFocused&&Mode==AtelierFinaleToolMode.Face
            ||Stage.Flow.IsRoofFocused&&session!=null&&session.State.HasFact(AtelierFlowRules.RoofAsPaint)
            ||Stage.Flow.IsRoomSurfaceFocused&&session!=null&&session.State.HasFact(AtelierFlowRules.RoomAsPaint);
        /// <summary>接入本轮会话，默认画笔模式并清除旧的未提交手势。</summary>
        public void Bind(GameSession game) {session=game;Mode=AtelierFinaleToolMode.Paint;Cancel();}
        /// <summary>正式Workspace和临时按钮共用，不因选择工具写入完成事实。</summary>
        public void SelectMode(AtelierFinaleToolMode mode)
        {
            if(!Stage.Flow.CanUseStageThreeInput||Stage.Ending)return;
            if(mode==AtelierFinaleToolMode.Face&&session.State.AtelierCanvas.Layers[3].Count<3)return;
            Cancel();Mode=mode;
            Stage.Flow.CanvasPort.Preview(Vector2.zero,false);
        }
        /// <summary>只读取目标表面对应的局部UV，不从相邻物体或画面空处延长接触。</summary>
        public bool TryHit(Ray ray,out Vector2 uv,out Vector3 world)
        {
            uv=Vector2.zero;world=Vector3.zero;
            var hits=Physics.RaycastAll(ray,40,Stage.Flow.References.PickingMask,QueryTriggerInteraction.Collide)
                .OrderBy(h=>h.distance);
            foreach(var hit in hits)
            {
                if(Stage.SkipPicking(hit))continue;
                var target=hit.collider.GetComponent<AtelierInteractionTarget>();
                if(target==null)return false;
                if(Stage.Flow.IsCanvasFocused&&target.Kind==AtelierTargetKind.Painting)uv=hit.textureCoord;
                else if(Stage.Flow.IsRoofFocused&&target==Stage.RoofTarget)
                    uv=new Vector2((hit.point.x-Stage.RoofOrigin.x)/Stage.RoofSize.x+.5f,(hit.point.z-Stage.RoofOrigin.z)/Stage.RoofSize.y+.5f);
                else if(Stage.Flow.IsRoomSurfaceFocused&&target==Stage.RoomTarget)
                {
                    var b=hit.collider.bounds;
                    uv=new Vector2(Mathf.InverseLerp(b.min.x,b.max.x,hit.point.x),Mathf.InverseLerp(b.min.z,b.max.z,hit.point.z));
                }
                else return false;
                world=hit.point;contactNormal=hit.normal;

                return Inside(uv);
            }
            return false;
        }
        /// <summary>手势只在释放后提交；辅助短点每次按下松开只计一次。</summary>
        public bool HandlePointer(bool overUI)
        {
            if(!Active) {PointerKnife.gameObject.SetActive(false);return false;}
            var flow=Stage.Flow;
            if(!flow.CanUseStageThreeInput||Stage.Ending||overUI||Mouse.current==null||!UnityEngine.Application.isFocused)
            {Cancel();return true;}
            var mouse=Mouse.current;
            bool hit=TryHit(flow.References.ViewCamera.ScreenPointToRay(mouse.position.ReadValue()),out var uv,out var world);
            PointerKnife.gameObject.SetActive(hit);
            if(hit)
            {
                PointerKnife.position=world+contactNormal*.014f;
                PointerKnife.rotation=Quaternion.FromToRotation(Vector3.up,contactNormal)*Quaternion.Euler(0,-20,(Mode==AtelierFinaleToolMode.Face||Stage.Flow.IsRoomSurfaceFocused)?8:68);
            }
            if(mouse.leftButton.wasPressedThisFrame&&hit)
            {
                held=true;moved=false;pressed=previous=uv;
                revision=session.State.AtelierCanvas.Revision;epoch=session.SessionEpoch;
            }
            if(held&&mouse.leftButton.isPressed)
            {
                if(!hit) {Cancel();return true;}
                if(Vector2.Distance(pressed,uv)>.012f)moved=true;
                if(moved&&Vector2.Distance(previous,uv)>.002f) {Add(previous,uv);previous=uv;}
            }
            if(held&&mouse.leftButton.wasReleasedThisFrame)
            {
                held=false;
                if(!hit||revision!=session.State.AtelierCanvas.Revision||epoch!=session.SessionEpoch) {Cancel();return true;}
                if(moved) {Add(previous,uv);Submit();}
                else
                {
                    Add(uv,uv);taps++;
                    if(taps>=3)Submit();
                }
            }
            return true;
        }
        private static bool Inside(Vector2 uv)=>uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1;
        private void Add(Vector2 a,Vector2 b)
        {
            var value=new AtelierContactSegment(Mathf.RoundToInt(a.x*10000),Mathf.RoundToInt(a.y*10000),Mathf.RoundToInt(b.x*10000),Mathf.RoundToInt(b.y*10000));
            if(path.Count<64)path.Add(value);else path[63]=value;
        }
        private void Submit()
        {
            var result=ApplyContact(path.ToArray(),Mathf.Min(taps,3));
            if(!result.Accepted)Stage.Flow.ShowWorldObservation(result.Reason);
            Cancel();
        }
        /// <summary>同一公开接触入口供正式UI与自动取证使用；世界射线仍由调用方保证。</summary>
        public AtelierCanvasResult ApplyContact(IReadOnlyList<AtelierContactSegment> contacts,int assistedTaps=0)
        {
            var flow=Stage.Flow;
            if(!flow.CanUseStageThreeInput||Stage.Ending||!Active) return new AtelierCanvasResult(false,"当前不能使用刮刀。");

            var action=flow.IsCanvasFocused?AtelierFinaleAction.SpreadCanvas:flow.IsRoomSurfaceFocused?AtelierFinaleAction.EraseRoom
                :session.State.HasFact(AtelierFlowRules.RoofProbed)?AtelierFinaleAction.ContinueRoof:AtelierFinaleAction.ProbeRoof;
            return session.ApplyAtelierFinale(session.SessionEpoch,session.State.AtelierCanvas.Revision,action,contacts,assistedTaps);
        }
        /// <summary>取消未提交路径与短点计数；已确认作品、屋孔和收色不受影响。</summary>
        public void Cancel()
        {
            held=moved=false;taps=0;path.Clear();
            if(PointerKnife!=null)PointerKnife.gameObject.SetActive(false);
        }
    }
}
