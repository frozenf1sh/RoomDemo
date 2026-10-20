// 文件：AtelierFlowGameplay.StageFour.cs
// 职责：补充末阶段固定机位、终局输入隔离与内存练习的生命周期。
// 边界：恢复由应用层创建新世代；场景重载负责取消旧字幕、演出与自有材质。
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Unity.ArtRebuild
{
    /// <summary>主装配器仍是唯一会话持有者，正式UI只调用这些明确入口。</summary>
    public sealed partial class AtelierFlowGameplay
    {
        private static AtelierPracticeSnapshot practice,pendingPractice;
        private bool restoringPractice;
        public bool IsLampFocused => focus=="lamp";
        public bool IsRoofFocused => focus=="roof";
        public bool IsRoomSurfaceFocused => focus=="room_surface";
        public bool IsRoomView => focus=="";
        public bool IsEnding => StageFour!=null&&StageFour.Ending;
        private GameSession CreateFlowSession()
        {
            restoringPractice=pendingPractice!=null;
            if(!restoringPractice) {practice=null;return new GameSession(AtelierFlowRules.CreateOpening());}
            var snapshot=pendingPractice;pendingPractice=null;
            practice=snapshot;
            return GameSession.FromAtelierPractice(snapshot);
        }
        /// <summary>有限机位入口，禁止自由转头、缩放或未许可的终局后镜头。</summary>
        public void FocusStageFour(string kind,Transform pose,float fov)
        {
            if(!CanUseStageThreeInput||StageFour==null)return;
            CancelPointer();References.Lamp.PutMatchDown();Focus(kind,pose,fov);
        }
        /// <summary>屋面第二次揭底后回到主构图，只改变镜头，不代授天空、笔迹或房间。</summary>
        public void ReturnFromToolForWorld() => ReturnToRoom();
        /// <summary>终局接受后立即取消所有暂存手势和遮挡面板，旁白与淡出由末阶段演出负责。</summary>
        public void PrepareEnding()
        {
            References.Notebook.SetOpen(false);
            CancelPointer();References.Lamp.PutMatchDown();Wash.CancelContact();Mirror.End();
            References.ContextPanel.SetActive(false);References.ReturnButton.gameObject.SetActive(false);
            References.Reminder.gameObject.SetActive(false);
            if(Session.State.HasFact(AtelierRules.RoomErased))
            {
                focus="";StartMove(home,homeTurn,homeFov);
            }
            else if(Session.State.CanvasLocked)
            {
                var camera=References.ViewCamera;
                StartMove(camera.transform.position+camera.transform.forward*.05f,camera.transform.rotation,camera.fieldOfView-1);
            }
        }
        /// <summary>清空本轮练习入口并重载开场，旧会话不能继续提交。</summary>
        public void RestartGame()
        {
            pendingPractice=null;practice=null;Time.timeScale=1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        /// <summary>从点灯前重试，保留真实前三层；不重放开场，不写硬盘存档。</summary>
        public void RetryFourthStage()
        {
            if(practice==null)return;
            pendingPractice=practice;Time.timeScale=1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
    }
}
