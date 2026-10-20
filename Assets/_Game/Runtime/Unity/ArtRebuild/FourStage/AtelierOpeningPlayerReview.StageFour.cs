// 文件：AtelierOpeningPlayerReview.StageFour.cs
// 职责：在真实前三层之后检查点灯、暖星、接触收色、两次屋面、双收束和练习重载。
// 边界：仅显式启动的自动回放，实际InputSystem鼠标事件仍不能算作真人验收。
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Game.Unity.ArtRebuild
{
    /// <summary>同一演示包依次跑普通线和恢复后的现实线，记录真实作品与Console。</summary>
    public sealed partial class AtelierOpeningPlayerReview
    {
        private IEnumerator ReviewFourthStage(AtelierFlowGameplay flow)
        {
            float reviewSpeed=Time.timeScale;
            string firstThree=Hash(flow.CanvasPort.CanvasTexture as RenderTexture);
            bool practiceRestored=false,normalCompleted=false;
            string oldEpoch=flow.Session.SessionEpoch;
            Check(flow.StageFour!=null&&flow.Draft.WeightForStage(4)>.99f,"N05结束未接入第四阶段显色。");
            Check(flow.Draft.WeightForStage(0)==0,"点灯前提前显露了屋顶。");
            yield return Shot("26_N05后只开放点灯簇");
            for(int route=0;route<2;route++)
            {
                var stage=flow.StageFour;var refs=flow.References;var canvas=flow.CanvasPort;
                var targets=FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include);
                AtelierInteractionTarget Target(AtelierTargetKind kind)=>targets.First(t=>t.Kind==kind);
                void FirstHit(AtelierTargetKind kind)
                {
                    var collider=Target(kind).GetComponent<Collider>();
                    var ray=refs.ViewCamera.ScreenPointToRay(refs.ViewCamera.WorldToScreenPoint(collider.bounds.center));
                    var hits=Physics.RaycastAll(ray,40,refs.PickingMask,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).Where(h=>!stage.SkipPicking(h)).ToArray();
                    Check(hits.Length>0&&hits[0].collider==collider,"第四阶段目标被其他拾取面挡住："+kind);
                }
                FirstHit(AtelierTargetKind.OilLamp);
                flow.Observe(Target(AtelierTargetKind.OilLamp));yield return new WaitForSeconds(1.5f);
                Check(!flow.Session.State.Knows(AtelierFlowRules.C("warm")),"只调查未燃油灯就给了温暖。");
                FirstHit(AtelierTargetKind.Matches);flow.Observe(Target(AtelierTargetKind.Matches));
                FirstHit(AtelierTargetKind.MatchStriker);flow.Observe(Target(AtelierTargetKind.MatchStriker));
                yield return new WaitForSeconds(9);
                Check(refs.Lamp.HasBurningMatch,"火柴仍有短促燃尽倒计时。");
                var mouse=InputSystem.AddDevice<Mouse>();
                var screen=refs.ViewCamera.WorldToScreenPoint(refs.Lamp.Wick.position);
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(screen.x,screen.y)});
                yield return new WaitForSecondsRealtime(.7f);
                yield return null;yield return null;
                Check(refs.Lamp.CanTouchWick,"鼠标靠近灯芯时火柴头未形成实际接触。");
                FirstHit(AtelierTargetKind.Wick);flow.Observe(Target(AtelierTargetKind.Wick));
                yield return new WaitForSeconds(.3f);
                Check(flow.Session.State.HasFact(StudioRules.LampTurned)&&flow.Voice.ActiveKey=="N06","有效点灯后未记录观察和播放N06。");
                Check(flow.Draft.WeightForStage(0)==0&&!stage.RoofTrace.gameObject.activeSelf,"N06结束前提前露出屋面或细痕。");
                if(route==0)yield return Shot("27_有效接触点灯与N06");
                float deadline=Time.realtimeSinceStartup+45;
                while((flow.Voice.ActiveKey!=""||flow.Draft.WeightForStage(0)<.99f)&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(flow.Draft.WeightForStage(0)>.99f,"N06结束后屋面没有独立渐显。");
                flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
                if(route==0)yield return Shot("28_N06后屋面持久细痕");
                refs.Notebook.SetOpen(true);yield return null;yield return null;yield return null;yield return new WaitForSecondsRealtime(.3f);
                var paper=refs.Notebook.CombinationCanvas;
                foreach(var item in paper.CombinationInput.ToArray())paper.Remove(item);
                paper.Add(AtelierFlowRules.C("warm"),new Vector2(-90,20));paper.Add(AtelierFlowRules.C("bright"),new Vector2(90,20));
                refs.Notebook.InterpretSelected();
                Check(flow.Session.State.Knows(AtelierFlowRules.Brush(4))&&paper.CombinationInput.Single()==AtelierFlowRules.Brush(4),"暖星没有同帧替换两个灰词。");
                if(route==0)yield return Shot("29_暖星笔刷与灰词消费");
                refs.Notebook.SetOpen(false);yield return null;yield return null;yield return null;
                flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.5f);
                for(int n=0;n<2;n++)Check(canvas.Commit(new Vector2(.30f+n*.28f,.78f),canvas.DefaultCost,0).Accepted,"暖星前两笔无法落下。");
                stage.Tool.SelectMode(AtelierFinaleToolMode.Face);
                Check(stage.Tool.Mode!=AtelierFinaleToolMode.Face&&!canvas.Finish().Accepted,"不足三笔就开放了刀面或普通收束。");
                Check(canvas.Commit(new Vector2(.75f,.60f),canvas.DefaultCost,20).Accepted,"第三枚暖星未接受。");
                if(route==1)
                {
                    for(int n=0;n<3;n++)Check(canvas.Commit(new Vector2(.34f+n*.16f,.63f),canvas.DefaultCost,0).Accepted,"暖星余下颜料无法按六笔用尽。");
                    Check(canvas.Document.Current.Count==6&&canvas.Document.Current.RemainingPaint==0,"第四层没有用合法六笔耗尽颜料。");
                }
                stage.FaceButton.onClick.Invoke();
                Check(stage.Tool.Mode==AtelierFinaleToolMode.Face,"三笔后无法选择最终刀面。");
                string beforeSpread=Hash(canvas.CanvasTexture as RenderTexture);
                Vector3 CanvasPoint(Vector2 uv)=>WorldForUV(canvas.Surface,uv);
                yield return DragFinale(flow,mouse,new Vector2(.26f,.78f),new Vector2(.42f,.78f),CanvasPoint);
                Check(canvas.Document.SpreadRevision==canvas.Document.Revision&&canvas.CanFinish,"有效刀面未完成当前版本收色。");
                Check(beforeSpread!=Hash(canvas.CanvasTexture as RenderTexture),"刀面收色没有改变真实作品RT。");
                if(route==0)
                {
                    yield return Shot("30_暖星三笔与实际刀面收色");
                    stage.PaintButton.onClick.Invoke();
                    canvas.Commit(new Vector2(.5f,.80f),canvas.DefaultCost,0);
                    Check(!canvas.CanFinish&&canvas.Document.SpreadRevision==-1,"继续落笔后仍然允许旧收色定稿。");
                    Check(canvas.Undo().Accepted,"末层撤销失败。");stage.FaceButton.onClick.Invoke();
                    yield return DragFinale(flow,mouse,new Vector2(.26f,.78f),new Vector2(.42f,.78f),CanvasPoint);
                    Check(canvas.CanFinish,"撤销后重新收色没有恢复完成许可。");
                }
                flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
                FirstHit(AtelierTargetKind.Ceiling);flow.Observe(Target(AtelierTargetKind.Ceiling));yield return new WaitForSeconds(1.5f);
                Check(flow.Session.State.Knows(StudioRules.Ceiling)&&!stage.RoofTargetSelected,"屋面来源未亲点或直接代选了笔记目标。");
                stage.TargetButton.onClick.Invoke();yield return null;yield return null;yield return null;
                refs.Notebook.SelectObject(StudioRules.Ceiling);refs.Notebook.SetOpen(false);yield return null;yield return null;yield return null;
                Check(stage.RoofTargetSelected,"笔记选择屋面没有接到工具目标。");stage.EdgeButton.onClick.Invoke();
                Vector3 RoofPoint(Vector2 uv)=>stage.RoofOrigin+new Vector3((uv.x-.5f)*stage.RoofSize.x,-.002f,(uv.y-.5f)*stage.RoofSize.y);
                yield return TapFinale(flow,mouse,new Vector2(.5f,.5f),RoofPoint);
                Check(flow.Session.State.HasFact(AtelierFlowRules.RoofProbed)&&!flow.Session.State.HasFact(AtelierFlowRules.WorldCommitted),"第一次屋面短点没有小试或过早锁了路线。");
                Check(!flow.Session.State.Knows(StudioRules.Sky)&&!flow.Session.State.Knows(StudioRules.Room),"小孔自动代领天空或房间。");
                yield return new WaitForSeconds(.3f);
                if(route==0)
                {
                    yield return Shot("31_首次小孔仍保留普通收束");
                    flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);
                    flow.Observe(Target(AtelierTargetKind.Painting));yield return new WaitForSeconds(1.5f);
                    Check(canvas.CanFinish,"试刮小孔后普通完成被错误取消。");refs.ContextAction.onClick.Invoke();
                    deadline=Time.realtimeSinceStartup+60;while(!stage.MenuVisible&&Time.realtimeSinceStartup<deadline)yield return null;
                    normalCompleted=stage.MenuVisible&&stage.EndingTitle.text=="重新点亮"&&flow.Session.State.CanvasLocked;
                    Check(normalCompleted,"普通结尾没有按真实作品完成。");
                    yield return Shot("32_普通结尾重新点亮");
                    InputSystem.RemoveDevice(mouse);
                    string previousObject=flow.GetEntityId().ToString();stage.RetryButton.onClick.Invoke();
                    yield return null;yield return null;
                    flow=FindAnyObjectByType<AtelierFlowGameplay>();
                    deadline=Time.realtimeSinceStartup+30;
                    while((flow==null||flow.Session==null)&&Time.realtimeSinceStartup<deadline) {yield return null;flow=FindAnyObjectByType<AtelierFlowGameplay>();}
                    Time.timeScale=reviewSpeed;yield return new WaitForSeconds(.3f);
                    Check(flow!=null&&flow.GetEntityId().ToString()!=previousObject&&flow.Session.SessionEpoch!=oldEpoch,"第四阶段重试未生成新世代。");
                    practiceRestored=Hash(flow.CanvasPort.CanvasTexture as RenderTexture)==firstThree;
                    Check(practiceRestored,"第四阶段重试丢失或改变了真实前三层画面。");
                    Check(flow.CanvasPort.Document.Current.Id==4&&flow.CanvasPort.Document.Current.Count==0&&!flow.Session.State.HasFact(StudioRules.LampTurned),"第四阶段重试残留点灯或最后一层。");
                    Check(!flow.Session.State.Knows(StudioRules.Ceiling)&&!flow.IsOpening&&flow.Voice.ActiveKey=="","重试残留屋面调查或重新播放了开场。");
                    yield return Shot("33_练习恢复点灯前三层原作");
                }
                else
                {
                    yield return DragFinale(flow,mouse,new Vector2(.63f,.5f),new Vector2(.78f,.5f),RoofPoint);
                    Check(flow.Session.State.HasFact(AtelierFlowRules.WorldCommitted)&&flow.Session.State.HasFact(AtelierRules.RoofErased),"第二段有效揭底未主动扩大开口。");
                    Check(!canvas.CanFinish,"扩大屋面后仍能普通定稿。");
                    yield return new WaitForSeconds(9);
                    Check(!flow.Session.State.Knows(StudioRules.Sky)&&!flow.Session.State.Knows(StudioRules.Room),"等待屋顶演出自动发了天空或房间。");
                    if(!flow.IsRoomView) {flow.ReturnToRoom();yield return new WaitForSeconds(1.5f);}
                    yield return Shot("34_屋顶开口与现有星空");
                    FirstHit(AtelierTargetKind.Sky);flow.Observe(Target(AtelierTargetKind.Sky));yield return null;yield return null;
                    FirstHit(AtelierTargetKind.SharedMark);flow.Observe(Target(AtelierTargetKind.SharedMark));yield return null;yield return null;
                    Check(flow.Session.State.HasFact(AtelierRules.SharedMarkSeen)&&!flow.Session.State.Knows(StudioRules.Room),"笔迹调查未成立或代领了房间。");
                    yield return Shot("35_亲点天空与跨表面笔迹");
                    FirstHit(AtelierTargetKind.Room);flow.Observe(Target(AtelierTargetKind.Room));yield return new WaitForSeconds(1.5f);
                    Check(flow.Session.State.Knows(StudioRules.Room),"窗下墙面没有亲点取得房间。");stage.EdgeButton.onClick.Invoke();
                    Vector3 RoomPoint(Vector2 uv)
                    {
                        var b=stage.RoomTarget.GetComponent<Collider>().bounds;
                        return new Vector3(Mathf.Lerp(b.min.x,b.max.x,uv.x),Mathf.Lerp(b.min.y,b.max.y,uv.y),b.min.z);
                    }
                    yield return DragFinale(flow,mouse,new Vector2(.38f,.5f),new Vector2(.56f,.5f),RoomPoint);
                    Check(flow.Session.State.HasFact(AtelierRules.RoomErased)&&!flow.Session.State.CanvasLocked,"最后墙面划痕没有进入现实收束。");
                    yield return new WaitForSeconds(6);yield return Shot("36_统一笔势擦开房间");
                    deadline=Time.realtimeSinceStartup+35;while(!refs.Finale.Finished&&Time.realtimeSinceStartup<deadline)yield return null;
                    yield return Shot("36b_全屋擦开后保留现有星空");
                    deadline=Time.realtimeSinceStartup+50;while(!stage.MenuVisible&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(stage.MenuVisible&&stage.EndingTitle.text=="星之河之外","现实结尾未完整收束。");
                    yield return Shot("37_现实结尾星之河之外");
                    File.WriteAllText(Path.Combine(output,"末阶段状态.json"),JsonUtility.ToJson(new FourthEvidence {width=Screen.width,height=Screen.height,speed=reviewSpeed,
                        layerCounts=canvas.Document.Layers.Select(l=>l.Count).ToArray(),normalCompleted=normalCompleted,practiceRestored=practiceRestored,
                        roofOpened=flow.Session.State.HasFact(AtelierRules.RoofErased),roomKnown=flow.Session.State.Knows(StudioRules.Room),roomErased=flow.Session.State.HasFact(AtelierRules.RoomErased),
                        skyShader=refs.Finale.Sky.Surface.sharedMaterial.shader.name,skyFlow=refs.Finale.Sky.Flow},true));
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }
        [Serializable]private sealed class FourthEvidence {public int width,height;public float speed,skyFlow;public int[] layerCounts;public bool normalCompleted,practiceRestored,roofOpened,roomKnown,roomErased;public string skyShader;}
        private IEnumerator DragFinale(AtelierFlowGameplay flow,Mouse mouse,Vector2 a,Vector2 b,Func<Vector2,Vector3> world)
        {
            var p=flow.References.ViewCamera.WorldToScreenPoint(world(a));
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(p.x,p.y),buttons=1});yield return null;yield return null;
            for(int n=1;n<=12;n++)
            {
                p=flow.References.ViewCamera.WorldToScreenPoint(world(Vector2.Lerp(a,b,n/12f)));
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(p.x,p.y),buttons=1});yield return null;yield return null;
            }
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(p.x,p.y)});yield return null;yield return null;yield return null;
        }
        private IEnumerator TapFinale(AtelierFlowGameplay flow,Mouse mouse,Vector2 uv,Func<Vector2,Vector3> world)
        {
            var p=flow.References.ViewCamera.WorldToScreenPoint(world(uv));
            for(int n=0;n<3;n++)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(p.x,p.y),buttons=1});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(p.x,p.y)});yield return null;yield return null;
                if(n<2)Check(!flow.Session.State.HasFact(AtelierFlowRules.RoofProbed),"不足三次短点就刮开了屋面。");
            }
        }
        /// <summary>从真实网格UV三角形反求世界接触点，避免模型轴向与导入旋转造成假射线。</summary>
        private static Vector3 WorldForUV(Renderer surface,Vector2 uv)
        {
            var collider=surface.GetComponentsInChildren<MeshCollider>(true).First();
            var mesh=collider.sharedMesh;var vertices=mesh.vertices;var tex=mesh.uv;var triangles=mesh.triangles;
            for(int n=0;n<triangles.Length;n+=3)
            {
                int a=triangles[n],b=triangles[n+1],c=triangles[n+2];Vector2 v0=tex[b]-tex[a],v1=tex[c]-tex[a],v2=uv-tex[a];
                float den=v0.x*v1.y-v1.x*v0.y;if(Mathf.Abs(den)<.00001f)continue;
                float u=(v2.x*v1.y-v1.x*v2.y)/den,v=(v0.x*v2.y-v2.x*v0.y)/den;
                if(u<0||v<0||u+v>1)continue;
                return collider.transform.TransformPoint(vertices[a]*(1-u-v)+vertices[b]*u+vertices[c]*v);
            }
            throw new InvalidOperationException("找不到画布UV对应的真实网格表面。");
        }
    }
}
