// =============================================================================
// 文件：WorldMutation.cs
// 职责：提供少量场景表现操作，捕获被改节点的初始姿态，并管理按需生成物。
// 边界：不引用 GameSession，不接受规则，不产生事实；只有具体演出消费者调用它。
//       没有通用脚本语言、反射、资产加载框架或运行时世界生成算法。
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>显示、隐藏、替换、生成、变换、材质与主题的直接表现操作。</summary>
    public sealed class WorldMutation : MonoBehaviour
    {
        [SerializeField] private VisualThemeController theme;
        private readonly Dictionary<Transform, Pose> poses = new Dictionary<Transform, Pose>();
        private readonly Dictionary<string, GameObject> spawned = new Dictionary<string, GameObject>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new Dictionary<Renderer, MaterialPropertyBlock>();
        private MaterialPropertyBlock animationBlock;
        // 属性块包含 Unity 原生资源，必须在主线程生命周期中创建，不能放在字段初始化器。
        private void Awake() { animationBlock = new MaterialPropertyBlock(); }
        public int SpawnedCount => spawned.Count;
        public void Configure(VisualThemeController controller) { theme = controller; }
        private void Remember(Transform node)
        {
            // 初次修改才记录；演出每帧调用不会不断分配快照。
            if (!poses.ContainsKey(node)) poses.Add(node, new Pose(node));
        }
        public void Show(GameObject node) { Remember(node.transform); node.SetActive(true); }
        public void Hide(GameObject node) { Remember(node.transform); node.SetActive(false); }
        public void ReplaceVisual(GameObject oldVisual, GameObject newVisual) { Hide(oldVisual); Show(newVisual); }
        /// <summary>同一槽位只生成一个实例；重复最终恢复不会堆积篝火。</summary>
        public GameObject Spawn(string slot, GameObject prefab, Transform parent)
        {
            if (spawned.TryGetValue(slot, out var existing) && existing != null) return existing;
            var instance = Instantiate(prefab, parent, false); instance.name = slot; spawned[slot] = instance; return instance;
        }
        public void Move(Transform node, Vector3 from, Vector3 to, float progress) { Remember(node); node.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress))); }
        public void Rotate(Transform node, Quaternion from, Quaternion to, float progress) { Remember(node); node.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress))); }
        public void Scale(Transform node, Vector3 from, Vector3 to, float progress) { Remember(node); node.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress))); }
        /// <summary>颜色过渡使用属性块；允许共享材质且能恢复原有块。</summary>
        public void MaterialTransition(Renderer renderer, Color from, Color to, float progress)
        {
            if (!originals.ContainsKey(renderer)) { var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original); originals.Add(renderer, original); }
            renderer.GetPropertyBlock(animationBlock); animationBlock.SetColor(ShaderIds.BaseColor, Color.Lerp(from, to, Mathf.Clamp01(progress))); renderer.SetPropertyBlock(animationBlock);
        }
        public void TransitionTheme(VisualThemeProfile profile, float progress) { theme.Transition(profile, progress); }
        /// <summary>退出旧演出时恢复自己借用的节点，立即停用旧生成物，再延迟销毁。</summary>
        public void ResetCapturedState()
        {
            foreach (var pair in poses)
            {
                if (pair.Key == null) continue; var pose = pair.Value;
                pair.Key.localPosition = pose.Position; pair.Key.localRotation = pose.Rotation; pair.Key.localScale = pose.Scale; pair.Key.gameObject.SetActive(pose.Active);
            }
            foreach (var pair in originals) if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
            foreach (var instance in spawned.Values) if (instance != null) { instance.SetActive(false); Destroy(instance); }
            poses.Clear(); originals.Clear(); spawned.Clear();
            // 场景卸载的组件销毁顺序不固定；主题组件可能已经先于本组件被销毁。
            if (theme != null) theme.ResetTheme();
        }
        /// <summary>临时姿态快照，不作为世界状态或存档使用。</summary>
        private readonly struct Pose
        {
            public readonly Vector3 Position, Scale;
            public readonly Quaternion Rotation;
            public readonly bool Active;
            public Pose(Transform node) { Position = node.localPosition; Rotation = node.localRotation; Scale = node.localScale; Active = node.gameObject.activeSelf; }
        }
        private void OnDestroy() { ResetCapturedState(); }
    }
}
