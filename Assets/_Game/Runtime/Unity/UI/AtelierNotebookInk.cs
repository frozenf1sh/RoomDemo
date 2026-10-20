// 文件：AtelierNotebookInk.cs
// 职责：统一四阶段笔记文字配色；笔刷按稳定身份显示为紫色。
// 边界：仅提供显示颜色，不更改碎片类别、来源或配方权限。
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>普通灵感与实际笔刷保持独立的显示身份。</summary>
    public static class AtelierNotebookInk
    {
        /// <summary>返回已有纸底上的深色墨迹；不按中文名称猜测笔刷。</summary>
        public static Color For(ClueId id,ClueCategory category)
        {
            // 四张原始笔刷的身份固定，未来物体尚未认识也不会改变显示类别。
            if(IsBrush(id))return new Color(.42f,.29f,.51f,1);
            return category==ClueCategory.Concept?new Color(.25f,.39f,.63f,1):
                category==ClueCategory.Observation?new Color(.76f,.39f,.12f,1):new Color(.20f,.30f,.26f,1);
        }
        /// <summary>按稳定身份判断四张笔刷，供左页笔刷分区使用；不按中文名猜测。</summary>
        public static bool IsBrush(ClueId id)
        {
            for(int layer=1;layer<=4;layer++)
                if(id==AtelierFlowRules.Brush(layer))return true;
            return false;
        }
    }
}
