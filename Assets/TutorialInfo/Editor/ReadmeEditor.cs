// =============================================================================
// 文件：ReadmeEditor.cs
// 职责：绘制模板说明页并提供说明资产清理入口。
// 边界：中文界面改动不改变原有确认与清理流程。
// =============================================================================
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.UIElements;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
/// <summary>绘制模板说明页并提供说明资产清理入口。</summary>
sealed class ReadmeEditor : Editor
{
    static readonly string k_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";
    static readonly string k_ReadmeSourceDirectory = "Assets/TutorialInfo";

    static ReadmeEditor()
        => EditorApplication.delayCall += SelectReadmeAutomatically;

    /// <summary>每个 Editor 会话至多自动选择一次说明页。</summary>
    static void SelectReadmeAutomatically()
    {
        // 仅控制说明页首次选择，不与游戏状态共享任何全局数据。
        if (!SessionState.GetBool(k_ShowedReadmeSessionStateName, false))
        {
            var readme = SelectReadme();
            SessionState.SetBool(k_ShowedReadmeSessionStateName, true);

            if (readme && !readme.loadedLayout)
            {
                EditorUtility.LoadWindowLayout(Path.Combine(Application.dataPath, "TutorialInfo/Layout.wlt"));
                readme.loadedLayout = true;
            }
        }
    }

    /// <summary>寻找唯一说明资产并在 Inspector 中选中。</summary>
    static Readme SelectReadme()
    {
        var ids = AssetDatabase.FindAssets("Readme t:Readme");
        if (ids.Length != 1)
        {
            Debug.Log("未找到唯一的项目说明资产。");
            return null;
        }

        var readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));
        Selection.objects = new UnityEngine.Object[] { readmeObject };
        return (Readme)readmeObject;
    }
    
    /// <summary>保留确认对话框后再清理模板说明资产。</summary>
    void RemoveTutorial()
    {
        // 删除模板资源仍保留原有确认步骤，本次翻译不会自动触发删除。
        if (EditorUtility.DisplayDialog("移除说明资源",
            
            $"将移除 {k_ReadmeSourceDirectory} 下的全部说明资源。确认继续吗？",
            "继续",
            "取消"))
        {
            if (Directory.Exists(k_ReadmeSourceDirectory))
            {
                FileUtil.DeleteFileOrDirectory(k_ReadmeSourceDirectory);
                FileUtil.DeleteFileOrDirectory(k_ReadmeSourceDirectory + ".meta");
            }
            else
            {
                Debug.Log($"未找到说明目录：{k_ReadmeSourceDirectory}");
            }

            var readmeAsset = SelectReadme();
            if (readmeAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(readmeAsset);
                FileUtil.DeleteFileOrDirectory(path + ".meta");
                FileUtil.DeleteFileOrDirectory(path);
            }

            AssetDatabase.Refresh();
        }
    }

    //Remove ImGUI
    /// <summary>隐藏模板说明的默认 Inspector 标题。</summary>
    protected sealed override void OnHeaderGUI() { }
    /// <summary>禁用默认 IMGUI 内容，改用下面的 UI Toolkit 视图。</summary>
    public sealed override void OnInspectorGUI() { }

    /// <summary>以中文标题、正文、链接与按钮绘制说明面板。</summary>
    public override VisualElement CreateInspectorGUI()
    {
        var readme = (Readme)target;

        VisualElement root = new();
        root.styleSheets.Add(readme.commonStyle);
        root.styleSheets.Add(EditorGUIUtility.isProSkin ? readme.darkStyle : readme.lightStyle);

        VisualElement ChainWithClass(VisualElement created, string className)
        {
            created.AddToClassList(className);
            return created;
        }

        //Header
        VisualElement title = new();
        title.AddToClassList("title");
        title.Add(ChainWithClass(new Image() { image = readme.icon }, "title__icon"));
        title.Add(ChainWithClass(new Label(readme.title), "title__text"));
        root.Add(title);

        //Content
        foreach (var section in readme.sections)
        {
            VisualElement part = new();
            part.AddToClassList("section");

            if (!string.IsNullOrEmpty(section.heading))
                part.Add(ChainWithClass(new Label(section.heading), "section__header"));

            if (!string.IsNullOrEmpty(section.text))
                part.Add(ChainWithClass(new Label(section.text), "section__body"));

            if (!string.IsNullOrEmpty(section.linkText))
            {
                var link = ChainWithClass(new Label(section.linkText), "section__link");
                link.RegisterCallback<ClickEvent>(evt => Application.OpenURL(section.url));
                part.Add(link);
            }

            root.Add(part);
        }

        var button = new Button(RemoveTutorial) { text = "移除说明资源" };
        button.AddToClassList("remove-readme-button");
        root.Add(button);

        return root;
    }
}
