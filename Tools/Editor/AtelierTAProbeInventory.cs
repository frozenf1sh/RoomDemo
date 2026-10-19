// 文件：AtelierTAProbeInventory.cs
// 职责：只读核对TA验证副本需要的机位、工作簇、表面和当前渲染器。
// 边界：不保存源场景，不修改玩法事实；结果仅放本地Logs。
using System;
using System.IO;
using System.Linq;
using Game.Unity.ArtRebuild;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>通过Editor API读取真实工作簇，避免按截图猜世界坐标。</summary>
public static class AtelierTAProbeInventory
{
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static object V(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

    /// <summary>拒绝未保存内容，打开已保存开发入口并输出只读实查。</summary>
    public static string Inspect()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("存在未保存场景，已停止TA实查。");
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Content/Scenes/AtelierPlayable.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<AtelierGameplay>();
        var renderers = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Renderer>(true));
        var report = new {
            camera = new { position = V(game.ViewCamera.transform.position), euler = V(game.ViewCamera.transform.eulerAngles), fov = game.ViewCamera.fieldOfView },
            desk = new { position = V(game.DeskFocusPose.position), euler = V(game.DeskFocusPose.eulerAngles) },
            painting = new { position = V(game.FocusPose.position), euler = V(game.FocusPose.eulerAngles) },
            water = new { path = PathOf(game.Wash.Water.transform), centre = V(game.Wash.Water.bounds.center), size = V(game.Wash.Water.bounds.size) },
            roof = game.Finale.RoofSurfaces.Select(r => new { path = PathOf(r.transform), centre = V(r.bounds.center), size = V(r.bounds.size) }).ToArray(),
            pipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
            renderers = renderers.Select(r => new { path = PathOf(r.transform), active = r.gameObject.activeInHierarchy,
                centre = V(r.bounds.center), size = V(r.bounds.size),
                materials = r.sharedMaterials.Select(m => m == null ? "空" : m.name + "|" + m.shader.name).ToArray() }).ToArray()
        };
        Directory.CreateDirectory("Logs/TAProbes20261019");
        File.WriteAllText("Logs/TAProbes20261019/场景实查.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JsonConvert.SerializeObject(new { success = true, report.camera, report.desk, report.water, report.roof, report.pipeline });
    }
}
