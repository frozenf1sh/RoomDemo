// 文件：AtelierPresentation.cs
// 职责：收整屋顶擦开残留，并把独立场景的真实运行镜头保存到文档证据目录。
// 架构边界：所有纹理由数学噪声生成；截图不作为游戏资源，不修改玩法或旧生产预设。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;

namespace Game.Editor.ArtRebuild
{
    /// <summary>独立美术场景的收整与镜头取证工具。</summary>
    public static class AtelierPresentation
    {
        private const string Art = "Assets/_Game/Content/ArtRebuild/";
        public const string Evidence = "Docs/DesignAudit/2026-10-10/主场景返工规划/实施截图/";
        private static int flowFrame;
        private static double nextFlowFrame;
        private static readonly List<string> flowTimes = new List<string>();

        /// <summary>在实际 Play 更新中记录六十帧主镜头，不手动推进天空时间；真实间隔另存。</summary>
        public static void BeginFlowCapture()
        {
            if (!UnityEngine.Application.isPlaying) throw new InvalidOperationException("流动记录需要实际运行场景。");
            EditorApplication.update -= CaptureFlowFrame;
            Directory.CreateDirectory("Logs/ArtRebuild20261010/FlowFrames");
            flowFrame = 0; nextFlowFrame = EditorApplication.timeSinceStartup;
            flowTimes.Clear(); flowTimes.Add("frame,editorTime,visualTime");
            EditorApplication.update += CaptureFlowFrame;
        }

        /// <summary>按真实时间采样，并在完成或退出运行时解除回调。</summary>
        private static void CaptureFlowFrame()
        {
            if (!UnityEngine.Application.isPlaying || Camera.main == null)
            {
                EditorApplication.update -= CaptureFlowFrame;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextFlowFrame) return;
            nextFlowFrame = EditorApplication.timeSinceStartup + .20;
            SaveCamera("Logs/ArtRebuild20261010/FlowFrames/" + flowFrame.ToString("D3") + ".png", 960, 540);
            var sky = UnityEngine.Object.FindAnyObjectByType<LivingSkyController>();
            flowTimes.Add(FormattableString.Invariant($"{flowFrame},{EditorApplication.timeSinceStartup:F3},{sky.Pigment.VisualTime:F3}"));
            flowFrame++;
            if (flowFrame < 60) return;
            EditorApplication.update -= CaptureFlowFrame;
            File.WriteAllLines("Logs/ArtRebuild20261010/FlowFrames/真实采样时间.csv", flowTimes);
            Debug.Log("独立画室：60 帧程序化星夜运行记录已完成，采样时间已单独保存。");
        }

        /// <summary>保留扫描窗周框的磨损细节，重做宽窗分格，避免两扇密集竖框挡住村落。</summary>
        public static void RefineWindowAndLighting()
        {
            var root = GameObject.Find("独立夜间画室").transform;
            var window = root.Find("建筑与木作/旧窗与亚麻帘");
            string path = Art + "Imports/QuixelWindow/Old_Wooden_Window_vmfwcabdw_Mid.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
            var mesh = WindowPerimeter(source);
            var first = window.Find("扫描旧木窗_0");
            first.GetComponentInChildren<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("扫描旧窗_保留周框", mesh);
            first.localPosition = new Vector3(1.35f, .97f, 4.12f);
            first.localScale = new Vector3(2.60f, 1.30f, 1.30f);
            var second = window.Find("扫描旧木窗_1");
            if (second != null) UnityEngine.Object.DestroyImmediate(second.gameObject);
            var previous = window.Find("宽窗细木分格");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var cross = AtelierSceneBuilder.Group(window, "宽窗细木分格");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/深暖旧木.mat");
            AtelierSceneBuilder.Beam(cross, "中央分格木", new Vector3(1.35f, 2.29f, 4.06f), new Vector3(.067f, 2.40f, .11f), wood, .009f);
            AtelierSceneBuilder.Beam(cross, "水平分格木", new Vector3(1.35f, 2.38f, 4.06f), new Vector3(3.10f, .060f, .10f), wood, .008f);
            var linen = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧亚麻.mat");
            linen.SetColor("_BaseColor", new Color(.81f, .75f, .63f)); linen.SetFloat("_BumpScale", .30f);
            for (int i = 0; i < 2; i++) AtelierMeshLibrary.Save("亚麻帘_" + i, AtelierMeshLibrary.Curtain(.67f, 2.70f, i));
            var lights = root.Find("照明");
            lights.Find("桌灯烘焙柔光").GetComponent<Light>().intensity = 32;
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().intensity = 2.2f;
            lights.Find("桌灯在画室中的漫反弹").position = new Vector3(.87f, 1.65f, -.56f);
            lights.Find("桌灯向下的暖工作区").GetComponent<Light>().intensity = 1.8f;
            lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>().intensity = 4.3f;
            var camera = root.GetComponentInChildren<Camera>();
            camera.transform.position = new Vector3(-.4f, 1.86f, -3.8f);
            camera.transform.rotation = Quaternion.Euler(-6, 4, 0);
            camera.fieldOfView = 55;
            var works = root.Find("原创风景习作");
            for (int i = 0; i < 2; i++) works.Find("左墙参考习作_" + i).localRotation = Quaternion.Euler(0, -90, i == 0 ? -3 : 4);
            AtelierSceneBuilder.Save();
        }

        /// <summary>带原始法线与贴图坐标的裁切顶点。</summary>
        private struct CutVertex
        {
            public Vector3 Position, Normal;
            public Vector2 Uv;
        }

        /// <summary>对四块周框精确裁切三角形；不会留下按重心删除造成的尖角。</summary>
        private static Mesh WindowPerimeter(Mesh source)
        {
            var sourceVertices = source.vertices; var sourceNormals = source.normals; var sourceUv = source.uv;
            var output = new List<CutVertex>(); var triangles = new List<int>();
            List<CutVertex> Clip(List<CutVertex> polygon, Func<Vector3, float> distance)
            {
                var result = new List<CutVertex>();
                if (polygon.Count == 0) return result;
                CutVertex previous = polygon[polygon.Count - 1]; float previousDistance = distance(previous.Position);
                foreach (var current in polygon)
                {
                    float d = distance(current.Position);
                    if ((d >= 0) != (previousDistance >= 0))
                    {
                        float ratio = previousDistance / (previousDistance - d);
                        result.Add(new CutVertex { Position = Vector3.LerpUnclamped(previous.Position, current.Position, ratio), Normal = Vector3.LerpUnclamped(previous.Normal, current.Normal, ratio).normalized, Uv = Vector2.LerpUnclamped(previous.Uv, current.Uv, ratio) });
                    }
                    if (d >= 0) result.Add(current);
                    previous = current; previousDistance = d;
                }
                return result;
            }
            var original = source.triangles;
            for (int i = 0; i < original.Length; i += 3)
            {
                var triangle = new List<CutVertex>();
                for (int j = 0; j < 3; j++)
                {
                    int k = original[i + j];
                    triangle.Add(new CutVertex { Position = sourceVertices[k], Normal = sourceNormals[k], Uv = sourceUv[k] });
                }
                for (int region = 0; region < 4; region++)
                {
                    List<CutVertex> polygon;
                    if (region == 0) polygon = Clip(triangle, p => -.575f - p.x);
                    else if (region == 1) polygon = Clip(triangle, p => p.x - .575f);
                    else
                    {
                        polygon = Clip(Clip(triangle, p => p.x + .575f), p => .575f - p.x);
                        polygon = region == 2 ? Clip(polygon, p => .125f - p.y) : Clip(polygon, p => p.y - 1.92f);
                    }
                    int start = output.Count; output.AddRange(polygon);
                    for (int j = 1; j < polygon.Count - 1; j++) triangles.AddRange(new[] { start, start + j, start + j + 1 });
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(output.Select(x => x.Position).ToList()); mesh.SetNormals(output.Select(x => x.Normal).ToList());
            mesh.SetUVs(0, output.Select(x => x.Uv).ToList()); mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>替换桌布曲面、调整细星，并加入灰泥擦开的干刷残留。</summary>
        public static void Refine()
        {
            var root = GameObject.Find("独立夜间画室").transform;
            AtelierMeshLibrary.Save("桌沿垂布", AtelierMeshLibrary.Drape());
            var sky = root.GetComponentInChildren<LivingSkyController>();
            sky.InitialLight = .36f;
            sky.Surface.sharedMaterial.SetFloat("_FineStarGain", 1.7f);
            var existing = root.Find("建筑与木作/屋顶干刷残留");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            const int nx = 768, ny = 12;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= nx; i++)
            {
                float angle = i * Mathf.PI * 2 / nx;
                float grain = 1 + .055f * Mathf.Sin(angle * 5 + .7f) + .036f * Mathf.Sin(angle * 13) + .013f * Mathf.Sin(angle * 61);
                for (int j = 0; j <= ny; j++)
                {
                    float w = (float)j / ny;
                    float radius = grain * Mathf.Lerp(.87f, 1.035f, w);
                    v.Add(new Vector3(.1f + Mathf.Cos(angle) * 2.78f * radius, 4.212f - .008f * Mathf.Sin(angle * 31) * (1 - w), -.4f + Mathf.Sin(angle) * 2.91f * radius));
                    uv.Add(new Vector2((float)i / nx, w));
                }
            }
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
            {
                int k = i * (ny + 1) + j;
                t.AddRange(new[] { k, k + ny + 2, k + 1, k, k + ny + 1, k + ny + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            string texPath = Art + "Textures/数学生成_屋顶干刷残留.png";
            var tex = new Texture2D(2048, 128, TextureFormat.RGBA32, false);
            var pixels = new Color[2048 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 2048; x++)
            {
                float u = x / 2048f, w = y / 127f;
                float stroke = Mathf.PerlinNoise(u * 373, w * 4) * .55f + Mathf.PerlinNoise(u * 91, w * 9) * .30f + Mathf.PerlinNoise(u * 957, w * 2) * .15f;
                float edge = .13f + stroke * .69f;
                float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((w - edge) * 19));
                Color c = Color.Lerp(new Color(.22f, .29f, .38f), new Color(.59f, .55f, .45f), Mathf.SmoothStep(0, 1, w));
                c *= .88f + stroke * .24f;
                c.a = alpha;
                pixels[y * 2048 + x] = c;
            }
            tex.SetPixels(pixels); tex.Apply(); File.WriteAllBytes(texPath, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.SaveAndReimport();
            var mat = AtelierSceneBuilder.Lit("屋顶干刷边缘", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), null, .08f);
            mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .48f); mat.SetFloat("_Cull", 0);
            mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450;
            AtelierSceneBuilder.Node(root.Find("建筑与木作"), "屋顶干刷残留", AtelierMeshLibrary.Save("屋顶干刷薄层", mesh), mat, Vector3.zero);
            AtelierSceneBuilder.Save();
        }

        /// <summary>保存当前运行主镜头，保证证据留在 Assets 之外。恢复所有临时渲染目标。</summary>
        /// <param name="name">不带路径的 PNG 文件名。</param>
        /// <returns>项目内证据路径。</returns>
        /// <exception cref="InvalidOperationException">未进入运行状态或没有主镜头。</exception>
        public static string Capture(string name)
        {
            if (!UnityEngine.Application.isPlaying || Camera.main == null) throw new InvalidOperationException("请先运行独立画室，再保存真实镜头。");
            if (Path.GetFileName(name) != name || !name.EndsWith(".png")) throw new ArgumentException("截图只接受 PNG 文件名。");
            Directory.CreateDirectory(Evidence);
            SaveCamera(Evidence + name, 1600, 900);
            return Evidence + name;
        }

        /// <summary>临时离屏渲染，文件始终写在指定证据目录；释放临时 GPU／CPU 资源。</summary>
        private static void SaveCamera(string path, int width, int height)
        {
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(rt);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
