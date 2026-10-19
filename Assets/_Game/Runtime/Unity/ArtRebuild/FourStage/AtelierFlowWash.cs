// 文件：AtelierFlowWash.cs
// 职责：接收洗笔输入，以真实笔尖接触作为命令提交点，并读取结果恢复散色。
// 边界：不是延时发奖的演出；只有玩家发起且仍有效的接触才进入GameSession。
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity.ArtRebuild
{
    /// <summary>沿用已验收的杯口与色丝；材质、属性块和指针请求均有明确所有者。</summary>
    public sealed class AtelierFlowWash : MonoBehaviour
    {
        public Renderer Water;
        public Transform Brush;
        public MeshFilter Bristles;
        public Shader PigmentShader;
        private GameSession session;
        private Material original, owned;
        private MaterialPropertyBlock block;
        private Quaternion dry;
        private bool requested, submitted;
        private float age, progress, clock;
        /// <summary>接触动作仍在进行时不沿旧轮廓显示金丝。</summary>
        public bool Moving => requested;
        /// <summary>绑定新会话时克隆材质；不覆盖原杯水资产。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            block = new MaterialPropertyBlock();
            original = Water.sharedMaterial;
            owned = new Material(original) { shader = PigmentShader, name = "主流程_自有洗笔色丝" };
            Water.sharedMaterial = owned;
            dry = Quaternion.FromToRotation(Vector3.right,new Vector3(-.85f,.48f,.21f).normalized);
            Brush.localRotation = dry;
            progress = session.State.HasFact(AtelierFlowRules.Washed) ? 1 : 0;
        }
        /// <summary>先核对已调查来源；开始动作不提前写洗笔事实。</summary>
        public bool RequestContact()
        {
            if (session == null || requested || !session.IsActionAvailable(StudioRules.Cup,"wash_release_pigment")) return false;
            requested = true; submitted = false; age = 0;
            return true;
        }
        /// <summary>暂停、失焦或退出近景取消尚未发生的接触；已成立的结果保留。</summary>
        public void CancelContact() { requested = false; if (Brush != null) Brush.localRotation = dry; }
        private void Update()
        {
            if (session == null) return;
            float dt = Time.deltaTime;
            clock += dt;
            if (requested)
            {
                age += dt;
                float contact = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.55f,age)) * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.1f,2.8f,age)));
                var down = Quaternion.FromToRotation(Vector3.right,new Vector3(-.80f,-.57f,.20f).normalized);
                Brush.localRotation = Quaternion.Slerp(dry,down,contact);
                float tip = Bristles.transform.TransformPoint(new Vector3(Bristles.sharedMesh.bounds.min.x,0,0)).y;
                // 这是本次输入的几何接触判断；停止或中断不会再从旧计时器发命令。
                if (!submitted && tip < Water.bounds.center.y + .008f)
                {
                    session.PerformAction(StudioRules.Cup,"wash_release_pigment");
                    submitted = true;
                }
                if (age >= 2.8f) CancelContact();
            }
            if (session.State.HasFact(AtelierFlowRules.Washed)) progress = Mathf.MoveTowards(progress,1,dt/1.35f);
            Water.GetPropertyBlock(block);
            block.SetFloat("_ProbeProgress",progress); block.SetFloat("_ProbeTime",clock); block.SetFloat("_ProbeStrength",1);
            Water.SetPropertyBlock(block);
        }
        private void OnDisable() => CancelContact();
        private void OnDestroy()
        {
            if (Water != null && original != null) { Water.sharedMaterial = original; Water.SetPropertyBlock(null); }
            if (owned != null) Destroy(owned);
        }
    }
}
