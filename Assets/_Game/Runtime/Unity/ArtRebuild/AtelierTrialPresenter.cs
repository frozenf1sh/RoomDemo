// =============================================================================
// 文件：AtelierTrialPresenter.cs
// 职责：显示湿色擦拭与贴着木板的柔软布面。
// 边界：只消费事实与手势位置；布的运行时网格由本组件释放，不写 GameState。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>手势只改变临时压力与布面形态；完成命令以后才保留擦痕。</summary>
    public sealed class AtelierTrialPresenter : MonoBehaviour
    {
        public Renderer WetPaint;
        public MeshFilter Cloth;
        public Vector3 BoardCentre = new Vector3(.42f, .985f, -1.35f);
        private GameSession session;
        private Mesh mesh;
        private Vector3[] rest, points;
        private MaterialPropertyBlock paintBlock;
        private Vector2[] uv;
        private Vector3 contact;
        private float pressure, preview, retained;
        /// <summary>布离开后才允许采集擦后观察。</summary>
        public bool Settled => pressure < .025f;
        /// <summary>绑定后复制布网格，所有变形均局限在桌面接触区域。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            mesh = Instantiate(Cloth.sharedMesh);
            mesh.name = "擦笔布_运行时受约束褶皱";
            mesh.MarkDynamic();
            rest = mesh.vertices;
            uv = mesh.uv;
            points = (Vector3[])rest.Clone();
            Cloth.sharedMesh = mesh;
            paintBlock = new MaterialPropertyBlock();
            contact = BoardCentre;
        }
        /// <summary>按住手势时设置木板上的接触点；松开后布留在接触位置并自然落稳。</summary>
        public void Contact(Vector3 world, float amount)
        {
            contact = new Vector3(Mathf.Clamp(world.x, BoardCentre.x - .24f, BoardCentre.x + .24f),
                                  BoardCentre.y + .003f,
                                  Mathf.Clamp(world.z, BoardCentre.z - .12f, BoardCentre.z + .12f));
            pressure = 1;
            preview = Mathf.Max(preview, amount);
        }
        /// <summary>松手保留局部擦痕；完整擦除事实仍由下一帧读取。</summary>
        public void Release()
        {
            // 松手后布留在最后接触处，不能自动滑回原来的停放位置。
            if (points != null && pressure > .05f)
                rest = (Vector3[])points.Clone();
        }
        private void Update()
        {
            if (session == null)
                return;
            float dt = Time.deltaTime;
            pressure = Mathf.MoveTowards(pressure, 0, dt * 2.2f);
            bool wiped = session.State.HasFact(AtelierRules.TrialWiped);
            retained = Mathf.MoveTowards(retained, wiped ? 1 : 0, dt * 1.4f);
            // 未完成的短擦保留可见擦痕；重开才清除，不让颜料在松手后重新长回去。
            WetPaint.GetPropertyBlock(paintBlock);
            paintBlock.SetFloat("_Wipe", Mathf.Max(retained, preview));
            WetPaint.SetPropertyBlock(paintBlock);
            // 前缘受指尖压力约束，后缘滞后形成滚褶；底面始终在板上方，不能穿入木头。
            Vector3 local = Cloth.transform.InverseTransformPoint(contact);
            for (int i = 0; i < rest.Length; i++)
            {
                float u = uv[i].x, v = uv[i].y;
                Vector3 onBoard = local + new Vector3((u - .5f) * .13f, .002f + Mathf.Sin(v * Mathf.PI) * .018f,
                                                      (v - .5f) * .12f);
                float lag = 1 - Mathf.Pow(v, 2) * .30f;
                Vector3 desired = Vector3.Lerp(rest[i], onBoard, pressure * lag);
                points[i] = Vector3.Lerp(points[i], desired, 1 - Mathf.Exp(-dt * (17 - v * 5)));
            }
            mesh.vertices = points;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
