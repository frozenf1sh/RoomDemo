// =============================================================================
// 文件：WorldSceneSetup.cs
// 职责：增量拆分房间建筑，并生成具体星空样板所需的外景与技术绑定。
// 边界：不重建主场景、不改玩法 ID；只操作建筑和视觉资产，所有修改走 Editor API。
// =============================================================================
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    public static class WorldSceneSetup
    {
        /// <summary>保留世界坐标拆分建筑；窗根仍保持 Logic／Visual 包装结构。</summary>
        public static void ModularizeRoom()
        {
            var room = PresentationArtPass.Room(); var environment = room.Find("Environment");
            var architecture = PresentationArtPass.Ensure(environment, "Architecture");
            var existing = architecture.GetComponent<RoomModules>(); if (existing != null) return;
            var shell = environment.Find("RoomShell");
            Unpack(shell.gameObject);
            var floor = shell.Find("Floor"); var left = shell.Find("LeftWall"); var right = shell.Find("RightWall");
            floor.SetParent(architecture, true); left.SetParent(architecture, true); right.SetParent(architecture, true);
            var back = PresentationArtPass.Ensure(architecture, "Wall_Back");
            foreach (var name in new[] { "BackLeft", "BackRight", "BackBelow", "BackAbove" }) shell.Find(name).SetParent(back, true);
            var ceiling = environment.Find("Decoration/Ceiling"); ceiling.SetParent(architecture, true);
            var details = PresentationArtPass.Ensure(architecture, "FloorDetails");
            foreach (Transform item in environment.Find("Decoration").Cast<Transform>().ToArray())
                if (item.name.StartsWith("FloorJoint") || item.name.Contains("Skirting")) item.SetParent(details, true);
            var window = room.GetComponentInChildren<WindowPresenter>(true).transform; Unpack(window.gameObject);
            var visual = window.Find("Visual");
            var frame = PresentationArtPass.Ensure(visual, "Frame"); var glass = PresentationArtPass.Ensure(visual, "Glass");
            var exterior = PresentationArtPass.Ensure(visual, "ExteriorPortal"); var curtains = PresentationArtPass.Ensure(visual, "Curtains"); PresentationArtPass.Ensure(visual, "FX");
            foreach (var item in visual.Cast<Transform>().ToArray())
            {
                if (item.name.StartsWith("Frame") && item != frame || item.name == "Crossbar") item.SetParent(frame, true);
                if (item.name == "NearBackground") item.SetParent(glass, true);
                if (item.name == "WindowFarBackground" || item.name == "OceanBackground") item.SetParent(exterior, true);
                if (item.name.StartsWith("Curtain") && item != curtains) item.SetParent(curtains, true);
            }
            // 装饰 Prefab 已解包，几何能独立退去；实体身份和 Collider 仍保留原值。
            architecture.gameObject.AddComponent<RoomModules>().Configure(floor, ceiling, back, left, right, visual, details);
            PresentationArtPass.Save(room);
        }
        private static void Unpack(GameObject node)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(node))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(node), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }
}
