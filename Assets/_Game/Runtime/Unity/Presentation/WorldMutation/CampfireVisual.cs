// =============================================================================
// 文件：CampfireVisual.cs
// 职责：为按需生成的篝火提供少量火舌运动、暖光与空间声音。
// 边界：只做视觉循环，不添加 campfire Fact，不参与世界反应。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    public sealed class CampfireVisual : MonoBehaviour
    {
        [SerializeField] private Transform[] flames;
        [SerializeField] private Light glow;
        [SerializeField] private AudioSource crackle;
        private Vector3[] sizes;
        public void Configure(Transform[] fire, Light light, AudioSource sound) { flames = fire; glow = light; crackle = sound; }
        private void Awake()
        {
            // 缓存初始形状，每帧只插值已有网格，不生成材质或粒子。
            sizes = new Vector3[flames.Length]; for (int i = 0; i < flames.Length; i++) sizes[i] = flames[i].localScale;
        }
        private void OnEnable() { crackle.Play(); }
        private void Update()
        {
            for (int i = 0; i < flames.Length; i++) flames[i].localScale = Vector3.Scale(sizes[i], new Vector3(1, 1 + Mathf.Sin(Time.time * (3 + i) + i * 2) * .18f, 1));
            glow.intensity = 3.3f + Mathf.Sin(Time.time * 6) * .25f;
        }
        private void OnDisable() { crackle.Stop(); }
    }
}
