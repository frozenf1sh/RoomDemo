// =============================================================================
// 文件：AtelierWashPresenter.cs
// 职责：将洗笔事实表现为从杯口落下的笔尖运动、有限注色和受限水纹。
// 边界：不提交命令、不授予观察；只拥有本组件创建的网格副本和材质属性块。
// =============================================================================
using Game.Application;
using Game.Domain;
using Game.Unity.TechnicalArt.RealtimePainterly;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>小型洗笔罐适配器，复用 GpuPigment，不绑定旧杯水的溢流装置。</summary>
    public sealed class AtelierWashPresenter : MonoBehaviour
    {
        public GpuPigment Pigment;
        public Renderer Water;
        public Transform SupportedBrush;
        public MeshFilter BrushSurface;
        public float BristleEnd = -.00136f;
        public float BristleStart = -.00095f;
        private GameSession session;
        private MaterialPropertyBlock properties;
        private Mesh brushMesh;
        private Vector3[] original, bent;
        private Quaternion supportedRotation, dryRotation;
        private float washAge = 100, contactAge = 100, motion, flow;
        private bool washed;
        private bool injectThisContact;
        /// <summary>笔尖完成本次接触后才允许再次观察液面的表现门槛，不改变领域事实。</summary>
        public bool ContactPlaying => washAge < 4.2f;

        /// <summary>显式绑定新会话；重开清除颜料历史和正在进行的接触动画。</summary>
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            session.EventRaised += OnEvent;
            properties = new MaterialPropertyBlock();
            supportedRotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-.80f, -.57f, .20f).normalized);
            dryRotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-.85f, .48f, .21f).normalized);
            if (brushMesh == null && BrushSurface != null)
            {
                brushMesh = Instantiate(BrushSurface.sharedMesh);
                brushMesh.name = "洗笔画笔_运行时笔毛变形";
                brushMesh.MarkDynamic();
                original = brushMesh.vertices; bent = (Vector3[])original.Clone();
                BrushSurface.sharedMesh = brushMesh;
            }
            Pigment.AutomaticTime = false;
            Pigment.Flow = Pigment.Injection = 0;
            Pigment.ResetPigment();
            washAge = contactAge = 100; motion = flow = 0; washed = false;
            Apply(0);
        }

        private void OnEvent(DomainEvent value)
        {
            if (value is FactAdded fact && fact.Fact == StudioRules.Washed)
            {
                washAge = 0; contactAge = 100; washed = true; injectThisContact = true;
            }
        }

        /// <summary>重复亲手洗笔只再激起水纹；已经洗下的颜料不会每次凭空增加。</summary>
        public void RinseAgain()
        {
            if (!washed || ContactPlaying) return;
            washAge = 0; contactAge = 100; injectThisContact = false;
        }

        private void Update()
        {
            if (session == null) return;
            float dt = Time.deltaTime;
            washAge += dt; contactAge += dt;
            float desired = session.State.HasFact(StudioRules.WaterFlow) ? 1 : 0;
            flow = Mathf.MoveTowards(flow, desired, dt * .45f);
            Apply(dt);
        }

        private void Apply(float dt)
        {
            // 笔杆以杯口支点转动；接触和注色从笔毛入水以后开始，离水后注色归零。
            float press = Smooth(.35f, .8f, washAge) * (1 - Smooth(3.1f, 4.0f, washAge));
            float stroke = Mathf.Sin(Mathf.Max(0, washAge - .75f) * 5.2f) * press;

            // 杯沿是固定支点；笔毛从水面上方划弧入水，笔杆另一端相应抬起，不穿过陶壁。
            SupportedBrush.localRotation = Quaternion.Slerp(dryRotation, supportedRotation, press) * Quaternion.Euler(0, stroke * 2.2f, 0);
            float tipHeight = BrushSurface.transform.TransformPoint(new Vector3(brushMesh.bounds.min.x, 0, 0)).y;
            float contact = Mathf.Clamp01((Water.transform.position.y + .002f - tipHeight) / .012f);
            if (washed && contactAge > 90 && contact > .1f) contactAge = 0;
            if (brushMesh != null)
            {
                for (int i = 0; i < original.Length; i++)
                {
                    float weight = Smooth(BristleStart, BristleEnd, original[i].x);
                    bent[i] = original[i] + new Vector3(0, weight * weight * contact * -.000035f, weight * weight * stroke * contact * .000025f);
                }
                brushMesh.vertices = bent;
                brushMesh.RecalculateNormals();
                brushMesh.RecalculateBounds();
            }
            // 注入只持续这次洗笔；异常解释只搬运已有颜料，不凭空持续增加质量。
            Pigment.Flow = Mathf.Max(flow, contact * .32f);
            Pigment.Injection = injectThisContact ? contact * .65f : 0;
            if (Pigment.Flow > .001f || Pigment.Injection > .001f) Pigment.Advance(dt);
            motion += dt * Mathf.Max(.05f, Pigment.Flow);
            Water.GetPropertyBlock(properties);
            properties.SetFloat("_Contact", contact);
            properties.SetFloat("_ContactAge", contactAge);
            properties.SetFloat("_Motion", motion);
            properties.SetFloat("_Flow", flow);
            properties.SetFloat("_LightGain", session.State.HasFact(StudioRules.WaterLit) ? .45f : 0);
            Water.SetPropertyBlock(properties);
        }

        private static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));
        private void OnDestroy()
        {
            if (session != null) session.EventRaised -= OnEvent;
            if (brushMesh != null) Destroy(brushMesh);
        }
    }
}
