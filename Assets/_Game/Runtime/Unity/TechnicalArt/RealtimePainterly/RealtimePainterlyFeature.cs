// 文件：RealtimePainterlyFeature.cs
// 职责：将已有 Anisotropic Kuwahara 算法与世界空间笔触算法接入 URP 17 RenderGraph。
// 边界：摄像机颜色、深度与法线的表现处理；所有临时纹理由 RenderGraph 管理。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>只处理带 PainterlyCamera 的 Game 摄像机，避免影响主场景和资产预览。</summary>
    public sealed class RealtimePainterlyFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader kuwaharaShader;
        [SerializeField] private Shader worldBrushShader;
        [SerializeField] private Shader influenceCompositeShader;
        private Material kuwaharaMaterial;
        private Material worldMaterial;
        private Material influenceMaterial;
        private PainterlyPass pass;

        /// <summary>为生成器保存 Shader 资产引用，保证独立构建不会剥离隐藏 Shader。</summary>
        public void Configure(Shader kuwahara, Shader worldBrush)
        {
            kuwaharaShader = kuwahara;
            worldBrushShader = worldBrush;
            Create();
        }

        /// <summary>可选局部合成层；核心张量、取色与笔触 Shader 保持不变。</summary>
        public void ConfigureInfluence(Shader shader)
        {
            influenceCompositeShader = shader;
            Create();
        }

        /// <summary>创建实例拥有的临时材质；重新创建时先释放旧资源。</summary>
        public override void Create()
        {
            CoreUtils.Destroy(kuwaharaMaterial);
            CoreUtils.Destroy(worldMaterial);
            CoreUtils.Destroy(influenceMaterial);
            if (kuwaharaShader == null || worldBrushShader == null) return;
            kuwaharaMaterial = CoreUtils.CreateEngineMaterial(kuwaharaShader);
            worldMaterial = CoreUtils.CreateEngineMaterial(worldBrushShader);
            influenceMaterial = influenceCompositeShader != null ? CoreUtils.CreateEngineMaterial(influenceCompositeShader) : null;
            pass = new PainterlyPass(kuwaharaMaterial, worldMaterial, influenceMaterial);
        }

        /// <summary>在普通渲染之后、URP TAA 与色调映射之前排入绘画处理。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass == null || data.cameraData.cameraType != CameraType.Game) return;
            var parameters = data.cameraData.camera.GetComponent<PainterlyCamera>();
            if (parameters == null || !parameters.isActiveAndEnabled) return;
            bool local = false;
            foreach (var influence in PainterlyInfluence.Active)
                if (influence.AppliesTo(data.cameraData.camera)) { local = true; break; }
            if (parameters.PainterlyIntensity <= 0 && !local) return;
            pass.Parameters = parameters;
            renderer.EnqueuePass(pass);
        }

        /// <summary>释放 Feature 的资源，不留下隐藏材质。</summary>
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(kuwaharaMaterial);
            CoreUtils.Destroy(worldMaterial);
            CoreUtils.Destroy(influenceMaterial);
            pass = null;
        }

        /// <summary>保留六个基础绘制 Pass，按配置增加一个指定物体遮罩 Pass。</summary>
        private sealed class PainterlyPass : ScriptableRenderPass
        {
            public PainterlyCamera Parameters;
            private readonly Material kuwahara;
            private readonly Material world;
            private readonly Material influence;
            private TextureHandle objectMask;
            private readonly Vector4[] centres = new Vector4[16], radii = new Vector4[16];
            private int regionCount;
            private static readonly int TensorId = Shader.PropertyToID("_TFM");
            private static readonly int OriginalId = Shader.PropertyToID("_OriginalScene");

            /// <summary>请求正常深度／法线，并强制中间颜色纹理，避免直接读写屏幕。</summary>
            public PainterlyPass(Material kuwahara, Material world, Material influence)
            {
                this.kuwahara = kuwahara;
                this.world = world;
                this.influence = influence;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>单次绘制参数快照；避免共享材质在多摄像机录图时被后续设置覆盖。</summary>
            private sealed class DrawData
            {
                public Material Material;
                public TextureHandle Source, Tensor, Original;
                public int Index, Kernel;
                public float Anisotropy, Hardness, Sharpness, Intensity, Size, Blend, Variation, Highlight;
                public bool Debug;
                public Vector3 Light;
                public TextureHandle Mask;
                public int RegionCount;
                public Vector4[] Centres, Radii;
            }

            /// <summary>对象遮罩使用准确的 Renderer 几何，与当前摄像机的深度做只读测试。</summary>
            private sealed class MaskData
            {
                public Material Material;
                public Renderer[] Renderers;
                public float[] Amounts;
                public float[] Protected;
            }

            private void RecordInfluence(RenderGraph graph, UniversalResourceData resources, TextureDesc descriptor)
            {
                var targets = new List<Renderer>();
                var amounts = new List<float>();
                var protectedPixels = new List<float>();
                regionCount = 0;
                var camera = Parameters.GetComponent<Camera>();
                foreach (var item in PainterlyInfluence.Active)
                {
                    if (!item.AppliesTo(camera)) continue;
                    if (item.Targets != null && item.Targets.Length > 0)
                    {
                        foreach (var target in item.Targets)
                        {
                            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                            targets.Add(target);
                            amounts.Add(item.PainterlyIntensity);
                            protectedPixels.Add(item.PreserveOriginal ? 1 : 0);
                        }
                    }
                    else if (regionCount < 16)
                    {
                        var p = item.transform.position;
                        centres[regionCount] = new Vector4(p.x,p.y,p.z,item.PainterlyIntensity);
                        var r = item.Radii;
                        radii[regionCount++] = new Vector4(Mathf.Max(.001f,r.x),Mathf.Max(.001f,r.y),Mathf.Max(.001f,r.z),item.Feather);
                    }
                }
                descriptor.name = "绘画_指定物体遮罩";
                descriptor.colorFormat = GraphicsFormat.R8G8_UNorm;
                descriptor.clearBuffer = true;
                descriptor.clearColor = Color.clear;
                objectMask = graph.CreateTexture(descriptor);
                using var builder = graph.AddRasterRenderPass<MaskData>("绘画：局部物体遮罩",out var data);
                data.Material = influence;
                data.Renderers = targets.ToArray();
                data.Amounts = amounts.ToArray();
                data.Protected = protectedPixels.ToArray();
                builder.SetRenderAttachment(objectMask,0,AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (MaskData d, RasterGraphContext ctx) =>
                {
                    for (int i = 0; i < d.Renderers.Length; i++)
                    {
                        // 按绘制命令设置权重，不修改任何物体的材质或 PropertyBlock。
                        ctx.cmd.SetGlobalFloat("_InfluenceAmount",d.Amounts[i]);
                        ctx.cmd.SetGlobalFloat("_KeepOriginal",d.Protected[i]);
                        int count = d.Renderers[i].sharedMaterials.Length;
                        for (int sub = 0; sub < count; sub++) ctx.cmd.DrawRenderer(d.Renderers[i],d.Material,sub,1);
                    }
                });
            }

            /// <summary>为当前摄像机创建浮点张量和颜色缓冲，声明完整资源依赖。</summary>
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
            {
                var resources = context.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || Parameters == null) return;
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                descriptor.clearBuffer = false;
                descriptor.filterMode = FilterMode.Bilinear;
                TextureHandle Make(string label)
                {
                    descriptor.name = label;
                    return graph.CreateTexture(descriptor);
                }
                var tensor = Make("绘画_RGB结构张量");
                var horizontal = Make("绘画_张量水平平滑");
                var eigenvectors = Make("绘画_方向与各向异性");
                var patches = Make("绘画_世界空间色块");
                var filtered = Make("绘画_Kuwahara色彩整理");
                var destination = Make("绘画_世界空间笔触");

                if (influence != null) RecordInfluence(graph, resources, descriptor);

                Draw(graph, resources, "绘画：结构张量", source, tensor, 0, default, source);
                Draw(graph, resources, "绘画：张量水平平滑", tensor, horizontal, 1, default, source);
                Draw(graph, resources, "绘画：方向特征向量", horizontal, eigenvectors, 2, default, source);
                Draw(graph, resources, "绘画：世界色块与方向笔触", source, patches, 4, default, source);
                // 张量仍来自原始几何光照；方向核柔化色块，不让 Voronoi 边界反过来支配方向。
                Draw(graph, resources, "绘画：八扇区各向异性核", patches, filtered, 3, eigenvectors, source);
                Draw(graph, resources, "绘画：原图强度混合", filtered, destination, 5, default, source);
                // 后续 TAA 从绘画结果读取颜色；原始场景颜色保持独立，不形成反馈涂抹。
                resources.cameraColor = destination;
            }

            /// <summary>明确声明读取项，并在执行时绑定当前绘制快照。</summary>
            private void Draw(RenderGraph graph, UniversalResourceData resources, string label,
                TextureHandle source, TextureHandle destination, int index, TextureHandle tensor, TextureHandle original)
            {
                using var builder = graph.AddRasterRenderPass<DrawData>(label, out var data);
                var p = Parameters;
                data.Material = index == 5 && influence != null ? influence : index >= 4 ? world : kuwahara;
                data.Source = source;
                data.Tensor = tensor;
                data.Original = original;
                data.Index = index;
                data.Kernel = Mathf.Clamp(p.KernelSize, 2, 12);
                data.Anisotropy = p.Anisotropy;
                data.Hardness = p.Hardness;
                data.Sharpness = p.Sharpness;
                data.Intensity = p.PainterlyIntensity;
                data.Size = p.PatchSize;
                data.Blend = p.PatchBlend;
                data.Variation = p.PigmentVariation;
                data.Highlight = p.HighlightStrength;
                data.Debug = p.DebugWorldCells;
                data.Light = p.DirectionalLight != null ? -p.DirectionalLight.transform.forward : Vector3.up;
                if (index == 5 && influence != null)
                {
                    data.Mask = objectMask;
                    data.Centres = (Vector4[])centres.Clone();
                    data.Radii = (Vector4[])radii.Clone();
                    data.RegionCount = regionCount;
                    builder.UseTexture(objectMask);
                    builder.UseTexture(resources.cameraDepthTexture);
                }
                builder.UseTexture(source);
                if (index == 3) builder.UseTexture(tensor);
                if (index >= 4)
                {
                    builder.UseTexture(original);
                    if (index == 4) builder.UseTexture(resources.cameraNormalsTexture);
                }
                if (index == 3 || index == 4) builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                // Tensor 和原图是显式读依赖；仅在本次命令执行内设全局纹理绑定。
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (DrawData d, RasterGraphContext ctx) =>
                {
                    if (d.Index == 3)
                    {
                        ctx.cmd.SetGlobalTexture(TensorId, d.Tensor);
                        d.Material.SetInt("_KernelSize", d.Kernel);
                        d.Material.SetInt("_N", 8);
                        d.Material.SetFloat("_Alpha", d.Anisotropy);
                        d.Material.SetFloat("_Hardness", d.Hardness);
                        d.Material.SetFloat("_Q", d.Sharpness);
                        d.Material.SetFloat("_ZeroCrossing", 0.58f);
                        d.Material.SetFloat("_Zeta", 1f / (d.Kernel / 2));
                    }
                    else if (d.Index >= 4)
                    {
                        ctx.cmd.SetGlobalTexture(OriginalId, d.Original);
                        d.Material.SetFloat("_PainterlyIntensity", d.Intensity);
                        d.Material.SetFloat("_PatchSize", d.Size);
                        d.Material.SetFloat("_PatchBlend", d.Blend);
                        d.Material.SetFloat("_PigmentVariation", d.Variation);
                        d.Material.SetFloat("_HighlightStrength", d.Highlight);
                        d.Material.SetFloat("_DebugWorldCells", d.Debug ? 1 : 0);
                        d.Material.SetVector("_BrushLightDirection", d.Light);
                        if (d.Index == 5 && d.Centres != null)
                        {
                            ctx.cmd.SetGlobalTexture("_ObjectInfluence",d.Mask);
                            d.Material.SetInt("_RegionCount",d.RegionCount);
                            d.Material.SetVectorArray("_RegionCentres",d.Centres);
                            d.Material.SetVectorArray("_RegionRadii",d.Radii);
                        }
                    }
                    int shaderPass = d.Index >= 4 ? d.Index-4 : d.Index;
                    if (d.Index == 5 && d.Centres != null) shaderPass = 0;
                    Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1,1,0,0), d.Material, shaderPass);
                });
            }
        }
    }
}
