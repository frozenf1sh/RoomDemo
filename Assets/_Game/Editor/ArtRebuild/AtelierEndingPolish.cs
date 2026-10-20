// 文件：AtelierEndingPolish.cs
// 职责：把第四阶段双结尾菜单从“重试页”整理成终章页面，并同步场景实例与Prefab母版。
// 边界：只修改结尾淡出、菜单文字与按钮排版；不改领域事实、流程分支或练习快照。
using System;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>结尾排版复用第四阶段生成器的同一入口，避免场景与Prefab各改一套。</summary>
    public static class AtelierEndingPolish
    {
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Prefab="Assets/_Game/Content/ArtRebuild/FourStage/Prefabs/PF_四阶段结尾占位.prefab";
        /// <summary>显式整理结尾菜单；运行和未保存场景前停止。</summary>
        [MenuItem("游戏/新四阶段流程/整理结尾菜单")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出Play。");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("场景尚未保存，未整理结尾菜单。");
            var scene=EditorSceneManager.OpenScene(Main);
            var flow=Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null||flow.StageFour==null)throw new InvalidOperationException("可玩副本缺少第四阶段装配。");
            var stage=flow.StageFour;
            // 旧场景仍以 EndingFade 引用淡出根；没有引用时再从上下文面板的同级查找。
            var fade=stage.EndingFade!=null?stage.EndingFade.transform
                :flow.References.ContextPanel.transform.parent.Find("四阶段结尾淡出");
            if(fade==null)throw new InvalidOperationException("找不到四阶段结尾淡出。");
            AtelierFlowStageFourBuilder.ApplyEndingLayout(flow,stage,fade);
            // 先应用Prefab实例上的新增控件与排版；只有非实例根才另存为Prefab。
            if(PrefabUtility.IsPartOfPrefabInstance(fade.gameObject))
                PrefabUtility.ApplyPrefabInstance(fade.gameObject,InteractionMode.AutomatedAction);
            else
                PrefabUtility.SaveAsPrefabAssetAndConnect(fade.gameObject,Prefab,InteractionMode.AutomatedAction);
            // ApplyPrefabInstance后场景实例仍在；按名称重新绑定，防止序列化引用被旧母版替换。
            var ending=fade.Find("结束菜单");
            stage.EndingFade=fade.GetComponent<CanvasGroup>();
            stage.EndingPanel=ending.gameObject;
            stage.EndingTitle=ending.Find("结尾标题").GetComponent<Text>();
            stage.EndingSubtitle=ending.Find("结尾副题").GetComponent<Text>();
            stage.RetryButton=ending.Find("回到点灯前_点击区").GetComponent<Button>();
            stage.AgainButton=ending.Find("重新开始").GetComponent<Button>();
            stage.QuitButton=ending.Find("退出").GetComponent<Button>();
            EditorUtility.SetDirty(stage);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("结尾菜单场景保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[结尾菜单] 已整理终章排版并同步Prefab母版。");
        }
    }
}
