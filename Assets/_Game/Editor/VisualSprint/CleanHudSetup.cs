// =============================================================================
// 文件：CleanHudSetup.cs
// 职责：增量安装第五阶段的干净玩家 HUD 与纸墨风格暂停菜单。
// 边界：仅调整 UI 节点与绑定，不重建场景、不删除开发工具或改变玩法规则。
// =============================================================================
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor.VisualSprint
{
    /// <summary>将旧常驻界面转为暂时提示，并保留 N 和开发快捷键入口。</summary>
    public static class CleanHudSetup
    {
        /// <summary>重复执行替换自己的样式与绑定，不覆盖艺术场景。</summary>
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var hud = root.Find("UI/DebugCanvas");
            var view = hud.GetComponent<PlayerHudView>();
            var notebook = root.GetComponentInChildren<NotebookView>(true);
            var game = root.GetComponentInChildren<GameBootstrap>(true);
            var days = root.GetComponentInChildren<DayTransitionController>(true);
            // 旧控件仍作为调试与回归引用存在，但不在正式画面显示。
            foreach (string name in new[] { "Header/Title", "Header/Objective", "Header/Restart", "Footer", "Observation", "DayControls" })
                hud.Find(name).gameObject.SetActive(false);
            foreach (string name in new[] { "Header", "Footer" })
            { var image = hud.Find(name).GetComponent<Image>(); if (image != null) image.enabled = false; }
            var legacyReminder = hud.Find("Header/OpenNotebook");
            if (legacyReminder != null) legacyReminder.gameObject.SetActive(false);
            // Prefab 的既有子节点不能跨父级移动，因此新建小提醒并显式替换按钮绑定。
            var reminder = ExplorationSceneSetup.Button("OpenNotebook", hud, "笔记 · N", new Vector2(.915f,.025f), new Vector2(.984f,.067f));
            var notebookData = new SerializedObject(notebook);
            notebook.ConfigureControls(reminder, (Button)notebookData.FindProperty("closeButton").objectReferenceValue);
            reminder.GetComponent<Image>().color = new Color(.32f, .26f, .17f, .12f);
            reminder.GetComponentInChildren<Text>().fontSize = 16;
            reminder.gameObject.SetActive(false);
            var notice = hud.Find("DiscoveryNotice");
            notice.GetComponent<Image>().color = new Color(.14f, .105f, .07f, .78f);
            var noticeText = notice.GetComponentInChildren<Text>(); noticeText.fontSize = 17;
            noticeText.color = new Color(.95f, .87f, .71f); noticeText.alignment = TextAnchor.MiddleLeft;
            var hover = hud.Find("Hover").GetComponent<Text>(); hover.fontSize = 16;
            Rect(hover.transform, .42f, .44f, .58f, .48f); hover.alignment = TextAnchor.MiddleCenter;
            var tutorial = ExplorationSceneSetup.Label("FirstLookTip", hud, "拖动鼠标观察 · N 翻开笔记", new Vector2(.28f, .05f), new Vector2(.72f, .10f), 15);
            tutorial.alignment = TextAnchor.MiddleCenter;
            var pause = ExplorationSceneSetup.Rect("PauseMenu", hud, Vector2.zero, Vector2.one);
            var background = pause.GetComponent<Image>(); if (background == null) background = pause.gameObject.AddComponent<Image>();
            background.color = new Color(.035f, .025f, .016f, .87f);
            var heading = ExplorationSceneSetup.Label("Title", pause, "暂歇", new Vector2(.39f, .64f), new Vector2(.61f, .74f), 32); heading.alignment = TextAnchor.MiddleCenter;
            var resume = QuietButton("Resume", pause, "回到房间", .51f);
            var endDay = QuietButton("EndToday", pause, "结束今天", .41f);
            var restart = QuietButton("Restart", pause, "重新开始", .31f);
            view.ConfigureCleanHud(game, days, pause.gameObject, resume, restart, endDay, reminder, tutorial, hover);
            pause.gameObject.SetActive(false);
            // 日序只在黑屏过渡中央出现，原常驻标签继续隐藏。
            var transition = root.Find("UI/DayCanvas/DayCurtain");
            if (transition != null) foreach (var text in transition.GetComponentsInChildren<Text>(true)) { text.fontSize = 34; text.color = new Color(.93f,.88f,.78f); }
            PresentationArtPass.Save(root);
        }
        private static Button QuietButton(string name, Transform parent, string text, float y)
        {
            var b = ExplorationSceneSetup.Button(name, parent, text, new Vector2(.4f, y), new Vector2(.6f, y + .063f));
            b.GetComponent<Image>().color = new Color(.27f, .22f, .16f, .28f);
            b.GetComponentInChildren<Text>().fontSize = 20; b.GetComponentInChildren<Text>().color = new Color(.92f,.86f,.74f);
            return b;
        }
        private static void Rect(Transform node, float x0, float y0, float x1, float y1)
        {
            var rect = (RectTransform)node; rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
