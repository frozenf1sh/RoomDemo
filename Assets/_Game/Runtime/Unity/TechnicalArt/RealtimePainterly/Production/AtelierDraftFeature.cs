// 文件：AtelierDraftFeature.cs
// 职责：在现有画室管线中，对当前验证镜头进行局部炭稿合成。
// 边界：只消费视觉组件的视觉参数；保留原有Renderer通道、不写玩法事实。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>复用实际深度与法线，窗口遮挡跟随三维窗洞，不贴静态屏幕截图。</summary>
    public sealed class AtelierDraftFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        private Material material;
        private DraftPass pass;

        /// <summary>生成器提供明确Shader引用，独立演示构建也可收集资源。</summary>
        public void Configure(Shader value) { shader = value; Create(); }

        /// <summary>每次重建先释放本Feature拥有的材质。</summary>
        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new DraftPass(material);
        }

        /// <summary>只处理带视觉组件的主镜头，镜面和天空源镜头不进入该通道。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass == null || data.cameraData.cameraType != CameraType.Game) return;
            var probe = data.cameraData.camera.GetComponent<AtelierDraftCamera>();
            if (probe == null || !probe.isActiveAndEnabled) return;
            pass.Probe = probe;
            renderer.EnqueuePass(pass);
        }

        /// <summary>退出或重建时释放自身资源，不销毁共享正式资源。</summary>
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); pass = null; }

        private sealed class DraftPass : ScriptableRenderPass
        {
            internal AtelierDraftCamera Probe;
            private readonly Material material;

            internal DraftPass(Material value)
            {
                material = value;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            private sealed class Data
            {
                internal Material Material;
                internal TextureHandle Source, Depth, Normals;
                internal Vector4 Amounts;
                internal float Density;
            }

            /// <summary>读写使用不同颜色纹理；每次绘制冻结本镜头参数。</summary>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || Probe == null) return;
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid()) return;
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "TA验证_局部未完成炭稿";
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                var output = graph.CreateTexture(descriptor);
                using var builder = graph.AddRasterRenderPass<Data>("TA验证：局部底稿与显色", out var data);
                data.Material = material;
                data.Source = source;
                data.Depth = resources.cameraDepthTexture;
                data.Normals = resources.cameraNormalsTexture;
                data.Amounts = new Vector4(Probe.LeftDraft, Probe.WindowDraft, Probe.Ghost, 0);
                data.Density = Probe.Density;
                builder.UseTexture(data.Source);
                builder.UseTexture(data.Depth);
                builder.UseTexture(data.Normals);
                builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (Data d, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture("_ProbeDepth", d.Depth);
                    context.cmd.SetGlobalTexture("_ProbeNormals", d.Normals);
                    d.Material.SetVector("_DraftAmounts", d.Amounts);
                    d.Material.SetFloat("_DraftDensity", d.Density);
                    Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 0);
                });
                resources.cameraColor = output;
            }
        }
    }
}
