// 文件：AtelierSkyFilmRevision.cs
// 职责：增量安装固定画幅星夜，保存独立复刻场景并更新两份画室的天空接线。
// 边界：不重建画室，不改规则、镜头姿态或正式 Build Settings；未保存场景会阻止操作。
using System;
using Game.Unity.TechnicalArt.RealtimePainterly;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>把原 Hero 完整渲染接到固定视角，保留正式房间的其他人工美术。</summary>
    public static class AtelierSkyFilmRevision
    {
        public const string PreviewPath = "Assets/_Game/Content/Scenes/Experiments/TechnicalArt/FixedViewSkyBenchmark.unity";
        private const string Matter = "Assets/_Game/Content/TechnicalArt/LivingMatter/";
        private const string Production = "Assets/_Game/Content/TechnicalArt/Production/";

        /// <summary>先生成独立复刻样例，不保存主场景。</summary>
        [MenuItem("游戏/技术美术/固定画幅星夜/生成复刻预览")]
        public static void BuildPreview()
        {
            Guard();
            InstallFeature("Assets/_Game/Content/TechnicalArt/LivingMatter/Rendering/MatterRenderer.asset");
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/Experiments/TechnicalArt/LivingStarryNightHero.unity");
            // 先另存，再修改副本；原 Hero 与其已通过的资源保持原样。
            EditorSceneManager.SaveScene(scene, PreviewPath);
            var hero = Object.FindAnyObjectByType<LivingMatterHero>();
            var gpu = hero.Sky;
            var camera = hero.View;
            var pipeline = hero.Pipeline;
            var root = hero.gameObject;
            Object.DestroyImmediate(hero);
            var sky = root.AddComponent<LivingSkyController>();
            sky.Pigment = gpu;
            sky.Surface = gpu.Target;
            sky.InitialCoverage = sky.InitialFlow = sky.InitialLight = 1;
            sky.FixedView = true;
            Configure(camera, sky);
            var benchmark = root.AddComponent<SkyFilmBenchmark>();
            benchmark.Pipeline = pipeline;
            benchmark.Sky = sky;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>只增量更新画室天空和摄像机接线，不触碰场景其他对象。</summary>
        [MenuItem("游戏/技术美术/固定画幅星夜/接入画室固定视角")]
        public static void InstallAtelier()
        {
            Guard();
            InstallFeature("Assets/_Game/Content/ArtRebuild/Rendering/画室渲染器.asset");
            foreach (string name in new[] { "AtelierNight", "AtelierPlayable" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/" + name + ".unity");
                var sky = Object.FindAnyObjectByType<LivingSkyController>();
                if (sky == null) throw new InvalidOperationException("画室没有现有天空：" + name);
                Camera camera = sky.FollowCamera != null ? sky.FollowCamera : Camera.main;
                Configure(camera, sky);
                var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(sky.gameObject);
                if (prefab != null) PrefabUtility.UnpackPrefabInstance(prefab, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                // 原 Renderer 只传递 API 参数，不再保留或绘制大天空球。
                var filter = sky.Surface.GetComponent<MeshFilter>();
                filter.sharedMesh = null;
                sky.Surface.transform.localScale = Vector3.one;
                sky.Surface.gameObject.name = "星夜参数载体_实际使用固定画幅";
                sky.Surface.enabled = false;
                sky.Pigment.Width = 1024;
                sky.Pigment.Height = 576;
                sky.Pigment.AutomaticTime = false;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Configure(Camera camera, LivingSkyController sky)
        {
            sky.FixedView = true;
            sky.BoundedHistory = true;
            sky.HistorySeconds = 21;
            sky.HistoryBlendSeconds = 6;
            var frame = camera.GetComponent<FixedViewSky>();
            if (frame == null) frame = camera.gameObject.AddComponent<FixedViewSky>();
            frame.Sky = sky;
            frame.FilmShader = Required<Shader>(Production + "Shaders/FixedViewSkyFilm.shader");
            frame.FilmMesh = Required<Mesh>(Matter + "Meshes/天空画面载体.asset");
            frame.FilmVolume = Required<VolumeProfile>(Matter + "Rendering/SkyVolume.asset");
            EditorUtility.SetDirty(frame);
            EditorUtility.SetDirty(sky);
        }

        private static void InstallFeature(string path)
        {
            var renderer = Required<UniversalRendererData>(path);
            var feature = renderer.rendererFeatures.Find(x => x is FixedViewSkyFeature) as FixedViewSkyFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FixedViewSkyFeature>();
                feature.name = "固定画幅星夜_原版绘画深度合成";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.Configure(Required<Shader>(Production + "Shaders/FixedViewSkyComposite.shader"));
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }

        private static T Required<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidOperationException("缺少已有绘画资源：" + path);
            return value;
        }

        private static void Guard()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止预览播放。");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，停止增量安装。");
        }
    }
}
