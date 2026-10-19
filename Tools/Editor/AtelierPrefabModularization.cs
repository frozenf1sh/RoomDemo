// =============================================================================
// 文件：AtelierPrefabModularization.cs
// 职责：经 Unity Editor API 将已保存的可玩画室拆为自然工作簇 Prefab，并审计引用与组件数据。
// 边界：由 Pipeline run_script 临时编译执行，不进入运行时程序集，不重建场景、不改变玩法规则。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>只迁移现有层级，不重新摆放物件；保存失败时恢复本次操作前的场景。</summary>
public static class AtelierPrefabModularization
{
    private const string ScenePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
    private const string Destination = "Assets/_Game/Content/Prefabs/AtelierModules";
    private const string Evidence = "Logs/FlowRedesign/Prefab";
    private static readonly HashSet<string> Ignored = new HashSet<string>
    {
        "m_PrefabInstance", "m_PrefabAsset", "m_CorrespondingSourceObject", "m_RootOrder"
    };

    /// <summary>读取当前开发场景并输出迁移候选与跨模块引用，不写场景或Prefab。</summary>
    public static string Audit()
    {
        var scene = RequireScene();
        Directory.CreateDirectory(Evidence);
        var targets = Candidates(scene).ToArray();
        var data = new
        {
            scene = scene.path,
            objects = Transforms(scene).Count(),
            candidates = targets.Select(x => new { path = Hierarchy(x.Item1.transform), asset = x.Item2,
                objects = x.Item1.GetComponentsInChildren<Transform>(true).Length,
                alreadyPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(x.Item1),
                externalReferences = ExternalReferences(x.Item1).ToArray() }).ToArray()
        };
        File.WriteAllText(Evidence + "/audit.json", JsonConvert.SerializeObject(data, Formatting.Indented));
        return JsonConvert.SerializeObject(data);
    }

    /// <summary>迁移自然边界，核对原始数据后保存，再重开验证持久化引用。</summary>
    /// <returns>审计结果；任意数据变化会抛异常并恢复原场景。</returns>
    public static string Migrate()
    {
        var scene = RequireScene();
        Directory.CreateDirectory(Evidence);
        string backup = Evidence + "/AtelierPlayable.before.unity";
        if (File.Exists(backup) && HashFile(backup) != HashFile(ScenePath))
            throw new InvalidOperationException("已有备份与当前场景不同，已拒绝覆盖恢复点。");
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        var before = Snapshot(scene);
        File.WriteAllText(Evidence + "/before.json", JsonConvert.SerializeObject(before, Formatting.Indented));
        var referenceBindings = CaptureBindings(scene);
        var protectedFiles = new[] { "ProjectSettings/QualitySettings.asset", "ProjectSettings/EditorBuildSettings.asset",
            "Assets/_Game/Content/Scenes/AtelierNight.unity" }.ToDictionary(x => x, HashFile);
        var created = new List<string>();
        var modules = new List<object>();
        try
        {
            // 候选来自既有自然层级；先检查所有路径，保证不会覆盖旧母版。
            var candidates = Candidates(scene).Where(x => !PrefabUtility.IsAnyPrefabInstanceRoot(x.Item1)).ToArray();
            foreach (var item in candidates)
                if (File.Exists(item.Item2)) throw new IOException("目标已经存在，已拒绝覆盖：" + item.Item2);
            foreach (var item in candidates)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Item2));
                AssetDatabase.Refresh();
                var external = ExternalReferences(item.Item1).ToArray();
                int count = item.Item1.GetComponentsInChildren<Transform>(true).Length;
                bool saved;
                PrefabUtility.SaveAsPrefabAssetAndConnect(item.Item1, item.Item2, InteractionMode.AutomatedAction, out saved);
                if (!saved) throw new IOException("Prefab保存失败：" + item.Item2);
                created.Add(item.Item2);
                modules.Add(new { path = Hierarchy(item.Item1.transform), asset = item.Item2, objects = count,
                    externalReferences = external });
            }
            // Prefab资产不持有外部场景引用；实例通过明确覆盖保留原绑定。
            RestoreBindings(scene, referenceBindings);
            var after = Snapshot(scene);
            AssertSame(before, after, "连接Prefab后");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("模块化场景保存失败。");
            AssetDatabase.SaveAssets();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reloaded = Snapshot(scene);
            AssertSame(before, reloaded, "重新打开场景后");
            foreach (var pair in protectedFiles)
                if (HashFile(pair.Key) != pair.Value) throw new InvalidOperationException("受保护文件发生变化：" + pair.Key);
            File.WriteAllText(Evidence + "/after-reload.json", JsonConvert.SerializeObject(reloaded, Formatting.Indented));
            var report = new { success = true, scene = ScenePath, createdCount = created.Count,
                objectCount = Transforms(scene).Count(), comparedProperties = before.Count,
                snapshotHash = HashText(JsonConvert.SerializeObject(before)), protectedFiles,
                modules, validation = "连接后与重开后组件数据、引用、层级、Transform均与迁移前一致；尚需运行检查。" };
            string json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(Evidence + "/result.json", json);
            Debug.Log("[画室模块化] 已拆分 " + created.Count + " 个Prefab，组件与引用审计通过。");
            return json;
        }
        catch (Exception error)
        {
            // 精确恢复本次备份，不接触用户其他资产或已有Prefab。
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            File.Copy(backup, ScenePath, true);
            foreach (string path in created) AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            File.WriteAllText(Evidence + "/failure.txt", error.ToString());
            throw;
        }
    }

    /// <summary>拒绝运行中、未保存场景与错误入口；不会替用户保存或丢弃手工改动。</summary>
    private static Scene RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，已拒绝迁移。");
        var scene = SceneManager.GetActiveScene();
        // 无界面命令从空场景启动；仅在批处理且无未保存内容时打开明确的开发入口。
        if (UnityEngine.Application.isBatchMode && scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (scene.path != ScenePath) throw new InvalidOperationException("请打开已保存的AtelierPlayable开发场景。");
        return scene;
    }

    /// <summary>按现有自然父节点拆分，场景组合根、机位、跨模块装配和声音入口仍留在场景。</summary>
    private static IEnumerable<Tuple<GameObject, string>> Candidates(Scene scene)
    {
        var art = scene.GetRootGameObjects().Single(x => x.name == "独立夜间画室").transform;
        var game = scene.GetRootGameObjects().Single(x => x.name == "新画室玩法").transform;
        foreach (string group in new[] { "建筑与木作", "室内家具", "窗外山谷", "原创风景习作" })
        {
            var parent = art.Find(group);
            if (parent == null) continue;
            foreach (Transform child in parent)
            {
                // 村落根含大量房屋网格，先逐栋成为小模块，再保存只负责组合的父Prefab。
                if (child.name == "中景错落小屋")
                    foreach (Transform house in child)
                        yield return Target(house, "村落房屋");
                yield return Target(child, group);
            }
        }
        foreach (string group in new[] { "返工_扫描工作簇", "返工_辅助工作簇", "桌面与创作痕迹", "照明", "持续流动的程序化星夜" })
        {
            var child = art.Find(group);
            if (child != null) yield return Target(child, "表现工作簇");
        }
        foreach (string group in new[] { "独立洗笔工作簇", "点灯工作簇", "拉帘工作簇", "湿色与整体擦除工作簇",
            "Logic_室内遮挡", "Visual_交稿便条", "EventSystem" })
        {
            var child = game.Find(group);
            if (child != null) yield return Target(child, "交互工作簇");
        }
        var canvas = game.Find("画室纸面与字幕");
        if (canvas != null)
            foreach (Transform child in canvas)
                yield return Target(child, "UI");
    }

    /// <summary>保留中文语义的文件名；同分类内不重命名任何原场景对象。</summary>
    private static Tuple<GameObject, string> Target(Transform transform, string category)
    {
        string safe = string.Concat(transform.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Tuple.Create(transform.gameObject, Destination + "/" + category + "/PF_" + safe + ".prefab");
    }

    /// <summary>枚举所有含禁用对象的现有节点，用兄弟序号建立不依赖中文重名的地址。</summary>
    private static IEnumerable<Transform> Transforms(Scene scene) => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true));
    private static string Address(Transform value) => value.parent == null ? value.GetSiblingIndex().ToString() : Address(value.parent) + "/" + value.GetSiblingIndex();
    private static string Hierarchy(Transform value) => value.parent == null ? value.name : Hierarchy(value.parent) + "/" + value.name;

    /// <summary>场景引用使用节点地址和组件序号，资产引用使用GUID与本地ID。</summary>
    private static string Identity(Object value)
    {
        if (value == null) return "null";
        if (EditorUtility.IsPersistent(value))
        {
            string guid; long local;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out local);
            return "asset:" + guid + ":" + local;
        }
        var go = value as GameObject;
        if (go != null) return "go:" + Address(go.transform);
        var component = value as Component;
        if (component != null) return "component:" + Address(component.transform) + ":" + Array.IndexOf(component.gameObject.GetComponents<Component>(), component);
        return value.GetType().FullName + ":" + value.name;
    }

    /// <summary>获取序列化叶子值，排除仅因连接Prefab改变的元信息。</summary>
    private static string Value(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.ObjectReference: return Identity(p.objectReferenceValue);
            case SerializedPropertyType.ExposedReference: return Identity(p.exposedReferenceValue);
            case SerializedPropertyType.Integer: return p.longValue.ToString();
            case SerializedPropertyType.Float: return p.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.String: return p.stringValue;
            case SerializedPropertyType.Enum: return p.intValue.ToString();
            case SerializedPropertyType.LayerMask: return p.intValue.ToString();
            case SerializedPropertyType.Character: return p.intValue.ToString();
            case SerializedPropertyType.ArraySize: return p.intValue.ToString();
            case SerializedPropertyType.Color: return p.colorValue.ToString("R");
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString("R");
            case SerializedPropertyType.Vector3: return p.vector3Value.ToString("R");
            case SerializedPropertyType.Vector4: return p.vector4Value.ToString("R");
            case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString("R");
            case SerializedPropertyType.Rect: return p.rectValue.ToString("R");
            case SerializedPropertyType.Bounds: return p.boundsValue.ToString("R");
            case SerializedPropertyType.Vector2Int: return p.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int: return p.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt: return p.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt: return p.boundsIntValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                return JsonConvert.SerializeObject(new { keys = p.animationCurveValue.keys,
                    pre = p.animationCurveValue.preWrapMode, post = p.animationCurveValue.postWrapMode });
            case SerializedPropertyType.Hash128: return p.hash128Value.ToString();
            case SerializedPropertyType.ManagedReference: return p.managedReferenceFullTypename;
            case SerializedPropertyType.Generic: return p.isArray ? "array:" + p.arraySize : p.type;
            default:
                try { return JsonConvert.SerializeObject(p.boxedValue); }
                catch { return p.type; }
        }
    }

    /// <summary>完整记录层级、对象属性和各组件可序列化值，覆盖跨Prefab引用与稳定ID。</summary>
    private static SortedDictionary<string, string> Snapshot(Scene scene)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in Transforms(scene))
        {
            string key = Address(t);
            result[key + ":object"] = t.name + "|" + t.gameObject.activeSelf + "|" + t.gameObject.layer + "|" + t.gameObject.tag + "|" + (int)GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            var components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                string owner = key + ":" + i;
                if (c == null) { result[owner] = "missing"; continue; }
                result[owner] = c.GetType().FullName;
                using (var so = new SerializedObject(c))
                {
                    var p = so.GetIterator();
                    bool enterChildren = true;
                    while (p.Next(enterChildren))
                    {
                        // 对象引用按语义地址比较，不比较Unity内部指针的FileID/PathID。
                        enterChildren = p.propertyType != SerializedPropertyType.ObjectReference &&
                            p.propertyType != SerializedPropertyType.ExposedReference && !Ignored.Contains(p.propertyPath);
                        if (Ignored.Contains(p.propertyPath)) continue;
                        result[owner + ":" + p.propertyPath] = Value(p);
                    }
                }
            }
        }
        return result;
    }

    /// <summary>保存所有场景引用；恢复阶段仅修复确实改变的引用，不重写其他属性。</summary>
    private static List<Tuple<string, int, string, Object>> CaptureBindings(Scene scene)
    {
        var result = new List<Tuple<string, int, string, Object>>();
        foreach (var t in Transforms(scene))
        {
            var components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                using (var so = new SerializedObject(components[i]))
                {
                    var p = so.GetIterator();
                    while (p.Next(true))
                        if (p.propertyType == SerializedPropertyType.ObjectReference && !Ignored.Contains(p.propertyPath))
                            result.Add(Tuple.Create(Address(t), i, p.propertyPath, p.objectReferenceValue));
                }
            }
        }
        return result;
    }

    /// <summary>保持对象实例，外部绑定写为场景实例覆盖。</summary>
    private static void RestoreBindings(Scene scene, List<Tuple<string, int, string, Object>> bindings)
    {
        var nodes = Transforms(scene).ToDictionary(Address);
        foreach (var row in bindings)
        {
            var owner = nodes[row.Item1].GetComponents<Component>()[row.Item2];
            using (var so = new SerializedObject(owner))
            {
                var p = so.FindProperty(row.Item3);
                if (p == null || p.objectReferenceValue == row.Item4) continue;
                p.objectReferenceValue = row.Item4;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            }
        }
    }

    /// <summary>列出工作簇对其他场景对象的依赖，交接时明确哪些引用仍由场景提供。</summary>
    private static IEnumerable<string> ExternalReferences(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            using (var so = new SerializedObject(c))
            {
                var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || Ignored.Contains(p.propertyPath)) continue;
                    var value = p.objectReferenceValue;
                    if (value == null || EditorUtility.IsPersistent(value)) continue;
                    var target = value as GameObject ?? (value as Component)?.gameObject;
                    if (target != null && target != root && !target.transform.IsChildOf(root.transform) && p.propertyPath != "m_Father")
                        yield return Hierarchy(c.transform) + "." + p.propertyPath + " -> " + Hierarchy(target.transform);
                }
            }
        }
    }

    /// <summary>把差异完整落盘后拒绝保存；不把对象数量相同误报为全部数据相同。</summary>
    private static void AssertSame(SortedDictionary<string, string> before, SortedDictionary<string, string> after, string stage)
    {
        var differences = before.Keys.Union(after.Keys).Where(k => !before.ContainsKey(k) || !after.ContainsKey(k) || before[k] != after[k])
            .Select(k => k + "\n之前=" + (before.ContainsKey(k) ? before[k] : "缺失") + "\n之后=" + (after.ContainsKey(k) ? after[k] : "缺失")).ToArray();
        if (differences.Length == 0) return;
        File.WriteAllLines(Evidence + "/differences.txt", differences);
        throw new InvalidOperationException(stage + "发现 " + differences.Length + " 项差异，已拒绝交付。");
    }

    /// <summary>计算固定指纹以核实共享设置和正式场景未被迁移触及。</summary>
    private static string HashFile(string path) => HashBytes(File.ReadAllBytes(path));
    private static string HashText(string value) => HashBytes(System.Text.Encoding.UTF8.GetBytes(value));
    private static string HashBytes(byte[] value)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
    }
}
