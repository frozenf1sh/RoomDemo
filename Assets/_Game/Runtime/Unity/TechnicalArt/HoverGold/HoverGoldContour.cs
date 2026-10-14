// 文件：HoverGoldContour.cs
// 职责：缓存真实物品投影的外轮廓，并沿弧长生成三缕疏松、断续且偶尔交错的游丝。
// 边界：只读取几何与摄像机；内部孔洞不描边，GPU 读回只在投影或物品变化时执行。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>拥有单个物品／镜头组合的轮廓缓存、临时读回纹理及动态光线网格。</summary>
    internal sealed class HoverGoldContour : IDisposable
    {
        private readonly List<Vector2[]> loops = new();
        private readonly List<Vector2[]> sampledLoops = new();
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<Color> colours = new();
        private readonly List<int> indices = new();
        private RenderTexture capture;
        private byte[] pendingPixels;
        private int captureWidth, captureHeight, requestedSignature;
        private int sourceSignature = int.MinValue;
        private bool reading, disposed;
        private bool samplesDirty = true;
        private int sampledWidth, sampledHeight;
        private float retryAfter;

        /// <summary>仅在外轮廓就绪后存在；本缓存负责释放。</summary>
        public Mesh StrokeMesh { get; private set; }

        /// <summary>外轮廓数量；孔洞已在轮廓整理时排除。</summary>
        public int OuterLoopCount => loops.Count;

        /// <summary>投影不变时复用轮廓；变动时异步采集指定物品，不采集房间或光照。</summary>
        public void UpdateSource(Camera view, HoverGoldTarget target, Material maskMaterial)
        {
            if (disposed) return;
            if (pendingPixels != null)
            {
                BuildOuterLoops(pendingPixels, captureWidth, captureHeight);
                pendingPixels = null;
                sourceSignature = requestedSignature;
            }
            int signature = SourceSignature(view, target);
            if (reading || signature == sourceSignature || Time.unscaledTime < retryAfter) return;
            captureWidth = Mathf.Min(1024, Mathf.Max(64, view.pixelWidth));
            captureHeight = Mathf.Max(64, Mathf.RoundToInt(captureWidth * view.pixelHeight / (float)view.pixelWidth));
            capture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "悬停编织光_轮廓采集",
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            capture.Create();
            var commands = CommandBufferPool.Get("悬停编织光：仅采集目标几何");
            commands.SetRenderTarget(capture);
            commands.ClearRenderTarget(true, true, Color.clear);
            // 专用矩阵避免污染主管线的相机全局量；不改 Renderer 原材质。
            commands.SetGlobalMatrix("_HoverCaptureVP", GL.GetGPUProjectionMatrix(view.projectionMatrix, true) * view.worldToCameraMatrix);
            for (int i = 0; i < target.Surfaces.Length; i++)
            {
                var surface = target.Surfaces[i];
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                for (int sub = 0; sub < target.SubmeshCounts[i]; sub++)
                    commands.DrawRenderer(surface, maskMaterial, sub, 0);
            }
            Graphics.ExecuteCommandBuffer(commands);
            CommandBufferPool.Release(commands);
            reading = true;
            requestedSignature = signature;
            AsyncGPUReadback.Request(capture, 0, TextureFormat.RGBA32, request =>
            {
                reading = false;
                if (!disposed && !request.hasError)
                {
                    // 复制原始像素；轮廓整理和 Unity 网格更新留在随后主线程的渲染准备阶段。
                    var data = request.GetData<byte>();
                    pendingPixels = new byte[data.Length];
                    data.CopyTo(pendingPixels);
                }
                else if (!disposed)
                {
                    retryAfter = Time.unscaledTime + 1;
                    Debug.LogWarning("悬停编织光轮廓读回失败，将稍后重试。");
                }
                ReleaseCapture();
            });
        }

        private static int SourceSignature(Camera view, HoverGoldTarget target)
        {
            unchecked
            {
                int hash = view.worldToCameraMatrix.GetHashCode();
                hash = hash * 31 + view.projectionMatrix.GetHashCode();
                hash = hash * 31 + view.pixelWidth;
                hash = hash * 31 + view.pixelHeight;
                foreach (var surface in target.Surfaces)
                {
                    if (surface == null) continue;
                    hash = hash * 31 + surface.localToWorldMatrix.GetHashCode();
                    hash = hash * 31 + (surface.enabled && surface.gameObject.activeInHierarchy ? 1 : 0);
                }
                return hash;
            }
        }

        private static long Key(int x, int y) => ((long)(x + 2) << 32) | (uint)(y + 2);

        private static Vector2 Position(long key, int width, int height)
        {
            float x = ((int)(key >> 32) - 2) * .5f + .5f;
            float y = ((int)key - 2) * .5f + .5f;
            return new Vector2(x / width, y / height);
        }

        private static bool Filled(byte[] pixels, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            return pixels[(y * width + x) * 4] >= 128;
        }

        private static void Link(Dictionary<long, List<long>> edges, long a, long b)
        {
            if (!edges.TryGetValue(a, out var first)) edges[a] = first = new List<long>(2);
            if (!edges.TryGetValue(b, out var second)) edges[b] = second = new List<long>(2);
            first.Add(b);
            second.Add(a);
        }

        /// <summary>追踪闭合轮廓并排除被更大轮廓包围的孔洞，避免杯把内缘出现第二层。</summary>
        private void BuildOuterLoops(byte[] pixels, int width, int height)
        {
            var edges = new Dictionary<long, List<long>>();
            for (int y = -1; y < height; y++)
            {
                for (int x = -1; x < width; x++)
                {
                    int code = (Filled(pixels, x, y, width, height) ? 1 : 0) |
                        (Filled(pixels, x + 1, y, width, height) ? 2 : 0) |
                        (Filled(pixels, x + 1, y + 1, width, height) ? 4 : 0) |
                        (Filled(pixels, x, y + 1, width, height) ? 8 : 0);
                    if (code == 0 || code == 15) continue;
                    long a = Key(2 * x + 1, 2 * y);
                    long b = Key(2 * x + 2, 2 * y + 1);
                    long c = Key(2 * x + 1, 2 * y + 2);
                    long d = Key(2 * x, 2 * y + 1);
                    switch (code)
                    {
                        case 1: case 14: Link(edges, d, a); break;
                        case 2: case 13: Link(edges, a, b); break;
                        case 3: case 12: Link(edges, d, b); break;
                        case 4: case 11: Link(edges, b, c); break;
                        case 5: Link(edges, d, a); Link(edges, b, c); break;
                        case 6: case 9: Link(edges, a, c); break;
                        case 7: case 8: Link(edges, d, c); break;
                        case 10: Link(edges, a, b); Link(edges, c, d); break;
                    }
                }
            }
            var visited = new HashSet<long>();
            var candidates = new List<Vector2[]>();
            foreach (var entry in edges)
            {
                if (visited.Contains(entry.Key)) continue;
                var chain = new List<Vector2>();
                long start = entry.Key, current = start, previous = long.MinValue;
                bool closed = false;
                // 每条边最多走一次；有损坏的链也会终止，不让采集阻塞游戏循环。
                for (int count = 0; count <= edges.Count; count++)
                {
                    if (!visited.Add(current)) break;
                    chain.Add(Position(current, width, height));
                    var neighbours = edges[current];
                    long next = neighbours[0] == previous && neighbours.Count > 1 ? neighbours[1] : neighbours[0];
                    if (next == start) { closed = true; break; }
                    previous = current;
                    current = next;
                }
                if (closed && chain.Count >= 12) candidates.Add(chain.ToArray());
            }
            loops.Clear();
            foreach (var candidate in candidates)
            {
                float area = Mathf.Abs(Area(candidate));
                if (area * width * height < 6) continue;
                bool hole = false;
                foreach (var other in candidates)
                {
                    if (other == candidate || Mathf.Abs(Area(other)) <= area) continue;
                    if (Contains(other, candidate[0])) { hole = true; break; }
                }
                if (hole) continue;
                // 轻度平滑只处理二值采集的台阶，不对最终发光线做模糊。
                var smooth = candidate;
                for (int pass = 0; pass < 2; pass++)
                {
                    var next = new Vector2[smooth.Length];
                    for (int i = 0; i < smooth.Length; i++)
                        next[i] = (smooth[(i + smooth.Length - 1) % smooth.Length] + smooth[i] * 2 + smooth[(i + 1) % smooth.Length]) * .25f;
                    smooth = next;
                }
                loops.Add(smooth);
            }
            samplesDirty = true;
        }

        private static float Area(Vector2[] points)
        {
            float area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }
            return area * .5f;
        }

        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) == (b.y > point.y)) continue;
                float crossing = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (point.x < crossing) inside = !inside;
            }
            return inside;
        }

        /// <summary>沿真实轮廓弧长生成三个局部片段；保留大片空段、渐变尾部和局部穿插。</summary>
        public void BuildStroke(Camera view, HoverGoldFeature style, float time)
        {
            vertices.Clear(); uvs.Clear(); colours.Clear(); indices.Clear();
            float scale = Mathf.Clamp(view.pixelHeight / 1080f, .65f, 1.5f);
            float halfGeometry = style.WidthAt1080 * scale * .5f + 2.5f;
            // 弧长采样只在轮廓或输出尺寸变化时更新，持续悬停不重复分配采样数组。
            if (samplesDirty || sampledWidth != view.pixelWidth || sampledHeight != view.pixelHeight)
            {
                sampledLoops.Clear();
                foreach (var loop in loops) sampledLoops.Add(Resample(loop, view.pixelWidth, view.pixelHeight));
                sampledWidth = view.pixelWidth;
                sampledHeight = view.pixelHeight;
                samplesDirty = false;
            }
            foreach (var points in sampledLoops)
            {
                if (points.Length < 12) continue;
                float length = 0;
                for (int i = 0; i < points.Length; i++) length += Vector2.Distance(points[i], points[(i + 1) % points.Length]);
                float orientation = Area(points) > 0 ? 1 : -1;
                for (int strand = 0; strand < 3; strand++)
                {
                    int first = vertices.Count;
                    float travelled = 0;
                    for (int i = 0; i <= points.Length; i++)
                    {
                        int index = i % points.Length;
                        if (i > 0) travelled += Vector2.Distance(points[(i - 1) % points.Length], points[index]);
                        Vector2 tangent = (points[(index + 4) % points.Length] - points[(index + points.Length - 4) % points.Length]).normalized;
                        Vector2 outward = new Vector2(tangent.y, -tangent.x) * orientation;
                        // 三段合计最多覆盖约 55% 弧长；即使移动也不会形成封闭光圈。
                        float centrePhase = (strand == 0 ? .09f : strand == 1 ? .25f : .72f) + time * style.BraidSpeed * .012f;
                        float span = strand == 0 ? .095f : strand == 1 ? .105f : .075f;
                        float local = (Mathf.Repeat(travelled / length - centrePhase + .5f, 1) - .5f) / span;
                        float envelope = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1, Mathf.Abs(local)));
                        float phase = local * Mathf.PI * 1.15f + strand * 1.6f + time * .15f;
                        float front = Mathf.Sin(local * 2.8f + time * .12f + strand * 2.1f);
                        float offset = (style.BraidOffsetAt1080 + style.BraidAmplitudeAt1080 * Mathf.Sin(phase) + (1 - envelope) * 8) * scale;
                        Vector2 centre = points[index] + outward * offset;
                        float depth = view.nearClipPlane * (1.55f - front * .1f);
                        Color colour = Color.Lerp(style.Gold * new Color(.85f, .77f, .62f, 1), new Color(1, .9f, .55f, 1), (front + 1) * .5f).linear;
                        float bright = .3f + .7f * Mathf.Pow(.5f + .5f * Mathf.Cos(local * Mathf.PI * 1.3f - time * .65f + strand), 3);
                        colour = Color.Lerp(colour, new Color(1, .92f, .62f, 1).linear, Mathf.Pow(bright, 3) * .65f);
                        colour.a = envelope * bright;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 screen = centre + outward * (halfGeometry * side);
                            vertices.Add(view.ScreenToWorldPoint(new Vector3(screen.x + view.pixelRect.x,
                                screen.y + view.pixelRect.y, depth)));
                            uvs.Add(new Vector2(halfGeometry * side, envelope));
                            colours.Add(colour);
                        }
                        if (i == 0) continue;
                        int a = first + (i - 1) * 2;
                        indices.Add(a); indices.Add(a + 1); indices.Add(a + 2);
                        indices.Add(a + 1); indices.Add(a + 3); indices.Add(a + 2);
                    }
                }
            }
            if (StrokeMesh == null)
            {
                StrokeMesh = new Mesh { name = "悬停外侧疏松游丝", hideFlags = HideFlags.HideAndDontSave };
                StrokeMesh.MarkDynamic();
            }
            StrokeMesh.Clear();
            StrokeMesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            StrokeMesh.SetVertices(vertices);
            StrokeMesh.SetUVs(0, uvs);
            StrokeMesh.SetColors(colours);
            StrokeMesh.SetTriangles(indices, 0);
        }

        private static Vector2[] Resample(Vector2[] loop, int width, int height)
        {
            var screen = new Vector2[loop.Length];
            var cumulative = new float[loop.Length + 1];
            for (int i = 0; i < loop.Length; i++) screen[i] = Vector2.Scale(loop[i], new Vector2(width, height));
            for (int i = 1; i <= loop.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(screen[i - 1], screen[i % loop.Length]);
            float total = cumulative[loop.Length];
            int samples = Mathf.Max(12, Mathf.CeilToInt(total / 1.5f));
            var result = new Vector2[samples];
            int segment = 0;
            for (int i = 0; i < samples; i++)
            {
                float distance = total * i / samples;
                while (segment + 1 < loop.Length && cumulative[segment + 1] < distance) segment++;
                float t = (distance - cumulative[segment]) / Mathf.Max(.0001f, cumulative[segment + 1] - cumulative[segment]);
                result[i] = Vector2.Lerp(screen[segment], screen[(segment + 1) % loop.Length], t);
            }
            // 向外游离的曲线使用更平稳的切线，防止放大遮罩像素台阶造成细碎抖动。
            for (int pass = 0; pass < 3; pass++)
            {
                var smooth = new Vector2[samples];
                for (int i = 0; i < samples; i++)
                    smooth[i] = (result[(i + samples - 4) % samples] + result[(i + samples - 2) % samples] * 2 +
                        result[i] * 6 + result[(i + 2) % samples] * 2 + result[(i + 4) % samples]) / 12;
                result = smooth;
            }
            return result;
        }

        private void ReleaseCapture()
        {
            if (capture == null) return;
            capture.Release();
            CoreUtils.Destroy(capture);
            capture = null;
        }

        /// <summary>立即释放网格；尚在读回的纹理由完成回调释放，避免访问已销毁资源。</summary>
        public void Dispose()
        {
            disposed = true;
            pendingPixels = null;
            loops.Clear();
            sampledLoops.Clear();
            CoreUtils.Destroy(StrokeMesh);
            StrokeMesh = null;
            if (!reading) ReleaseCapture();
        }
    }
}
