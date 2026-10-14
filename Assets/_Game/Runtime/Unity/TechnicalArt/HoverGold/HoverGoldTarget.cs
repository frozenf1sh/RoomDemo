// 文件：HoverGoldTarget.cs
// 职责：把一个物品的多个可见 Renderer 合成同一悬停身份，并保存柔和淡入进度。
// 边界：只持有表现状态；不替换材质、不修改稳定物品 ID，也不访问游戏事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>显式指定整件物品的视觉边界；逻辑 Collider 可放在独立子节点。</summary>
    public sealed class HoverGoldTarget : MonoBehaviour
    {
        [Tooltip("只包含该物品自身的视觉，不包含桌面、标签或其他物品。")]
        public Renderer[] Surfaces;
        [Min(.1f)] public float FadeInSeconds = 1.35f;
        [Min(.1f)] public float FadeOutSeconds = .55f;
        private float progress;
        private bool hovered;

        /// <summary>零斜率起止的淡入权重；离开时从当前进度消退。</summary>
        public float Opacity => Mathf.SmoothStep(0, 1, progress);
        /// <summary>物品可见面的子网格数量，绑定时缓存，避免逐帧读取材质数组。</summary>
        public int[] SubmeshCounts { get; private set; }

        private void OnEnable()
        {
            // 只在显式未指定时收集子节点；生产接入需要审核这份视觉清单。
            if (Surfaces == null || Surfaces.Length == 0) Surfaces = GetComponentsInChildren<Renderer>();
            SubmeshCounts = new int[Surfaces.Length];
            for (int i = 0; i < Surfaces.Length; i++)
                SubmeshCounts[i] = Surfaces[i] != null ? Surfaces[i].sharedMaterials.Length : 0;
            ResetEffect();
        }

        private void OnDisable()
        {
            ResetEffect();
        }

        /// <summary>由输入适配层通知当前是否悬停；不产生调查或收集命令。</summary>
        public void SetHovered(bool value) => hovered = value;

        /// <summary>立即清除渐变，用于停用、输入阻断、重开及会话结束。</summary>
        public void ResetEffect()
        {
            hovered = false;
            progress = 0;
        }

        private void Update()
        {
            // 使用受暂停影响的时间；来回移入沿现有进度反转，不突然点亮。
            float duration = hovered ? FadeInSeconds : FadeOutSeconds;
            progress = Mathf.MoveTowards(progress, hovered ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, duration));
        }

        /// <summary>提供世界包围盒以缩小轮廓扫描区域；最终金丝仍沿实际几何投影生成。</summary>
        /// <param name="bounds">所有启用视觉的合并包围盒。</param>
        /// <returns>至少存在一个启用视觉时返回 true。</returns>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var surface in Surfaces)
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                if (!found) bounds = surface.bounds;
                else bounds.Encapsulate(surface.bounds);
                found = true;
            }
            return found;
        }
    }
}
