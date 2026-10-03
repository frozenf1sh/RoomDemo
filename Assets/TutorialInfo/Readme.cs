// =============================================================================
// 文件：Readme.cs
// 职责：保存 Unity 模板说明页的可编辑数据。
// 边界：作为 Editor 说明资产，不参与游戏规则；显示内容统一为中文。
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>保存 Unity 模板说明页的可编辑数据。</summary>
public class Readme : ScriptableObject
{
    public StyleSheet commonStyle;
    public StyleSheet darkStyle;
    public StyleSheet lightStyle;
    public Texture2D icon;
    public string title;
    // 说明页正文由资产持有，翻译通过 Editor API 更新而不改序列化结构。
    public Section[] sections;
    public bool loadedLayout;

    /// <summary>说明页中的一节中文标题、正文，以及可选的资源链接。</summary>
    [Serializable]
    public class Section
    {
        public string heading, text, linkText, url;
    }
}
