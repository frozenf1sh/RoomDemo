// 文件：FixedViewSkyFeature.cs
// 职责：把已完成原版绘画处理的画幅合成到主摄像机没有几何的区域。
// 边界：不替换主管线或 UI；只处理显式挂有 FixedViewSky 的摄像机。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>色调映射之后合成，避免原 Hero 的 ACES 被主房间再处理一遍。</summary>
    public sealed class FixedViewSkyFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader compositeShader;
        private Material material;
        private SkyPass pass;

        /// <summary>保存明确 Shader 引用，供构建收集依赖。</summary>
        public void Configure(Shader shader) { compositeShader = shader; Create(); }
        /// <summary>创建本 Feature 拥有的材质。</summary>
        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (compositeShader == null) return;
            material = CoreUtils.CreateEngineMaterial(compositeShader);
            pass = new SkyPass(material);
        }
        /// <summary>仅在目标摄像机显示星夜时排入合成。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass == null || data.cameraData.cameraType != CameraType.Game) return;
            var sky = data.cameraData.camera.GetComponent<FixedViewSky>();
            if (sky == null || !sky.isActiveAndEnabled || sky.Picture == null || sky.Opacity <= .0001f) return;
            pass.Sky = sky;
            renderer.EnqueuePass(pass);
        }
        /// <summary>释放内部材质。</summary>
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); pass = null; }

        private sealed class SkyPass : ScriptableRenderPass
        {
            internal FixedViewSky Sky;
            private readonly Material material;
            internal SkyPass(Material value)
            {
                material = value;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            private sealed class Data
            {
                internal Material Material;
                internal TextureHandle Picture, Depth;
                internal float Opacity;
                internal Vector4 ScaleBias;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                using var builder = graph.AddRasterRenderPass<Data>("固定星夜：原版画幅深度合成", out var data);
                data.Material = material;
                var texture = Sky.Texture;
                var colourView = new RenderTargetInfo
                {
                    width = texture.width, height = texture.height, volumeDepth = 1,
                    msaaSamples = 1, format = texture.graphicsFormat
                };
                data.Picture = graph.ImportTexture(Sky.Picture, colourView);
                data.Depth = resources.cameraDepthTexture;
                data.Opacity = Sky.Opacity;
                data.ScaleBias = Sky.FrameScaleBias;
                builder.UseTexture(data.Picture);
                builder.UseTexture(data.Depth);
                // 只写深度空白像素；硬件混合保留其他颜色，不读取和回写同一颜色纹理。
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (Data d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture("_FixedSkyDepth", d.Depth);
                    ctx.cmd.SetGlobalTexture("_FixedSkyTexture", d.Picture);
                    d.Material.SetFloat("_Opacity", d.Opacity);
                    d.Material.SetVector("_FrameScaleBias", d.ScaleBias);
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.Material, 0);
                });
            }
        }
    }
}
