// =============================================================================
// 文件：AtelierCurtainPresenter.cs
// 职责：沿铜杆约束帘环，收拢亚麻网格，并让下摆延迟沉降。
// 边界：只读取拉帘事实；不提交命令、不移动整幅布的 Transform，不修改共享网格。
// =============================================================================
using Game.Application;
using Game.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>使用现有密集亚麻网格；上缘由帘环约束，下摆保持原长并增加褶深。</summary>
    public sealed class AtelierCurtainPresenter : MonoBehaviour
    {
        public MeshFilter[] Cloth;
        public MeshCollider[] PickingCloth;
        public Transform[] Rings;
        public MeshFilter[] Straps;
        public LineRenderer Cord;
        public Transform Pull;
        private GameSession session;
        private Mesh[] owned, strapMeshes, pickingMeshes;
        private Vector3[][] original, vertices, outerEdges;
        private Vector2[][] coordinates;
        private int[][] topIndices;
        private float age;
        private bool released;
        /// <summary>接触演出结束以前，输入层不能把尚未出现的结果当作观察。</summary>
        public bool Gathering => released && age < 4.8f;

        /// <summary>复制网格并恢复既有事实；这些副本由本组件释放。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            owned = new Mesh[Cloth.Length]; original = new Vector3[Cloth.Length][];
            vertices = new Vector3[Cloth.Length][]; coordinates = new Vector2[Cloth.Length][];
            outerEdges = new Vector3[Cloth.Length][];
            topIndices = new int[Cloth.Length][];
            strapMeshes = new Mesh[Straps.Length];
            pickingMeshes = new Mesh[Cloth.Length];
            for (int i = 0; i < Cloth.Length; i++)
            {
                owned[i] = Instantiate(Cloth[i].sharedMesh); owned[i].MarkDynamic();
                original[i] = owned[i].vertices; vertices[i] = new Vector3[original[i].Length];
                coordinates[i] = owned[i].uv; Cloth[i].sharedMesh = owned[i];
                pickingMeshes[i]=Instantiate(owned[i]);
                var front=owned[i].triangles;var both=new int[front.Length*2];
                System.Array.Copy(front,both,front.Length);
                for(int triangle=0;triangle<front.Length;triangle+=3)
                {
                    both[front.Length+triangle]=front[triangle];
                    both[front.Length+triangle+1]=front[triangle+2];
                    both[front.Length+triangle+2]=front[triangle+1];
                }
                pickingMeshes[i].triangles=both;
                // 薄布的两面都可观察；仅此拾取网格保留反向三角面，不改全局背面查询。
                PickingCloth[i].cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.UseFastMidphase;
                PickingCloth[i].sharedMesh=pickingMeshes[i];
                outerEdges[i] = OuterEdges(original[i], coordinates[i], i);
                topIndices[i] = new int[8];
                for (int ring=0;ring<8;ring++) topIndices[i][ring]=TopIndex(coordinates[i],ring);
            }
            for (int i = 0; i < Straps.Length; i++)
            {
                strapMeshes[i] = Instantiate(Straps[i].sharedMesh);
                Straps[i].sharedMesh = strapMeshes[i];
            }
            released = game.State.HasFact(StudioRules.CurtainReleased);
            age = released ? 6 : 0;
            Pose(age);
            RefreshPicking();
        }
        private void Update()
        {
            if (session == null) return;
            if (!released && session.State.HasFact(StudioRules.CurtainReleased)) { released = true; age = 0; }
            if (!released || age >= 5.4f) return;
            age = Mathf.Min(5.4f, age + Time.deltaTime);
            Pose(age);
            // 变形期间禁止结果观察；落稳后拾取网格跟随新轮廓，空出的窗口不再算作点击窗帘。
            if(age>=5.4f)RefreshPicking();
        }
        private void RefreshPicking()
        {
            for(int i=0;i<PickingCloth.Length;i++)
            {
                pickingMeshes[i].vertices=vertices[i];pickingMeshes[i].RecalculateBounds();
                PickingCloth[i].sharedMesh=null;
                PickingCloth[i].sharedMesh=pickingMeshes[i];
            }
        }
        /// <summary>计算一行布料的位置；上沿先走，下摆经张力传递后才收束，外侧接点不漂移。</summary>
        public static Vector3 Gather(Vector3 point, Vector3 outer, Vector2 uv, int side, float time)
        {
            float v = Mathf.Clamp01(uv.y / 4.8f);
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - v * .55f) / 3.1f));
            // 八枚帘环至少留 6 cm 间距；布料不是缩放，上缘与下摆使用不同收束量。
            float factor = 1 - t * Mathf.Lerp(.22f, .46f, Mathf.SmoothStep(0, 1, v));
            float u = uv.x / 1.5f;
            point.x = outer.x + (point.x - outer.x) * factor;
            point.z += Mathf.Sin(u * 7.4f * Mathf.PI + .38f * Mathf.Sin(v * 3.6f + side)) * .037f * t * v;
            float settle = time > 2 ? Mathf.Sin((time - 2) * 7 - v * 2) * Mathf.Exp(-(time - 2) * 1.9f) : 0;
            point.z += settle * .013f * v * v;
            point.y -= Mathf.Sin(u * Mathf.PI) * .010f * t * v;
            return point;
        }
        /// <summary>按 UV 行寻找外边；Unity 优化后的顶点可能重排或分裂，不能假定规则网格索引。</summary>
        public static Vector3[] OuterEdges(Vector3[] points, Vector2[] uv, int side)
        {
            var rows = new Dictionary<float, int>();
            for (int i=0;i<uv.Length;i++)
                if (!rows.TryGetValue(uv[i].y,out int current) || (side==0 ? uv[i].x<uv[current].x : uv[i].x>uv[current].x)) rows[uv[i].y]=i;
            var result = new Vector3[points.Length];
            for (int i=0;i<result.Length;i++) result[i]=points[rows[uv[i].y]];
            return result;
        }
        /// <summary>从上缘 UV 找吊环的缝合点，不依赖导入器的顶点排序。</summary>
        public static int TopIndex(Vector2[] uv, int ring)
        {
            int nearest=0;float distance=float.MaxValue;
            for(int i=0;i<uv.Length;i++)
            {
                float candidate=Mathf.Abs(uv[i].x-ring*1.5f/7)+Mathf.Abs(uv[i].y)*100;
                if(candidate<distance){distance=candidate;nearest=i;}
            }
            return nearest;
        }
        private void Pose(float time)
        {
            for (int side = 0; side < Cloth.Length; side++)
            {
                for (int i = 0; i < vertices[side].Length; i++)
                {
                    vertices[side][i] = Gather(original[side][i], outerEdges[side][i], coordinates[side][i], side, time);
                }
                owned[side].vertices = vertices[side]; owned[side].RecalculateNormals(); owned[side].RecalculateBounds();
                for (int ring = 0; ring < 8; ring++)
                {
                    int index = topIndices[side][ring];
                    Vector3 anchor = Cloth[side].transform.TransformPoint(vertices[side][index]);
                    Rings[side * 8 + ring].position = new Vector3(anchor.x, 3.75f, 2.06f);
                    var strap = Straps[side * 8 + ring];
                    // 吊带连续连接布顶和铜环下缘，不让帘环悬在布料上方。
                    Vector3 top = strap.transform.InverseTransformPoint(new Vector3(anchor.x, 3.723f, 2.06f));
                    Vector3 bottom = strap.transform.InverseTransformPoint(anchor);
                    Vector3 width = Vector3.right * .009f;
                    var mesh = strapMeshes[side * 8 + ring];
                    mesh.vertices = new[] { top-width, top+width, bottom-width, bottom+width };
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                }
            }
            float force = Mathf.Sin(Mathf.Clamp01(time / 1.05f) * Mathf.PI) * .12f;
            Pull.position = new Vector3(-1.11f, 1.52f - force, 1.88f);
            // 闭合拉绳绕过上端导向环；下端受力只拉长绳环，不能离开固定支点。
            const int segments = 48;
            Cord.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2;
                Cord.SetPosition(i, new Vector3(-1.11f + Mathf.Sin(a) * .021f,
                    2.62f + Mathf.Cos(a) * (1.08f + force * .5f) - force * .5f, 1.88f));
            }
        }
        private void OnDestroy()
        {
            // 原始资产归项目所有；这里只释放运行时复制的网格。
            if (owned != null) foreach (var mesh in owned) if (mesh != null) Destroy(mesh);
            if (pickingMeshes != null) foreach (var mesh in pickingMeshes) if (mesh != null) Destroy(mesh);
            if (strapMeshes != null) foreach (var mesh in strapMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
