// =============================================================================
// 文件：HeroArtPass.cs
// 职责：增量绑定主物体专用材质与音频，不重建房间。
// 边界：只修改表现资源；玩法实体、规则与收集条件完全保持不变。
// =============================================================================
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>具体主物体演出资源的编辑器配置入口。</summary>
    public static class HeroArtPass
    {
        /// <summary>将画布绑定到专用连续海面 Shader，并调整空间音源。</summary>
        [MenuItem("游戏/美术/绑定连续海画")]
        public static void Painting()
        {
            var root = PresentationArtPass.Room();
            var material = SurfaceArtPass.Material("Effects", "M_PaintingSea", Color.white);
            material.shader = Shader.Find("RoomDemo/PaintingSea");
            material.SetFloat("_CanvasMode", 1); material.SetFloat("_Transition", 0);
            var painting = root.GetComponentInChildren<PaintingPresenter>(true);
            painting.SeaPainting.GetComponentInChildren<Renderer>(true).sharedMaterial = material;
            painting.SeaAudio.spatialBlend = 1; painting.SeaAudio.minDistance = 2; painting.SeaAudio.maxDistance = 12;
            painting.SeaAudio.rolloffMode = AudioRolloffMode.Linear;
            PresentationArtPass.Save(root);
        }
    }
}
