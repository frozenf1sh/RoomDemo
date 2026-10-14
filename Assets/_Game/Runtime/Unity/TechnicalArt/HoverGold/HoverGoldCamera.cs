// 文件：HoverGoldCamera.cs
// 职责：接收既有玩法拾取结果，保存当前金光目标与尚在消退的视觉。
// 边界：不读取输入、不做第二次射线，也不访问或修改游戏事实。
using System.Collections.Generic;
using UnityEngine;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>摄像机的悬停表现入口；目标许可由所属玩法输入系统决定。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HoverGoldCamera : MonoBehaviour
    {
        private readonly List<HoverGoldTarget> visible = new();

        /// <summary>当前许可的悬停视觉；不会调查或收集目标。</summary>
        public HoverGoldTarget Current { get; private set; }
        /// <summary>只读的当前及消退目标，供所属摄像机的渲染通道读取。</summary>
        public IReadOnlyList<HoverGoldTarget> Visible => visible;

        /// <summary>切换悬停视觉；旧物品自然消退，新物品从自身当前进度浮现。</summary>
        /// <param name="target">既有玩法拾取允许的目标；空值表示指针离开。</param>
        public void SetCurrent(HoverGoldTarget target)
        {
            if (!isActiveAndEnabled)
            {
                ResetEffect();
                return;
            }
            if (target != null && !target.isActiveAndEnabled) target = null;
            if (Current == target) return;
            if (Current != null) Current.SetHovered(false);
            Current = target;
            if (Current == null) return;
            if (!visible.Contains(Current)) visible.Add(Current);
            Current.SetHovered(true);
        }

        /// <summary>立即清除当前和旧目标，供笔记、暂停、切镜头、重开及结尾取消提示。</summary>
        public void ResetEffect()
        {
            foreach (var target in visible)
                if (target != null) target.ResetEffect();
            visible.Clear();
            Current = null;
        }

        private void LateUpdate()
        {
            // 只移除已消退或停用的目标；快速切换仍保留旧物品的短暂退场。
            for (int i = visible.Count - 1; i >= 0; i--)
                if (visible[i] == null || !visible[i].isActiveAndEnabled ||
                    (visible[i] != Current && visible[i].Opacity <= .0001f))
                    visible.RemoveAt(i);
        }

        private void OnDisable() => ResetEffect();
    }
}
