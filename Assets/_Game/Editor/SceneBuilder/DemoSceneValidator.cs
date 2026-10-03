// =============================================================================
// 文件：DemoSceneValidator.cs
// 职责：构建前校验场景实体、绑定、资源、字体与结构。
// 边界：只验证，不静默修复或保存人工修改；失败抛异常阻止构建。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Editor
{
    /// <summary>构建前校验场景实体、绑定、资源、字体与结构。</summary>
    public static class DemoSceneValidator
    {
        /// <summary>验证已保存的场景、绑定、资源与嵌入中文字体，失败立即抛异常。</summary>
        public static void ValidateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式，再验证或构建。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder.ScenePath) == null)
                throw new FileNotFoundException("请先通过 DemoSceneBuilder.Build 生成房间场景。");
            var scene = SceneManager.GetSceneByPath(DemoSceneBuilder.ScenePath);
            // 已加载场景直接验证；未加载时临时追加打开，结束后只关闭自己打开的场景。
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(x => x.name == "DemoRoom");
                foreach (var name in new[] { "_Bootstrap", "_Systems", "CameraRig", "Environment", "Interactables", "Effects", "UI" })
                    if (root.transform.Find(name) == null) throw new InvalidOperationException("缺少场景分组：" + name);
                if (root.GetComponentsInChildren<GameBootstrap>(true).Length != 1 || root.GetComponentsInChildren<NotebookView>(true).Length != 1 ||
                    root.GetComponentsInChildren<InteractionSystem>(true).Length != 1 || root.GetComponentsInChildren<Camera>(true).Length != 1)
                    throw new InvalidOperationException("场景系统缺失或重复。");
                // 同时检查数量、唯一性与内容覆盖，重复 ID 也必须阻止构建。
                var entities = root.GetComponentsInChildren<SceneEntity>(true);
                if (entities.Length != 5 || entities.Select(x => x.Id).Distinct().Count() != 5 ||
                    DemoRules.Create().Objects.Any(x => !entities.Any(e => e.Id == x.Id)))
                    throw new InvalidOperationException("场景应恰好包含盆栽、书、窗户、画、纸张，且标识符不得重复。");
                var required = new[] { typeof(PlantPresenter), typeof(BookPresenter), typeof(WindowPresenter), typeof(WindPresenter), typeof(PaintingPresenter), typeof(PaperPresenter), typeof(WaterPresenter), typeof(EndingPresenter) };
                var presenters = root.GetComponentsInChildren<WorldPresenter>(true);
                if (required.Any(t => presenters.Count(p => p.GetType() == t) != 1)) throw new InvalidOperationException("核心表现组件缺失或重复。");
                // 所有表现组件必须恰好绑定一次，避免漏播或双重播放。
                var bindings = new SerializedObject(root.GetComponentInChildren<GameBootstrap>(true)).FindProperty("presenters");
                if (bindings.arraySize != presenters.Length || Enumerable.Range(0, bindings.arraySize).Select(i => bindings.GetArrayElementAtIndex(i).objectReferenceValue).Distinct().Count() != presenters.Length ||
                    presenters.Any(p => !Enumerable.Range(0, bindings.arraySize).Any(i => bindings.GetArrayElementAtIndex(i).objectReferenceValue == p)))
                    throw new InvalidOperationException("启动组件必须恰好绑定每个表现组件一次。");
                // 未保存场景不能被静默忽略，构建必须使用明确的磁盘版本。
                if (scene.isDirty) throw new InvalidOperationException("请先保存房间场景；验证和构建不会静默使用未保存的修改。");
                foreach (var entity in entities)
                    if (entity.GetComponent<Collider>() == null || entity.GetComponent<Clickable>() == null || entity.transform.parent.Find("Visual") == null)
                        throw new InvalidOperationException("交互逻辑与视觉结构不完整：" + entity.Id);
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) throw new InvalidOperationException("场景包含缺失的脚本。");
                    var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                    // 遍历项目 Adapter 的序列化引用，定位缺失绑定的完整字段路径。
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null &&
                            component.GetType().Namespace == "Game.Unity")
                            throw new InvalidOperationException($"缺失引用：{component.name}/{component.GetType().Name}.{property.propertyPath}");
                }
                var audio = root.GetComponentInChildren<WindPresenter>(true);
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                    if (source.clip == null) throw new InvalidOperationException("缺少音频片段：" + source.name);
                if (audio.WindAudio.clip == null) throw new InvalidOperationException("风声音频缺失。");
                // 同时检查字体资产与打包配置；仅在 Editor 中能读中文并不足以证明构建正常。
                var chineseFont = AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath);
                var fontImporter = AssetImporter.GetAtPath(DemoSceneBuilder.ChineseFontPath) as TrueTypeFontImporter;
                if (chineseFont == null || fontImporter == null || !fontImporter.includeFontData || fontImporter.fontTextureCase != FontTextureCase.Dynamic)
                    throw new InvalidOperationException("中文字体缺失，或未配置为嵌入字体数据的动态字体。");
                if (root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(x => x.font != chineseFont) ||
                    root.GetComponentsInChildren<TextMesh>(true).Any(x => x.font != chineseFont))
                    throw new InvalidOperationException("面板或场景标签未使用项目中文字体。");
                Debug.Log("场景验证器：场景验证通过。");
            }
            // 只回收验证器临时打开的场景，保留用户原本的 Editor 工作状态。
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
