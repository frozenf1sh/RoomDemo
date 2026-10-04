// =============================================================================
// 文件：NotebookArtPass.cs
// 职责：把现有笔记本整理成纸页、墨线与剪贴风格，补演出调试面板。
// 边界：只配置 UI 视觉与声音；不在编辑器 UI 工具中定义配方或反应。
// =============================================================================
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>对已保存的笔记本进行增量视觉调整，不重建玩法界面绑定。</summary>
    public static class NotebookArtPass
    {
        private static readonly Color Ink = new Color(.25f, .21f, .16f);
        private static readonly Color Paper = new Color(.91f, .86f, .73f);
        /// <summary>应用纸页样式并保存当前房间。</summary>
        [MenuItem("游戏/美术/整理笔记本与演出调试")]
        public static void Apply() { Install(PresentationArtPass.Room()); }
        /// <summary>给指定房间配置纸面反馈与显式演出调试，不修改领域内容。</summary>
        public static void Install(Transform root)
        {
            var notebook = root.GetComponentInChildren<NotebookView>(true);
            var shade = notebook.transform.Find("NotebookPanel");
            var body = shade.Find("Notebook").GetComponent<RectTransform>();
            var group = shade.GetComponent<CanvasGroup>(); if (group == null) group = shade.gameObject.AddComponent<CanvasGroup>();
            body.GetComponent<Image>().sprite = PaperSprite(); body.GetComponent<Image>().color = Paper;
            shade.GetComponent<Image>().color = new Color(.07f, .055f, .04f, .78f);
            var backing = Rect("BookBacking", shade, new Vector2(.14f, .145f), new Vector2(.86f, .895f));
            Image(backing, new Color(.20f, .13f, .08f)); backing.SetAsFirstSibling();
            var crease = Rect("PaperCrease", body, new Vector2(.497f, .16f), new Vector2(.500f, .82f));
            Image(crease, new Color(.42f, .33f, .21f, .18f));
            body.Find("Title").GetComponent<Text>().text = "未完成的笔记";
            body.Find("ObjectsHeading").GetComponent<Text>().text = "看见的事物";
            body.Find("ConceptsHeading").GetComponent<Text>().text = "留下的意义";
            foreach (var button in notebook.GetComponentsInChildren<Button>(true))
            {
                // 使用纸片和墨色，不引入现代应用式蓝色按钮与成功勾。
                button.GetComponent<Image>().color = new Color(.84f, .77f, .63f, .45f);
                var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .94f, .78f);
                colors.selectedColor = new Color(.88f, .73f, .46f); colors.pressedColor = new Color(.72f, .60f, .42f); colors.fadeDuration = .12f; button.colors = colors;
                button.GetComponentInChildren<Text>(true).color = Ink;
                var line = Rect("PencilUnderline", button.transform, new Vector2(.03f, .02f), new Vector2(.97f, .035f)); Image(line, new Color(.35f, .27f, .15f, .3f));
            }
            foreach (var text in body.GetComponentsInChildren<Text>(true)) text.color = Ink;
            // 四个物体条目要放进现有区域；不让最后一行挤进组合区。
            foreach (var layout in new[] { body.Find("Objects").GetComponent<VerticalLayoutGroup>(), body.Find("Concepts").GetComponent<VerticalLayoutGroup>() }) layout.spacing = 4;
            notebook.transform.Find("RowTemplate").GetComponent<LayoutElement>().preferredHeight = 36;
            var relation = Rect("InkRelation", body, new Vector2(.20f, .27f), new Vector2(.80f, .274f));
            var relationImage = Image(relation, new Color(.40f, .27f, .10f)); relationImage.raycastTarget = false;
            var paperAudio = AudioArtPass.Source(notebook.transform, "Paper", false, .3f, false);
            var inkAudio = AudioArtPass.Source(notebook.transform, "Ink", false, .3f, false);
            var scratchAudio = AudioArtPass.Source(notebook.transform, "Scratch", false, .18f, false);
            notebook.ConfigurePresentation(body, group, relationImage, paperAudio, inkAudio, scratchAudio);
            var hud = root.Find("UI/DebugCanvas");
            var header = hud.Find("Header").GetComponent<RectTransform>(); header.offsetMin = new Vector2(0, -68);
            header.Find("Title").GetComponent<Text>().fontSize = 18; header.Find("Objective").GetComponent<Text>().fontSize = 14;
            header.GetComponent<Image>().color = new Color(.09f, .075f, .055f, .55f);
            hud.Find("Footer").GetComponent<Image>().color = new Color(.09f, .075f, .055f, .55f);
            hud.Find("Footer/Controls").GetComponent<Text>().text = "左键观察 · 右键转头 · N 笔记本 · H 提示 · F3 领域 · F4 演出 · F6 海之前 · F8 窗口评审";
            foreach (var name in new[] { "OpenNotebook", "Restart" })
            {
                var button = header.Find(name).GetComponent<Button>();
                var rect = button.GetComponent<RectTransform>(); rect.offsetMin = new Vector2(rect.offsetMin.x, 12); rect.offsetMax = new Vector2(rect.offsetMax.x, -12);
                button.GetComponent<Image>().color = new Color(.31f, .24f, .15f, .92f);
                button.GetComponentInChildren<Text>().color = Paper;
            }
            var discovery = hud.Find("DiscoveryNotice"); discovery.GetComponent<Image>().sprite = body.GetComponent<Image>().sprite;
            discovery.GetComponent<Image>().color = new Color(.95f, .87f, .68f, .98f);
            discovery.GetComponentInChildren<Text>(true).color = Ink;
            Tools(root, hud);
            PresentationArtPass.Save(root);
        }
        /// <summary>创建原创纸张纹理，进口为 Sprite；不依赖开发机图片或额外字体。</summary>
        private static Sprite PaperSprite()
        {
            const string path = "Assets/_Game/Content/Art/Textures/NotebookPaper.png";
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float grain = .955f + Mathf.PerlinNoise(x * .25f, y * .25f) * .035f;
                texture.SetPixel(x, y, new Color(grain, grain, grain));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        /// <summary>演出面板与领域面板分开，只预览表现，不修改事实。</summary>
        private static void Tools(Transform root, Transform hud)
        {
            var panel = Rect("PresentationTools", hud, new Vector2(0, .15f), new Vector2(.29f, .88f));
            panel.offsetMin = new Vector2(15, 0); panel.offsetMax = new Vector2(-10, 0); Image(panel, new Color(.09f, .075f, .055f, .96f)).raycastTarget = true;
            Label("Title", panel, "演出调试 · F4\nF8：窗口化评审", new Vector2(.05f, .87f), new Vector2(.95f, .98f));
            var status = Label("Status", panel, "静止", new Vector2(.05f, .66f), new Vector2(.95f, .86f));
            string[] names = { "单独播放海画", "单独播放滴水", "单独播放水生植物", "单独播放海窗", "播放完整演出预览", "重置为演出前视觉", "预览最终效果", "恢复当前领域事实" };
            var buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var rect = Rect("PresentationAction" + i, panel, new Vector2(.05f, .58f - i * .072f), new Vector2(.95f, .645f - i * .072f));
                var image = Image(rect, new Color(.30f, .24f, .15f)); var button = rect.GetComponent<Button>(); if (button == null) button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image; image.raycastTarget = true; Label("Text", rect, names[i], Vector2.zero, Vector2.one).alignment = TextAnchor.MiddleCenter; buttons[i] = button;
            }
            var debug = hud.GetComponent<PresentationDebugView>(); if (debug == null) debug = hud.gameObject.AddComponent<PresentationDebugView>();
            debug.Configure(root.GetComponentInChildren<PaintingSeaSequence>(true), panel.gameObject, status, buttons); panel.gameObject.SetActive(false);
        }
        /// <summary>创建或复用 UI 矩形；不改已有面板的玩法事件绑定。</summary>
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var found = parent.Find(name); var rect = found != null ? (RectTransform)found : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (found == null) rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        /// <summary>复用背景图片，所有装饰线均不阻挡点击。</summary>
        private static Image Image(RectTransform rect, Color color)
        {
            var image = rect.GetComponent<Image>(); if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        /// <summary>创建开发面板文字，统一使用项目嵌入中文字体。</summary>
        private static Text Label(string name, Transform parent, string value, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max); var text = rect.GetComponent<Text>(); if (text == null) text = rect.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath); text.fontSize = 14; text.color = Paper; text.text = value; text.raycastTarget = false;
            return text;
        }
    }
}
