// 文件：AtelierMirrorFeature.cs
// 职责：在主镜头后处理后以真实镜面网格合成LDR反射，隔离Player镜面过曝。
// 边界：保留场景后处理和真实深度遮挡，不使用静态反射，不处理玩法事实。
using Game.Unity.TechnicalArt.RealtimePainterly.Experiments;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>只有明确启用后处理后合成的手镜参与，不影响独立TA原有路径。</summary>
    public sealed class AtelierMirrorFeature : ScriptableRendererFeature
    {
        private MirrorPass pass;
        /// <summary>本通道材质由镜面拥有，Feature只管理绘制时序。</summary>
        public override void Create() => pass = new MirrorPass();
        /// <summary>只在反射所属主镜头绘制一次，镜像摄像机不递归合成。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if (pass == null) return;
            foreach (var mirror in AtelierMirrorProbe.Active)
                if (mirror != null && mirror.CompositeAfterPost && mirror.Source == data.cameraData.camera && mirror.ReflectionTexture != null)
                {
                    pass.Mirror = mirror;
                    renderer.EnqueuePass(pass);
                    break;
                }
        }
        private sealed class MirrorPass : ScriptableRenderPass
        {
            internal AtelierMirrorProbe Mirror;
            internal MirrorPass()
            {
                // 世界已经完成曝光/Bloom，镜面在金丝和Overlay UI之前，以有限LDR颜色合成。
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering+5);
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            private sealed class Data
            {
                internal Material Material;
                internal Mesh Mesh;
                internal Matrix4x4 Matrix;
                internal TextureHandle Depth, Reflection;
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                if (Mirror == null || Mirror.TextureHandle == null) return;
                var resources = frame.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                using var builder = graph.AddRasterRenderPass<Data>("主流程：有限亮度真实镜面",out var data);
                data.Material = Mirror.CompositeMaterial;
                data.Mesh = Mirror.CompositeMesh;
                data.Matrix = Mirror.Surface.localToWorldMatrix;
                data.Depth = resources.cameraDepthTexture;
                data.Reflection = graph.ImportTexture(Mirror.TextureHandle);
                builder.UseTexture(data.Depth);
                builder.UseTexture(data.Reflection);
                builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (Data d,RasterGraphContext ctx) => {
                    ctx.cmd.SetGlobalTexture("_MirrorSceneDepth",d.Depth);
                    ctx.cmd.DrawMesh(d.Mesh,d.Matrix,d.Material,0,0);
                });
            }
        }
    }
}
