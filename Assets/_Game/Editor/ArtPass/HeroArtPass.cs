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
        /// <summary>配置局部湿痕与第一滴水的单独音源，不添加游戏规则。</summary>
        [MenuItem("游戏/美术/绑定滴水与湿痕")]
        public static void Water()
        {
            var root = PresentationArtPass.Room();
            var water = root.GetComponentInChildren<WaterPresenter>(true);
            var wet = SurfaceArtPass.Material("Effects", "M_WetSurface", new Color(.12f, .20f, .19f, .72f), .94f);
            wet.SetFloat("_Surface", 1); wet.SetFloat("_Blend", 0); wet.SetFloat("_SrcBlend", 5); wet.SetFloat("_DstBlend", 10);
            wet.SetFloat("_ZWrite", 0); wet.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); wet.renderQueue = 3000;
            wet.SetFloat("_Metallic", .25f);
            water.Puddle.GetComponentInChildren<Renderer>(true).sharedMaterial = wet;
            // 从画框向盆栽方向逐渐扩散的细湿痕，加强两者之间的空间因果。
            for (int i = 0; i < 6; i++)
            {
                var patch = water.Puddle.transform.Find("Seepage" + i);
                if (patch == null) { patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform; patch.name = "Seepage" + i; patch.SetParent(water.Puddle.transform, false); Object.DestroyImmediate(patch.GetComponent<Collider>()); }
                patch.localPosition = new Vector3(-.4f - i * .48f, .023f, -.10f + i * .12f);
                patch.localScale = new Vector3(.65f, .002f, .20f); patch.localRotation = Quaternion.Euler(0, -12, 0);
                patch.GetComponent<Renderer>().sharedMaterial = wet;
            }
            var drop = AudioArtPass.Source(water.transform, "Drop", false, .7f);
            water.ConfigureDropAudio(drop);
            var audio = water.transform.Find("WaterAudio").GetComponent<AudioSource>();
            audio.clip = AudioArtPass.Clip("Water", true); audio.spatialBlend = 1; audio.minDistance = 2.5f; audio.maxDistance = 15;
            PresentationArtPass.Save(root);
        }
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
