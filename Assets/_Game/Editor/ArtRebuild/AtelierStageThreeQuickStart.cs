// 文件：AtelierStageThreeQuickStart.cs
// 职责：提供显式的开发菜单，从可玩场景直接快进到第三阶段开始的真实会话状态。
// 边界：只调用调试入口；不修改场景、Prefab、正式Build Settings或存档。
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Editor.ArtRebuild
{
    /// <summary>菜单可先进入Play，再在会话Start后调用；不要求先跑完开场旁白。</summary>
    [InitializeOnLoad]
    public static class AtelierStageThreeQuickStart
    {
        private const string Key="Atelier.StageQuickStart";
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        static AtelierStageThreeQuickStart(){EditorApplication.update+=Tick;}

        /// <summary>快进到第三阶段开始：窗景、板、刀可见，第三阶段观察尚未取得。</summary>
        [MenuItem("游戏/新四阶段流程/快进到第三阶段")]
        public static void JumpThird()=>JumpTo(3);

        /// <summary>快进到第三阶段完成、准备进第四阶段：前三层已收笔，灯和屋顶未动。</summary>
        [MenuItem("游戏/新四阶段流程/快进到第四阶段（第三阶段已完成）")]
        public static void JumpFourth()=>JumpTo(4);

        private static void JumpTo(int stage)
        {
            if(EditorApplication.isPlaying)
            {
                if(TryJump(stage))return;
                SessionState.SetInt(Key,stage);
                return;
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetInt(Key,stage);return;}
            var active=SceneManager.GetActiveScene();
            if(active.path!=Main)
            {
                if(active.isDirty)
                {
                    Debug.LogError("[四阶段] 当前场景有未保存修改，未打开可玩场景。");
                    return;
                }
                EditorSceneManager.OpenScene(Main);
            }
            // 域重载会清空编辑器静态字段；SessionState在Play启动后仍保留本次快进请求。
            SessionState.SetInt(Key,stage);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            int stage=SessionState.GetInt(Key,0);
            if(stage!=3&&stage!=4)return;
            if(!TryJump(stage))return;
            SessionState.SetInt(Key,0);
            Debug.Log(stage==4?"[四阶段] 已快进到第三阶段完成、准备进第四阶段。":"[四阶段] 已快进到第三阶段开始。");
        }

        private static bool TryJump(int stage)
        {
            var flow=UnityEngine.Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null||flow.Session==null)return false;
            if(stage==4)flow.FastForwardToFourthStage();
            else flow.FastForwardToThirdStage();
            return true;
        }
    }
}
