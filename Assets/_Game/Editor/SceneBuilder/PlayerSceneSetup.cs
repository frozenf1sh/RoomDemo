// =============================================================================
// 文件：PlayerSceneSetup.cs
// 职责：增量收敛正式 HUD，集中配置领域预设、世界样板与检视开发入口。
// 边界：只更新 UI 技术布局与引用，不重建人工美术，不修改领域内容和物体身份。
// =============================================================================
using System.Linq;
using Game.Unity;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>默认玩家模式与显式开发模式的场景安装器。</summary>
    public static class PlayerSceneSetup
    {
        /// <summary>保留发现通知与笔记按钮；所有开发入口集中到 F3／F4／F7。</summary>
        public static void InstallPlayerHud()
        {
            var root = PresentationArtPass.Room(); var hud = root.Find("UI/DebugCanvas");
            var game = root.GetComponentInChildren<GameBootstrap>(true);
            var inspection = root.GetComponentInChildren<InspectionController>(true);
            var debug = hud.GetComponent<DebugView>();
            var controls = hud.Find("Footer/Controls").GetComponent<Text>(); controls.fontSize = 12; debug.ConfigureControls(controls);
            var panel = hud.Find("DebugPanel");
            SetRect(panel.Find("StateViewport"), 0, .47f, 1, .93f);
            SetRect(panel.Find("Events"), 0, .36f, 1, .46f);
            SetRect(panel.Find("DebugReset"), 0, .30f, .30f, .35f); SetRect(panel.Find("GrantAll"), .30f, .30f, 1, .35f);
            SetRect(panel.Find("FactInput"), 0, .23f, .69f, .28f); SetRect(panel.Find("AddFact"), .69f, .23f, 1, .28f);
            var presets = new Button[9];
            for (int i = 0; i < presets.Length; i++)
            {
                int row = i / 2, column = i % 2; float bottom = .18f - row * .038f;
                presets[i] = ExplorationSceneSetup.Button("Preset" + i, panel, "状态预设", new Vector2(column * .5f + .025f, bottom), new Vector2((column + 1) * .5f - .025f, bottom + .034f));
                presets[i].GetComponentInChildren<Text>().fontSize = 12;
            }
            debug.ConfigureTools(hud.Find("Observation").GetComponent<Text>(), hud.Find("Header/Restart").GetComponent<Button>(),
                panel.Find("DebugReset").GetComponent<Button>(), panel.Find("GrantAll").GetComponent<Button>(), panel.Find("AddFact").GetComponent<Button>(), panel.Find("FactInput").GetComponent<InputField>(), presets);
            var body = ExplorationSceneSetup.Rect("WorldTools", hud, new Vector2(.315f, .14f), new Vector2(.655f, .90f));
            var image = body.GetComponent<Image>(); if (image == null) image = body.gameObject.AddComponent<Image>(); image.color = new Color(.09f, .075f, .055f, .97f);
            ExplorationSceneSetup.Label("Title", body, "世界／检视调试 · F7", new Vector2(.05f, .91f), new Vector2(.95f, .99f), 18);
            var status = ExplorationSceneSetup.Label("Status", body, "房间仍有边界", new Vector2(.05f, .68f), new Vector2(.95f, .89f), 14);
            string[] names = { "提交星空事实并播放", "仅预览星空演出", "重置星空表现", "恢复当前真实状态", "跳到星空之前", "直接恢复星空终态", "检视盆栽（开发）", "检视旧书（开发）" };
            var buttons = new Button[names.Length];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = ExplorationSceneSetup.Button("WorldAction" + i, body, names[i], new Vector2(.05f, .60f - i * .071f), new Vector2(.95f, .661f - i * .071f));
            var worldDebug = hud.GetComponent<WorldDebugView>(); if (worldDebug == null) worldDebug = hud.gameObject.AddComponent<WorldDebugView>();
            var items = root.GetComponentsInChildren<InspectableObject>(true);
            worldDebug.Configure(game, root.GetComponentInChildren<StarryWorldSequence>(true), inspection, items.Single(x => x.Entity.Id == Game.Domain.DemoRules.Plant), items.Single(x => x.Entity.Id == Game.Domain.DemoRules.Book), body.gameObject, status, buttons);
            // 日序控件用独立父节点控制显隐，不与跨日组件争抢按钮的 activeSelf。
            var days = ExplorationSceneSetup.Rect("DayControls", hud, Vector2.zero, Vector2.one);
            foreach (string name in new[] { "Day", "EndDay" }) { var node = hud.Find(name); if (node != null) node.SetParent(days, false); }
            var player = hud.GetComponent<PlayerHudView>(); if (player == null) player = hud.gameObject.AddComponent<PlayerHudView>();
            player.Configure(inspection, new[] { hud.Find("Header/Title").gameObject, hud.Find("Header/Objective").gameObject, hud.Find("Footer").gameObject,
                hud.Find("Observation").gameObject, hud.Find("Hover").gameObject, days.gameObject });
            var overlay = root.Find("UI/InspectionCanvas/InspectionOverlay");
            SetRect(overlay.Find("ExitInspection"), .78f, .055f, .95f, .125f);
            SetRect(overlay.Find("Hint"), .08f, .14f, .92f, .20f);
            game.ConfigureDevelopmentTools(root.GetComponentsInChildren<TextMesh>(true), hud.GetComponent<PresentationDebugView>(), worldDebug, root.GetComponentInChildren<BenchmarkReviewDriver>(true));
            game.SetDevelopmentMode(false); panel.gameObject.SetActive(false); body.gameObject.SetActive(false);
            PresentationArtPass.Save(root);
        }
        private static void SetRect(Transform node, float x0, float y0, float x1, float y1)
        {
            var rect = (RectTransform)node; rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
