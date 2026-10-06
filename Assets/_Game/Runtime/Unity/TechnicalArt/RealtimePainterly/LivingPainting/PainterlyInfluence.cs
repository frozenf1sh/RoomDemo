// 文件：PainterlyInfluence.cs
// 职责：注册指定物体或世界空间椭球的局部绘画权重。
// 边界：仅供合成 Pass 读取；不替换普通材质，不修改领域状态。
using System.Collections.Generic;
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>Renderer 模式准确限制到物体像素；区域模式按世界坐标传播。</summary>
    public sealed class PainterlyInfluence : MonoBehaviour
    {
        internal static readonly List<PainterlyInfluence> Active = new List<PainterlyInfluence>();
        [Header("指定物体；为空时使用世界区域")]
        public Renderer[] Targets;
        public Vector3 Radii = Vector3.one;
        [Range(0, 1)] public float PainterlyIntensity = .65f;
        [Range(.02f, 1)] public float Feather = .45f;
        public Camera OnlyCamera;
        [Tooltip("保护已创作的颜料线，避免基础取色将细线取成周围木纹。")]
        public bool PreserveOriginal;

        private void OnEnable()
        {
            // 去重注册，停止或删除实验物体时释放影响，不留下全局残影。
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        /// <summary>设置局部绝对强度；合成使用局部与全局的最大值，不累加变亮。</summary>
        public void SetPainterlyIntensity(float value) => PainterlyIntensity = Mathf.Clamp01(value);

        internal bool AppliesTo(Camera camera) => isActiveAndEnabled && (PainterlyIntensity > 0 || PreserveOriginal)
            && gameObject.scene == camera.gameObject.scene && (OnlyCamera == null || OnlyCamera == camera);

        private void OnDrawGizmosSelected()
        {
            if (Targets != null && Targets.Length > 0) return;
            Gizmos.color = new Color(.35f,.65f,.7f,.5f);
            // 区域按世界轴对齐，不借用物体旋转；大小明确以米为单位。
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Radii);
            Gizmos.DrawWireSphere(Vector3.zero,1);
        }
    }
}
