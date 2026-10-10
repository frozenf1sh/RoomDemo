// 文件：AtelierMeshLibrary.cs
// 职责：制作独立画室的倒角木作、厚屋顶、织物与陶器网格。
// 架构边界：仅 Editor 美术资产，不创建碰撞、不读取领域事实，不使用概念图贴图。
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.ArtRebuild
{
    /// <summary>具有真实厚边和连续法线的美术网格库。</summary>
    public static class AtelierMeshLibrary
    {
        private const string Folder = "Assets/_Game/Content/ArtRebuild/Meshes/";

        /// <summary>保存本工具拥有的网格；重做时保留资源 GUID 和场景引用。</summary>
        public static Mesh Save(string name, Mesh source)
        {
            source.name = name;
            source.RecalculateBounds();
            source.RecalculateTangents();
            string path = Folder + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, path);
                return source;
            }
            EditorUtility.CopySerialized(source, existing);
            UnityEngine.Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>以曲面倒角制作实木构件；UV 按真实米制铺设，避免拉长木纹。</summary>
        public static Mesh Timber(Vector3 size, float bevel)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3 half = size * .5f;
            bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .7f);
            Vector3 inner = half - Vector3.one * bevel;
            // 六个面各用四条控制线，边角以相邻面的距离场法线圆滑连接。
            for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int a = (axis + 1) % 3, b = (axis + 2) % 3;
                float[] aa = { -half[a], -inner[a], inner[a], half[a] };
                float[] bb = { -half[b], -inner[b], inner[b], half[b] };
                int start = v.Count;
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    Vector3 p = Vector3.zero;
                    p[axis] = sign * half[axis];
                    p[a] = aa[x];
                    p[b] = bb[y];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 normal = (p - c).normalized;
                    v.Add(c + normal * bevel);
                    n.Add(normal);
                    uv.Add(new Vector2(aa[x] * .45f, bb[y] * .45f));
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = start + y * 4 + x;
                    if (sign > 0) t.AddRange(new[] { i, i + 1, i + 5, i, i + 5, i + 4 });
                    else t.AddRange(new[] { i, i + 5, i + 1, i, i + 4, i + 5 });
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            return mesh;
        }

        /// <summary>中央不规则擦开，外围仍有灰泥的顶面、底面与真实断口厚度。</summary>
        public static Mesh Roof()
        {
            const int count = 256;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                float grain = 1 + .055f * Mathf.Sin(a * 5 + .7f) + .036f * Mathf.Sin(a * 13) + .013f * Mathf.Sin(a * 61);
                Vector3 inside = new Vector3(.10f + x * 2.78f * grain, 4.22f, -.40f + z * 2.91f * grain);
                float reach = Mathf.Min(4.36f / Mathf.Max(.001f, Mathf.Abs(x)), 4.35f / Mathf.Max(.001f, Mathf.Abs(z)));
                Vector3 outside = new Vector3(x * reach, 4.22f, z * reach);
                v.Add(outside); v.Add(inside); v.Add(outside + Vector3.up * .16f); v.Add(inside + Vector3.up * .16f);
                for (int j = 0; j < 4; j++) uv.Add(new Vector2(v[v.Count - 4 + j].x * .36f, v[v.Count - 4 + j].z * .36f));
            }
            for (int i = 0; i < count; i++)
            {
                int k = i * 4;
                t.AddRange(new[] { k, k + 5, k + 1, k, k + 4, k + 5 });
                t.AddRange(new[] { k + 2, k + 3, k + 7, k + 2, k + 7, k + 6 });
                t.AddRange(new[] { k + 1, k + 3, k + 7, k + 1, k + 7, k + 5 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>带收束顶部和不齐下摆的亚麻帘；每条褶皱都有受光曲面。</summary>
        public static Mesh Curtain(float width, float height, int seed)
        {
            const int nx = 72, ny = 36;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, w = (float)y / ny;
                float phase = u * Mathf.PI * 11 + seed * .8f + .33f * Mathf.Sin(w * 4 + u * 9);
                float pleat = Mathf.Sin(phase) + .22f * Mathf.Sin(phase * 2.7f + w);
                float flare = .82f + .18f * w;
                v.Add(new Vector3((u - .5f) * width * flare + .024f * Mathf.Sin(w * 5 + seed) * w, -w * height + .033f * Mathf.Sin(u * 21 + seed) * w * w,
                    pleat * (.041f + .021f * w) + .021f * Mathf.Sin(w * 8 + u * 10 + seed)));
                uv.Add(new Vector2(u * 1.2f, w * 3));
            }
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                int i = y * (nx + 1) + x;
                t.AddRange(new[] { i, i + nx + 2, i + 1, i, i + nx + 1, i + nx + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>桌布有水平承托、转折和下垂；不以薄盒子代替织物。</summary>
        public static Mesh Drape()
        {
            const int nx = 40, ny = 52;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, w = (float)y / ny;
                float fold = Mathf.Sin(u * 39 + w * 2) * .0035f + Mathf.Sin(u * 11 - w * 6) * .004f;
                float z = .55f - Mathf.Min(w * 1.15f, .72f);
                float drop = Mathf.Max(0, w * 1.15f - .72f);
                v.Add(new Vector3((u - .5f) * .68f, .012f + fold - drop, z - .035f * Mathf.Sin(drop * 7)));
                uv.Add(new Vector2(u * 1.3f, w * 2.3f));
            }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int i = y * (nx + 1) + x;
                t.AddRange(new[] { i, i + 1, i + nx + 2, i, i + nx + 2, i + nx + 1 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>车制陶器、铜灯和螺丝的旋转截面；轮廓本身包含口沿与内壁。</summary>
        public static Mesh Lathe(Vector2[] profile, int segments = 64)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j < profile.Length; j++)
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a) * profile[j].x, profile[j].y, Mathf.Sin(a) * profile[j].x));
                uv.Add(new Vector2((float)i / segments, (float)j / (profile.Length - 1)));
            }
            for (int j = 0; j < profile.Length - 1; j++) for (int i = 0; i < segments; i++)
            {
                int k = j * (segments + 1) + i;
                t.AddRange(new[] { k, k + segments + 2, k + 1, k, k + segments + 1, k + segments + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>纸张采用微翘边曲面和不齐纸边，细节只表达纸张结构。</summary>
        public static Mesh Paper(float width, float depth, int seed)
        {
            const int nx = 20, ny = 18;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, w = (float)y / ny;
                float edge = Mathf.Pow(Mathf.Abs(u * 2 - 1), 12) + Mathf.Pow(Mathf.Abs(w * 2 - 1), 12);
                v.Add(new Vector3((u - .5f) * width + Mathf.Sin(y * 8 + seed) * .0015f,
                    edge * (.003f + .004f * Mathf.Sin(u * 4 + w * 3 + seed)), (w - .5f) * depth));
                uv.Add(new Vector2(u, w));
            }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int k = y * (nx + 1) + x;
                t.AddRange(new[] { k, k + nx + 2, k + 1, k, k + nx + 1, k + nx + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }
    }
}
