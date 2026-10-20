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
        private const string Key="Atelier.StageThreeQuickStart";
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        static AtelierStageThreeQuickStart(){EditorApplication.update+=Tick;}

        /// <summary>打开可玩场景并进入Play，等会话建立后快进；在Play中则立即尝试。</summary>
        [MenuItem("游戏/新四阶段流程/快进到第三阶段")]
        public static void Jump()
        {
            if(EditorApplication.isPlaying)
            {
                if(TryJump())return;
                SessionState.SetBool(Key,true);
                return;
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,true);return;}
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
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if(!EditorApplication.isPlaying||!SessionState.GetBool(Key,false))return;
            if(!TryJump())return;
            SessionState.SetBool(Key,false);
            Debug.Log("[四阶段] 已快进到第三阶段开始。");
        }

        private static bool TryJump()
        {
            var flow=UnityEngine.Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null||flow.Session==null)return false;
            flow.FastForwardToThirdStage();
            return true;
        }
    }
}
