// =============================================================================
// 文件：PresentationArtPass.cs
// 职责：在已保存场景上逐项应用构图与美术调整，保留玩法身份和人工摆放。
// 边界：所有场景修改通过 Editor API；每个入口先拒绝运行模式和未保存修改。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Editor
{
    /// <summary>针对当前单房间的具体美术工具，不重建或覆盖整张场景。</summary>
    public static class PresentationArtPass
    {
        /// <summary>取得已保存的房间；拒绝丢弃开发者尚未保存的调整。</summary>
        public static Transform Room()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请退出运行模式再调整美术。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存当前场景，再执行美术工具。");
            var scene = SceneManager.GetSceneByPath(DemoSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath);
            return scene.GetRootGameObjects().Single(x => x.name == "DemoRoom").transform;
        }
        /// <summary>保存增量调整；不应用覆盖到共享 Prefab 或改变物体 ID。</summary>
        public static void Save(Transform root)
        {
            // 记录实例覆盖，使共享灰盒 Prefab 保持独立的技术基线。
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
        }
        /// <summary>调整三件主物体的视觉关系，给画框下的水迹留出无遮挡区域。</summary>
        [MenuItem("游戏/美术/应用主物体构图")]
        public static void Composition()
        {
            var root = Room();
            var painting = root.GetComponentInChildren<PaintingPresenter>(true).transform;
            painting.localPosition = new Vector3(2.72f, 1.85f, 1.30f);
            root.GetComponentInChildren<WaterPresenter>(true).transform.localPosition = new Vector3(2.72f, 0, 1.30f);
            // 桌椅退到右侧，避免遮住第一滴水的落点，保持固定机位可读性。
            root.Find("Environment/Table").localPosition = new Vector3(3.1f, 0, -.5f);
            root.Find("Environment/Chair").localPosition = new Vector3(3.5f, 0, -1.1f);
            root.GetComponentInChildren<WindowPresenter>(true).transform.localPosition = new Vector3(1.55f, 2.1f, 3.65f);
            root.GetComponentInChildren<Camera>().fieldOfView = 59;
            Save(root);
            Debug.Log("美术构图：画、水迹、植物与窗户的观察空间已调整。");
        }
        /// <summary>建立暖灰与两个定向局部光，初始状态没有运动冷光。</summary>
        [MenuItem("游戏/美术/应用房间光照")]
        public static void Lighting()
        {
            var root = Room();
            var systems = root.Find("_Systems");
            var sun = systems.Find("Sun").GetComponent<Light>();
            sun.color = new Color(1, .81f, .62f); sun.intensity = .65f;
            sun.transform.rotation = Quaternion.Euler(36, -32, 0);
            var fill = systems.Find("RoomFill").GetComponent<Light>();
            fill.color = new Color(1, .64f, .36f); fill.intensity = 1.35f; fill.range = 9;
            fill.transform.localPosition = new Vector3(-1.8f, 2.5f, -.5f);
            var presentation = Ensure(root, "Presentation");
            var lighting = Ensure(presentation, "Lighting");
            var window = Ensure(lighting, "WindowLight").GetComponent<Light>();
            if (window == null) window = lighting.Find("WindowLight").gameObject.AddComponent<Light>();
            window.type = LightType.Point; window.range = 8; window.intensity = .2f;
            window.color = new Color(.78f, .84f, .82f); window.transform.localPosition = new Vector3(1.4f, 2, 3);
            var painting = Ensure(lighting, "PaintingLight").GetComponent<Light>();
            if (painting == null) painting = lighting.Find("PaintingLight").gameObject.AddComponent<Light>();
            painting.type = LightType.Point; painting.range = 5; painting.intensity = 0;
            painting.color = new Color(.2f, .73f, .78f); painting.transform.localPosition = new Vector3(2.65f, 1.7f, .8f);
            root.GetComponentInChildren<EndingPresenter>(true).ConfigureLighting(fill, window, painting);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.23f, .21f, .19f);
            Save(root);
        }
        /// <summary>只补缺少的技术分组，不覆盖已有子对象或人工摆放。</summary>
        public static Transform Ensure(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) return found;
            var created = new GameObject(name).transform;
            created.SetParent(parent, false);
            return created;
        }
        /// <summary>只补演出技术对象，不覆盖主物体的人工摆放和已绑定资源。</summary>
        [MenuItem("游戏/美术/补齐缺失技术对象")]
        public static void CreateMissingTechnicalObjects() { InstallTechnicalObjects(Room()); }
        /// <summary>给指定房间安装导演，独立灰盒也可复用此入口。</summary>
        public static void InstallTechnicalObjects(Transform root)
        {
            var presentation = Ensure(root, "Presentation");
            var lighting = Ensure(presentation, "Lighting");
            var windowLight = Ensure(lighting, "WindowLight").GetComponent<Light>();
            if (windowLight == null) { windowLight = lighting.Find("WindowLight").gameObject.AddComponent<Light>(); windowLight.range = 8; windowLight.intensity = .2f; windowLight.transform.localPosition = new Vector3(1.4f, 2, 3); }
            var seaLight = Ensure(lighting, "PaintingLight").GetComponent<Light>();
            if (seaLight == null) { seaLight = lighting.Find("PaintingLight").gameObject.AddComponent<Light>(); seaLight.range = 5; seaLight.intensity = 0; seaLight.transform.localPosition = new Vector3(2.65f, 1.7f, .8f); }
            var painting = root.GetComponentInChildren<PaintingPresenter>(true);
            var water = root.GetComponentInChildren<WaterPresenter>(true);
            var plant = root.GetComponentInChildren<PlantPresenter>(true);
            var window = root.GetComponentInChildren<WindowPresenter>(true);
            var ending = root.GetComponentInChildren<EndingPresenter>(true);
            ending.ConfigureLighting(root.Find("_Systems/RoomFill").GetComponent<Light>(), windowLight, seaLight);
            // 仅填充缺失引用；有效的人工作品材质、音频和模型不被替换。
            if (new SerializedObject(water).FindProperty("firstDropAudio").objectReferenceValue == null) water.ConfigureDropAudio(AudioArtPass.Source(water.transform, "Drop", false, .7f));
            if (new SerializedObject(plant).FindProperty("aquaticAudio").objectReferenceValue == null) plant.ConfigureAquaticAudio(AudioArtPass.Source(plant.transform, "Aquatic", false, .45f));
            if (new SerializedObject(window).FindProperty("oceanAudio").objectReferenceValue == null) window.ConfigureOceanAudio(AudioArtPass.Source(window.transform, "Ocean", false, .5f));
            var node = Ensure(Ensure(presentation, "Sequences"), "PaintingSeaSequence");
            var sequence = node.GetComponent<PaintingSeaSequence>(); if (sequence == null) sequence = node.gameObject.AddComponent<PaintingSeaSequence>();
            sequence.Configure(painting, water, plant, window, ending);
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureSequence(sequence);
            Save(root);
        }
        /// <summary>离线渲染固定观察机位，用于前后构图比较；不代表实际玩家帧率。</summary>
        public static void Capture(string path)
        {
            var root = Room();
            var camera = root.GetComponentInChildren<Camera>();
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                // 临时截图资源与相机目标必须归还，不能污染保存的场景。
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
