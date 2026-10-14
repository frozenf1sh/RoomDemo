// 文件：AtelierHoverGoldRevision.cs
// 职责：向可玩画室增量安装悬停金丝，并显式绑定每件物品的视觉范围。
// 边界：不重建场景、不修改稳定身份、正式入口或原有 Renderer Feature。
using System;
using System.Linq;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.HoverGold;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>可重复执行的局部安装；只有明确的有限物品视觉参与轮廓提示。</summary>
    public static class AtelierHoverGoldRevision
    {
        /// <summary>安装摄像机入口、物品视觉及置顶渲染通道，并保存当前可玩副本。</summary>
        /// <returns>安装后的逻辑目标与视觉集合数量。</returns>
        /// <exception cref="InvalidOperationException">场景未保存、正在运行或缺少必要视觉时拒绝安装。</exception>
        [MenuItem("游戏/新画室玩法/安装悬停金色游丝")]
        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || scene.path != AtelierGameplayBuilder.ScenePath)
                throw new InvalidOperationException("请先保存并打开 AtelierPlayable，停止运行后再安装悬停金丝。");
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            if (game == null || game.Finale == null || game.Curtain == null)
                throw new InvalidOperationException("当前场景缺少完整玩法绑定。");
            var targets = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AtelierInteractionTarget>(true)).ToArray();
            var camera = game.ViewCamera.GetComponent<HoverGoldCamera>();
            if (camera == null) camera = game.ViewCamera.gameObject.AddComponent<HoverGoldCamera>();
            game.HoverGold = camera;
            foreach (var target in targets) target.HoverVisual = null;

            Bind(targets, AtelierTargetKind.WashWater, Visual(game.Wash.transform,
                game.Wash.transform.Find("Visual_有厚口和内壁的洗笔罐").GetComponent<Renderer>(), game.Wash.Water));
            Bind(targets, AtelierTargetKind.Brush, Visual(game.Wash.BrushSurface.transform,
                game.Wash.BrushSurface.GetComponent<Renderer>()));
            Bind(targets, AtelierTargetKind.Painting, Visual(game.Painting.Surface.transform.parent,
                game.Painting.Surface.transform.parent.GetComponentsInChildren<Renderer>(true)));
            var note = game.transform.Find("Visual_交稿便条");
            Bind(targets, AtelierTargetKind.Manuscript, Visual(note, note.GetComponent<Renderer>()));
            var lamp = GameObject.Find("独立夜间画室/返工_辅助工作簇/辅助工作台上的小油灯").transform;
            Bind(targets, AtelierTargetKind.OilLamp, Visual(lamp,
                lamp.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.Contains("glass") && !r.name.Contains("flame"))
                    .Append(game.Lamp.WickSurface).ToArray()));
            Bind(targets, AtelierTargetKind.Wick, Visual(game.Lamp.WickSurface.transform, game.Lamp.WickSurface));
            var mainLamp = GameObject.Find("独立夜间画室/返工_扫描工作簇/扫描维多利亚旧油灯").transform;
            Bind(targets, AtelierTargetKind.StillLamp, Visual(mainLamp,
                mainLamp.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.Contains("flame")).ToArray()));
            var matches = game.Lamp.transform.Find("Visual_旧纸火柴盒");
            var matchVisual = Visual(matches, matches.GetComponentsInChildren<Renderer>(true));
            Bind(targets, AtelierTargetKind.Matches, matchVisual);
            Bind(targets, AtelierTargetKind.MatchStriker, matchVisual);
            Bind(targets, AtelierTargetKind.Window, Visual(game.Curtain.Cloth[0].transform.parent,
                game.Finale.FrameSurfaces));
            for (int i = 0; i < game.Curtain.Cloth.Length; i++)
            {
                var visual = Visual(game.Curtain.Cloth[i].transform, game.Curtain.Cloth[i].GetComponent<Renderer>());
                targets.Single(t => t.Kind == AtelierTargetKind.Curtain && t.name == "Logic_窗帘_" + i).HoverVisual = visual;
            }
            Bind(targets, AtelierTargetKind.CurtainCord, Visual(game.Curtain.Cord.transform,
                game.Curtain.Cord, game.Curtain.Cord.transform.parent.Find("绳结上的旧铜拉坠").GetComponent<Renderer>()));
            var board = game.Trial.transform.Find("沾湿色的旧木板");
            Bind(targets, AtelierTargetKind.Trial, Visual(board, board.GetComponent<Renderer>(), game.Trial.WetPaint));
            Bind(targets, AtelierTargetKind.TrialCloth, Visual(game.Trial.Cloth.transform,
                game.Trial.Cloth.GetComponent<Renderer>()));
            // 天空、整间画室、贯通笔迹和满画幅屋顶没有独立物品外缘，继续使用既有观察文字。
            // 不把它们的逻辑包围盒、整张桌面或整个工作簇当成可见物品来描边。
            InstallFeature();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "悬停金丝已接入可玩画室：逻辑目标=" + targets.Count(t => t.HoverVisual != null) +
                   "，独立视觉=" + targets.Where(t => t.HoverVisual != null).Select(t => t.HoverVisual).Distinct().Count();
        }

        /// <summary>复用同一物品的表现组件，只收录经过明确筛选的视觉面。</summary>
        private static HoverGoldTarget Visual(Transform owner, params Renderer[] surfaces)
        {
            surfaces = surfaces.Where(r => r != null).Distinct().ToArray();
            if (surfaces.Length == 0) throw new InvalidOperationException("缺少悬停物品视觉：" + owner.name);
            var visual = owner.GetComponent<HoverGoldTarget>();
            if (visual == null) visual = owner.gameObject.AddComponent<HoverGoldTarget>();
            visual.Surfaces = surfaces;
            return visual;
        }

        /// <summary>让多个逻辑部位共用整件物品的金丝，不更改其交互 ID。</summary>
        private static void Bind(AtelierInteractionTarget[] targets, AtelierTargetKind kind, HoverGoldTarget visual)
        {
            foreach (var target in targets.Where(t => t.Kind == kind)) target.HoverVisual = visual;
        }

        /// <summary>保留已有管线通道，追加金丝并同步 Unity 的子资产恢复映射。</summary>
        private static void InstallFeature()
        {
            const string path = "Assets/_Game/Content/ArtRebuild/Rendering/画室渲染器.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var feature = renderer.rendererFeatures.OfType<HoverGoldFeature>().SingleOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<HoverGoldFeature>();
                feature.name = "悬停金色游丝";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Content/TechnicalArt/HoverGold/Shaders/HoverGold.shader"));
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }
    }
}
