// 文件：HoverGoldFeature.cs
// 职责：把真实外轮廓旁的三缕疏松游丝合成为最上层世界提示，保留渐变尾部和大片空段。
// 边界：仅响应显式的悬停摄像机，不改原材质或游戏事实；许可由玩法输入决定。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>只在外侧局部绘制游丝；不绘制基础内圈或孔洞描边，也不封闭成整圈。</summary>
    public sealed class HoverGoldFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader effectShader;
        [Range(1, 5)] public float WidthAt1080 = 3;
        [Range(.1f, 1)] public float Strength = .9f;
        public Color Gold = new(.97f, .68f, .22f, 1);
        [Range(4, 14)] public float BraidOffsetAt1080 = 8;
        [Range(1, 12)] public float BraidAmplitudeAt1080 = 7;
        /// <summary>1080p 下沿每缕固定曲线的前进速度，单位为屏幕像素／秒。</summary>
        [Range(4, 48)] public float FlowSpeedAt1080 = 22;
        /// <summary>三段金丝本身沿闭合轨迹顺时针绕行一周的秒数；与线内流光分别控制。</summary>
        [Min(20)] public float StrandLapSeconds = 64;
        [Range(0, .35f)] public float BreathAmount = .16f;
        [Min(2)] public float BreathPeriodSeconds = 4.8f;
        private readonly Dictionary<(Camera, HoverGoldTarget), HoverGoldContour> contours = new();
        private readonly List<(Camera, HoverGoldTarget)> stale = new();
        private Material material;
        private BraidPass pass;

        /// <summary>保存 Shader 资产引用，供独立预览构建收集。</summary>
        public void Configure(Shader shader)
        {
            effectShader = shader;
            Create();
        }

        /// <summary>释放旧缓存后建立本 Feature 独占的材质。</summary>
        public override void Create()
        {
            ReleaseOwnedResources();
            if (effectShader == null) return;
            material = CoreUtils.CreateEngineMaterial(effectShader);
            pass = new BraidPass(material, this);
        }

        /// <summary>预热并缓存几何轮廓；仅在有悬停或消退时排入游丝。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass == null || data.cameraData.cameraType != CameraType.Game) return;
            var camera = data.cameraData.camera;
            // 场景重开会销毁旧镜头与目标；主动释放其网格，不能跨会话保留悬停资源。
            stale.Clear();
            foreach (var pair in contours)
                if (pair.Key.Item1 == null || pair.Key.Item2 == null || !pair.Key.Item2.isActiveAndEnabled)
                {
                    pair.Value.Dispose();
                    stale.Add(pair.Key);
                }
            foreach (var key in stale) contours.Remove(key);
            var hover = camera.GetComponent<HoverGoldCamera>();
            if (hover == null || !hover.isActiveAndEnabled) return;
            pass.Items.Clear();
            foreach (var target in hover.Visible)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                bool current = hover.Current == target;
                if (!current && target.Opacity <= .0001f) continue;
                var key = (camera, target);
                if (!contours.TryGetValue(key, out var contour))
                    contours[key] = contour = new HoverGoldContour();
                contour.UpdateSource(camera, target, material);
                if (target.Opacity <= .0001f || contour.OuterLoopCount == 0) continue;
                contour.BuildStroke(camera, this);
                pass.Items.Add(new StrokeItem { Mesh = contour.StrokeMesh, Opacity = target.Opacity });
            }
            if (pass.Items.Count > 0) renderer.EnqueuePass(pass);
        }

        /// <summary>释放材质和各物品的异步采集／网格资源。</summary>
        protected override void Dispose(bool disposing) => ReleaseOwnedResources();

        private void ReleaseOwnedResources()
        {
            foreach (var contour in contours.Values) contour.Dispose();
            contours.Clear();
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        private struct StrokeItem
        {
            internal Mesh Mesh;
            internal float Opacity;
        }

        private sealed class BraidPass : ScriptableRenderPass
        {
            internal readonly List<StrokeItem> Items = new();
            private readonly Material material;
            private readonly HoverGoldFeature style;

            internal BraidPass(Material value, HoverGoldFeature settings)
            {
                material = value;
                style = settings;
                // 在主管线最终输出之后合成；不读取世界深度，不让物品自身截断光线。
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRendering + 10);
                requiresIntermediateTexture = true;
            }

            private sealed class StrokeData
            {
                internal Material Material;
                internal StrokeItem[] Items;
                internal float Width, Strength, FlowDistance, TravelPhase;
            }

            private sealed class CompositeData
            {
                internal Material Material;
                internal TextureHandle Colour;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var desc = new TextureDesc(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height)
                {
                    name = "悬停编织光_清晰线条",
                    colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point
                };
                var colour = graph.CreateTexture(desc);
                var depthDesc = desc;
                depthDesc.name = "悬停编织光_仅三股线的穿插深度";
                depthDesc.colorFormat = GraphicsFormat.None;
                depthDesc.depthBufferBits = DepthBits.Depth24;
                var depth = graph.CreateTexture(depthDesc);
                using (var builder = graph.AddRasterRenderPass<StrokeData>("悬停编织光：三股上下穿插", out var data))
                {
                    data.Material = material;
                    data.Items = Items.ToArray();
                    data.Width = style.WidthAt1080 * Mathf.Clamp(camera.camera.pixelHeight / 1080f, .65f, 1.5f);
                    float breath = 1 - style.BreathAmount * (.5f - .5f * Mathf.Cos(Time.time * Mathf.PI * 2 / Mathf.Max(2, style.BreathPeriodSeconds)));
                    data.Strength = style.Strength * breath;
                    // 整段金丝及线内明暗沿各自弧长前进；暂停时两种运动与呼吸一起停下。
                    data.FlowDistance = Time.time * style.FlowSpeedAt1080 * Mathf.Clamp(camera.camera.pixelHeight / 1080f, .65f, 1.5f);
                    data.TravelPhase = Mathf.Repeat(Time.time / Mathf.Max(20, style.StrandLapSeconds), 1);
                    builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (StrokeData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalFloat("_BraidHalfWidth", d.Width * .5f);
                        ctx.cmd.SetGlobalFloat("_BraidFlowDistance", d.FlowDistance);
                        ctx.cmd.SetGlobalFloat("_BraidTravelPhase", d.TravelPhase);
                        foreach (var item in d.Items)
                        {
                            ctx.cmd.SetGlobalFloat("_BraidOpacity", item.Opacity * d.Strength);
                            ctx.cmd.DrawMesh(item.Mesh, Matrix4x4.identity, d.Material, 0, 1);
                        }
                    });
                }
                using (var builder = graph.AddRasterRenderPass<CompositeData>("悬停编织光：最上层合成", out var data))
                {
                    data.Material = material;
                    data.Colour = colour;
                    builder.UseTexture(colour);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture("_HoverBraid", d.Colour);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.Material, 2);
                    });
                }
            }
        }
    }
}
