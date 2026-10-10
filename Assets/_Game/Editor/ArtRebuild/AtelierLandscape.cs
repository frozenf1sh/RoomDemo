// 文件：AtelierLandscape.cs
// 职责：为独立画室制作有真实空间关系的夜间坡地、木作小屋与枝叶。
// 架构边界：仅生成 Editor 美术网格，不引用旧场景、规则或碰撞，不使用背景图片。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.ArtRebuild
{
    /// <summary>近枝、中景村落、远山均为可编辑的三维场景资产。</summary>
    public static class AtelierLandscape
    {
        private static Material timber, wall, roof, ground, leaf, metal;

        /// <summary>按室内真实视线重新错开房屋，并用同一高度场支撑村落。</summary>
        public static void RegradeValley()
        {
            var root = GameObject.Find("独立夜间画室/窗外山谷").transform;
            var village = root.Find("中景错落小屋");
            string[] names = { "近坡石脚小屋", "中坡窄屋", "远坡旧屋" };
            Vector2[] places = { new Vector2(2.2f, 18), new Vector2(8.4f, 31), new Vector2(16.5f, 48) };
            for (int i = 0; i < 3; i++) village.Find(names[i]).localPosition = new Vector3(places[i].x, Height(places[i].x, places[i].y), places[i].y);
            AtelierMeshLibrary.Save("窗外山坡网格", Terrain(-38, 42, 6, 84, 100, 100));
            foreach (Transform parent in new[] { root.Find("近景枝叶与坡地"), village })
                foreach (Transform child in parent)
                    if (child.name.StartsWith("山坡树_") || child.name.StartsWith("窗左") || child.name.StartsWith("窗右"))
                        child.localPosition = new Vector3(child.localPosition.x, Height(child.localPosition.x, child.localPosition.z), child.localPosition.z);
            AtelierSceneBuilder.Save();
        }

        /// <summary>只更新本工具生成的枝叶网格，保留已经审看的村落与坡地布局。</summary>
        public static void RefineFoliage()
        {
            var root = GameObject.Find("独立夜间画室/窗外山谷").transform;
            timber = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/ArtRebuild/Materials/深暖旧木.mat");
            leaf = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/ArtRebuild/Materials/深绿叶片.mat");
            var near = root.Find("近景枝叶与坡地");
            var village = root.Find("中景错落小屋");
            foreach (Transform parent in new[] { near, village })
            {
                var remove = new List<GameObject>();
                foreach (Transform child in parent)
                    if (child.name.StartsWith("窗左") || child.name.StartsWith("窗右") || child.name.StartsWith("山坡树_")) remove.Add(child.gameObject);
                foreach (var tree in remove) Object.DestroyImmediate(tree);
            }
            Tree(near, "窗左近枝", new Vector3(-1.1f, Height(-1.1f, 9), 9), 5.7f, 19);
            Tree(near, "窗右树冠", new Vector3(5, Height(5, 12), 12), 5.5f, 63);
            for (int i = 0; i < 12; i++)
            {
                float z = 19 + i * 3.9f;
                float x = i % 2 == 0 ? -3.7f + i * .35f : 10.7f - i * .14f;
                Tree(village, "山坡树_" + i, new Vector3(x, Height(x, z), z), 3.8f + i % 3 * .7f, i * 23 + 5);
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>创建三层外景；小屋沿坡错开前后与朝向，夜灯数量固定稀疏。</summary>
        public static void Build(Transform root, Material wood, Material plaster, Material slate, Material soil, Material leaves, Material brass)
        {
            timber = wood; wall = plaster; roof = slate; ground = soil; leaf = leaves; metal = brass;
            var near = AtelierSceneBuilder.Group(root, "近景枝叶与坡地");
            var village = AtelierSceneBuilder.Group(root, "中景错落小屋");
            var far = AtelierSceneBuilder.Group(root, "远山轮廓");
            AtelierSceneBuilder.Node(near, "连续起伏的山坡", AtelierMeshLibrary.Save("窗外山坡网格", Terrain(-38, 42, 6, 84, 100, 100)), ground, Vector3.zero);
            Cottage(village, "近坡石脚小屋", new Vector3(1.45f, Height(1.45f, 17), 17), 1, -14, false);
            Cottage(village, "中坡窄屋", new Vector3(5.8f, Height(5.8f, 28), 28), .85f, 13, true);
            Cottage(village, "远坡旧屋", new Vector3(2.9f, Height(2.9f, 43), 43), .91f, -21, false);
            Tree(near, "窗左近枝", new Vector3(-1.1f, Height(-1.1f, 9), 9), 5.7f, 19);
            Tree(near, "窗右树冠", new Vector3(5.0f, Height(5, 12), 12), 5.5f, 63);
            for (int i = 0; i < 12; i++)
            {
                float z = 19 + i * 3.9f;
                float x = i % 2 == 0 ? -3.7f + i * .35f : 10.7f - i * .14f;
                Tree(village, "山坡树_" + i, new Vector3(x, Height(x, z), z), 3.8f + (i % 3) * .7f, i * 23 + 5);
            }
            for (int i = 0; i < 3; i++)
            {
                var mountain = AtelierSceneBuilder.Lit("远山蓝灰_" + i, new Color(.065f + i * .025f, .095f + i * .030f, .15f + i * .045f), null, null, .03f);
                AtelierSceneBuilder.Node(far, "远山层_" + i, AtelierMeshLibrary.Save("远山网格_" + i, Mountain(i)), mountain, Vector3.zero);
            }
        }

        /// <summary>坡面连续高度；房屋和树的根部用同一函数落地。</summary>
        private static float Height(float x, float z)
        {
            return -2.4f + .12f * (z - 10) + .44f * Mathf.Sin(x * .14f + z * .055f) + .30f * Mathf.Sin(z * .18f - x * .09f);
        }

        /// <summary>使用密集平滑网格建立近中景坡面，不以雾遮住几何边界。</summary>
        private static Mesh Terrain(float left, float right, float front, float back, int nx, int nz)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                float px = Mathf.Lerp(left, right, (float)x / nx), pz = Mathf.Lerp(front, back, (float)z / nz);
                v.Add(new Vector3(px, Height(px, pz), pz)); uv.Add(new Vector2(px * .13f, pz * .13f));
            }
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int i = z * (nx + 1) + x;
                t.AddRange(new[] { i, i + nx + 2, i + 1, i, i + nx + 1, i + nx + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>宽远山采用不同轮廓、实际距离与低对比色阶。</summary>
        private static Mesh Mountain(int layer)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= 180; i++)
            {
                float x = -140 + i * 1.6f;
                float z = 90 + layer * 36 + 9 * Mathf.Sin(x * .025f + layer);
                float h = 9 + layer * 6 + 4 * Mathf.Sin(x * .041f + layer * 2.3f) + 2.3f * Mathf.Sin(x * .083f - layer) + 1.1f * Mathf.Sin(x * .18f);
                v.Add(new Vector3(x, -8, z - 6)); uv.Add(new Vector2((float)i / 180, 0));
                v.Add(new Vector3(x, h, z)); uv.Add(new Vector2((float)i / 180, 1));
                if (i == 180) continue;
                int k = i * 2; t.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>带实体开窗、瓦片搭接、屋脊、石脚与砖烟囱的专属中景建筑。</summary>
        private static void Cottage(Transform parent, string name, Vector3 foot, float scale, float yaw, bool narrow)
        {
            var root = AtelierSceneBuilder.Group(parent, name); root.localPosition = foot; root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale;
            float width = narrow ? 2.15f : 2.75f, depth = narrow ? 3.2f : 2.65f, height = narrow ? 2.8f : 2.35f;
            var stone = AtelierSceneBuilder.Lit(name + "_石脚", new Color(.22f, .24f, .22f), wall.GetTexture("_BaseMap") as Texture2D, wall.GetTexture("_BumpMap") as Texture2D, .08f);
            for (int i = 0; i < 9; i++)
            {
                var block = AtelierSceneBuilder.Beam(root, "不齐基础石_" + i, new Vector3(-width * .5f + .16f + i * (width - .32f) / 8, .15f, -depth * .5f), new Vector3(.34f, .30f, .26f), stone, .047f);
                block.localRotation = Quaternion.Euler(0, (i % 3 - 1) * 4, 0);
            }
            AtelierSceneBuilder.Beam(root, "左侧厚墙", new Vector3(-width * .5f, height * .5f, 0), new Vector3(.16f, height, depth), wall, .035f);
            AtelierSceneBuilder.Beam(root, "右侧厚墙", new Vector3(width * .5f, height * .5f, 0), new Vector3(.16f, height, depth), wall, .035f);
            AtelierSceneBuilder.Beam(root, "后侧厚墙", new Vector3(0, height * .5f, depth * .5f), new Vector3(width, height, .16f), wall, .035f);
            // 正面两扇窗有实际空洞，窗框、暗室和发光窗面分离。
            float[] xx = { -width * .5f, -.89f, -.42f, .42f, .89f, width * .5f };
            float[] yy = { 0, .82f, 1.61f, height };
            for (int x = 0; x < xx.Length - 1; x++) for (int y = 0; y < yy.Length - 1; y++)
            {
                if (y == 1 && (x == 1 || x == 3)) continue;
                AtelierSceneBuilder.Beam(root, "开窗墙段_" + x + "_" + y, new Vector3((xx[x] + xx[x + 1]) * .5f, (yy[y] + yy[y + 1]) * .5f, -depth * .5f), new Vector3(xx[x + 1] - xx[x], yy[y + 1] - yy[y], .19f), wall, .014f);
            }
            var gable = new Mesh { vertices = new[] { new Vector3(-width * .5f, height, -depth * .5f), new Vector3(0, height + 1.04f, -depth * .5f), new Vector3(width * .5f, height, -depth * .5f), new Vector3(-width * .5f, height, depth * .5f), new Vector3(0, height + 1.04f, depth * .5f), new Vector3(width * .5f, height, depth * .5f) }, triangles = new[] { 0, 1, 2, 3, 5, 4 }, uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.zero, Vector2.up, Vector2.one } };
            gable.RecalculateNormals();
            AtelierSceneBuilder.Node(root, "灰泥山墙", AtelierMeshLibrary.Save(name + "_山墙", gable), wall, Vector3.zero);
            foreach (float x in new[] { -width * .5f, width * .5f }) AtelierSceneBuilder.Beam(root, "木角柱", new Vector3(x, height * .5f, -depth * .5f - .12f), new Vector3(.12f, height, .12f), timber, .015f);
            AtelierSceneBuilder.Beam(root, "山墙下承重木", new Vector3(0, height - .02f, -depth * .5f - .12f), new Vector3(width + .15f, .13f, .13f), timber, .012f);
            foreach (float side in new[] { -1f, 1f })
            {
                float angle = side * -Mathf.Atan2(1.04f, width * .5f + .15f) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(Mathf.Pow(width * .5f + .18f, 2) + 1.04f * 1.04f);
                var roofBase = AtelierSceneBuilder.Beam(root, "厚木屋面", new Vector3(side * (width * .25f + .07f), height + .51f, 0), new Vector3(length + .2f, .10f, depth + .48f), timber, .014f);
                roofBase.localRotation = Quaternion.Euler(0, 0, angle);
                // 每列瓦片有搭接厚度和轻微错位；近景不是一块平涂屋顶。
                for (int row = 0; row < 7; row++) for (int col = 0; col < 13; col++)
                {
                    float x = .06f + row * (width * .5f + .22f) / 7;
                    float y = height + 1.06f - x * 1.04f / (width * .5f + .15f) + .018f * (7 - row);
                    float z = -depth * .5f - .18f + col * (depth + .36f) / 12 + (row % 2) * .055f;
                    var tile = AtelierSceneBuilder.Beam(root, "搭接旧板瓦_" + side + "_" + row + "_" + col, new Vector3(side * x, y, z), new Vector3(.37f, .034f, (depth + .36f) / 12 + .025f), roof, .011f);
                    tile.localRotation = Quaternion.Euler(0, (row + col) % 3 - 1, angle + .8f * Mathf.Sin(col * 13 + row));
                }
                AtelierSceneBuilder.Link(root, "山墙斜封檐_" + side, new Vector3(0, height + 1.08f, -depth * .5f - .25f), new Vector3(side * (width * .5f + .27f), height - .05f, -depth * .5f - .25f), .065f, timber);
            }
            AtelierSceneBuilder.Beam(root, "屋脊脊瓦", new Vector3(0, height + 1.16f, 0), new Vector3(.18f, .12f, depth + .5f), roof, .035f);
            var glowing = AtelierSceneBuilder.Lit(name + "_稀疏暖窗", new Color(.20f, .10f, .025f), null, null, .18f);
            glowing.EnableKeyword("_EMISSION"); glowing.SetColor("_EmissionColor", new Color(1.2f, .49f, .105f));
            var dark = AtelierSceneBuilder.Lit(name + "_未亮暗窗", new Color(.015f, .022f, .028f), null, null, .28f);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -.655f : .655f;
                AtelierSceneBuilder.Beam(root, "窗内暗室_" + i, new Vector3(x, 1.215f, -depth * .5f + .085f), new Vector3(.44f, .76f, .055f), i == 0 ? glowing : dark, .012f);
                foreach (float dx in new[] { -.25f, .25f }) AtelierSceneBuilder.Beam(root, "窗套竖边", new Vector3(x + dx, 1.215f, -depth * .5f - .13f), new Vector3(.06f, .88f, .12f), timber, .008f);
                foreach (float dy in new[] { -.42f, .42f }) AtelierSceneBuilder.Beam(root, "窗套横边", new Vector3(x, 1.215f + dy, -depth * .5f - .13f), new Vector3(.55f, .06f, .12f), timber, .008f);
                AtelierSceneBuilder.Beam(root, "窗格中梃", new Vector3(x, 1.215f, -depth * .5f - .09f), new Vector3(.035f, .78f, .06f), timber, .005f);
                AtelierSceneBuilder.Beam(root, "窗格横梃", new Vector3(x, 1.24f, -depth * .5f - .09f), new Vector3(.47f, .035f, .06f), timber, .005f);
                AtelierSceneBuilder.Beam(root, "厚窗台", new Vector3(x, .79f, -depth * .5f - .18f), new Vector3(.66f, .07f, .29f), stone, .018f);
            }
            // 烟囱以独立砖层形成轮廓；没有火焰、烟雾或全村实时点光。
            for (int y = 0; y < 8; y++)
            {
                var brick = AtelierSceneBuilder.Beam(root, "烟囱砖层_" + y, new Vector3(.74f, height + .68f + y * .11f, .55f), new Vector3(.38f, .10f, .41f), stone, .018f);
                brick.localRotation = Quaternion.Euler(0, y % 2 == 0 ? 0 : 90, 0);
            }
        }

        /// <summary>树枝为连续锥管，叶片为弯曲尖叶；不以锥体或球簇替代树冠。</summary>
        private static void Tree(Transform parent, string name, Vector3 foot, float height, int seed)
        {
            var rng = new System.Random(seed);
            var root = AtelierSceneBuilder.Group(parent, name); root.localPosition = foot;
            var branchVertices = new List<Vector3>(); var branchUv = new List<Vector2>(); var branchTriangles = new List<int>();
            var leafVertices = new List<Vector3>(); var leafUv = new List<Vector2>(); var leafTriangles = new List<int>();
            void Tube(Vector3 a, Vector3 b, float radius)
            {
                const int sides = 7;
                Vector3 direction = (b - a).normalized;
                Vector3 tangent = Vector3.Cross(direction, Vector3.forward).normalized;
                Vector3 across = Vector3.Cross(direction, tangent);
                int k = branchVertices.Count;
                for (int ring = 0; ring < 2; ring++) for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    branchVertices.Add((ring == 0 ? a : b) + (tangent * Mathf.Cos(angle) + across * Mathf.Sin(angle)) * radius * (ring == 0 ? 1 : .67f));
                    branchUv.Add(new Vector2((float)i / sides, ring * Vector3.Distance(a, b)));
                }
                for (int i = 0; i < sides; i++) branchTriangles.AddRange(new[] { k + i, k + i + sides + 2, k + i + 1, k + i, k + i + sides + 1, k + i + sides + 2 });
            }
            void Leaf(Vector3 p, float size)
            {
                Quaternion rotation = Quaternion.Euler((float)rng.NextDouble() * 150, (float)rng.NextDouble() * 360, (float)rng.NextDouble() * 360);
                int k = leafVertices.Count;
                Vector3[] shape = { new Vector3(0, 0, -.5f), new Vector3(-.27f, 0, -.16f), new Vector3(-.23f, .04f, .25f), new Vector3(0, .07f, .5f), new Vector3(.23f, .04f, .25f), new Vector3(.27f, 0, -.16f), new Vector3(0, .10f, 0) };
                foreach (var point in shape) { leafVertices.Add(p + rotation * point * size); leafUv.Add(new Vector2(point.x + .5f, point.z + .5f)); }
                for (int i = 0; i < 6; i++) leafTriangles.AddRange(new[] { k + 6, k + i, k + (i + 1) % 6 });
            }
            void Branch(Vector3 start, Vector3 direction, float length, float radius, int depth)
            {
                Vector3 mid = start + direction * length * .52f;
                Vector3 end = start + direction * length + Vector3.up * length * .13f;
                Tube(start, mid, radius); Tube(mid, end, radius * .74f);
                if (depth <= 0)
                {
                    for (int j = 0; j < 44; j++)
                    {
                        Vector3 scatter = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * length * .85f;
                        Leaf(Vector3.Lerp(mid, end, (float)rng.NextDouble()) + scatter, .20f + (float)rng.NextDouble() * .13f);
                    }
                    return;
                }
                for (int i = 0; i < 3; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2;
                    Vector3 next = (direction * .45f + new Vector3(Mathf.Cos(a) * .82f, .30f + (float)rng.NextDouble() * .40f, Mathf.Sin(a) * .82f)).normalized;
                    Branch(Vector3.Lerp(mid, end, .30f + i * .29f), next, length * (.49f + (float)rng.NextDouble() * .12f), radius * .49f, depth - 1);
                }
            }
            Branch(Vector3.zero, new Vector3(.045f, 1, .035f).normalized, height * .62f, height * .027f, 4);
            var branches = new Mesh { indexFormat = IndexFormat.UInt32 }; branches.SetVertices(branchVertices); branches.SetUVs(0, branchUv); branches.SetTriangles(branchTriangles, 0); branches.RecalculateNormals();
            var foliage = new Mesh { indexFormat = IndexFormat.UInt32 }; foliage.SetVertices(leafVertices); foliage.SetUVs(0, leafUv); foliage.SetTriangles(leafTriangles, 0); foliage.RecalculateNormals();
            AtelierSceneBuilder.Node(root, "连续分叉树枝", AtelierMeshLibrary.Save(name + "_枝", branches), timber, Vector3.zero);
            AtelierSceneBuilder.Node(root, "曲面尖叶", AtelierMeshLibrary.Save(name + "_叶", foliage), leaf, Vector3.zero);
        }
    }
}
