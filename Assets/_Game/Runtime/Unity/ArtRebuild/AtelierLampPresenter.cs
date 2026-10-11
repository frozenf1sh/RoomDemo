// =============================================================================
// 文件：AtelierLampPresenter.cs
// 职责：表现有接触依据的划火柴、裸露灯芯引燃及局部暖光，不代替玩家取得观察。
// 边界：只消费会话事实；手持位置和燃烧余时属于当前输入演示，不向会话提交命令。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>扫描油灯保持原位，火苗由锚定灯芯的程序网格与 Shader 绘制。</summary>
    public sealed class AtelierLampPresenter : MonoBehaviour
    {
        public Transform Wick, Striker, MatchTip;
        public Renderer WickSurface, LampFlame, MatchFlame, MatchHead;
        public Light LampLight, MatchLight;
        public Vector3 PointerPlaneNormal = Vector3.forward;
        private GameSession session;
        private MaterialPropertyBlock flameProperties, wickProperties, matchProperties, headProperties;
        private float lampAge = 100, strikeAge = 100, clock;
        private bool held, striking, touching;
        private Vector3 desiredTip, tipVelocity;
        /// <summary>当前选中的手持火柴，仅是输入状态，不授予物体。</summary>
        public bool HasMatch => held;
        /// <summary>有限的火柴接触输入窗口；划亮历史由领域另行记录。</summary>
        public bool HasBurningMatch => held && striking && strikeAge > .36f && strikeAge < 7.5f;
        /// <summary>灯芯亮度正在稳定，只用于暂缓变化后的画面观察。</summary>
        public bool IgnitionPlaying => session != null && session.State.HasFact(StudioRules.LampTurned) && lampAge < 1.8f;
        /// <summary>真实火柴头已经靠近裸露灯芯，不能仅点远处灯座引燃。</summary>
        public bool CanTouchWick => HasBurningMatch && Vector3.Distance(MatchTip.position, Wick.position) < .04f;

        /// <summary>绑定并恢复最终事实；重开不残留火柴、光源或事件订阅。</summary>
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            session.EventRaised += OnEvent;
            flameProperties = new MaterialPropertyBlock();
            wickProperties = new MaterialPropertyBlock();
            matchProperties = new MaterialPropertyBlock();
            headProperties = new MaterialPropertyBlock();
            lampAge = game.State.HasFact(StudioRules.LampTurned) ? 100 : -1;
            strikeAge = 100;
            held = striking = touching = false;
            clock = 0;
            MatchTip.gameObject.SetActive(false);
            Apply();
        }

        private void OnEvent(DomainEvent value)
        {
            if (!(value is FactAdded fact)) return;
            if (fact.Fact == AtelierRules.MatchStruck) StrikeAgain();
            if (fact.Fact == StudioRules.LampTurned) lampAge = 0;
        }

        /// <summary>亲点纸盒后拿取新火柴；不自动划亮或点灯。</summary>
        public void TakeMatch()
        {
            held = true; striking = touching = false; strikeAge = 100;
            desiredTip = Striker.position + new Vector3(0, .055f, -.025f);
            MatchTip.position = desiredTip; tipVelocity = Vector3.zero;
            MatchTip.gameObject.SetActive(true);
        }

        /// <summary>重复亲点砂纸可划另一根；不重复添加历史事实或路线次数。</summary>
        public void StrikeAgain()
        {
            if (!held || (striking && strikeAge < .65f)) return;
            striking = true; touching = false; strikeAge = 0;
        }

        /// <summary>从近景真实指针更新手持位置；视线离开近景不让火柴跟随到别的物体。</summary>
        public void SetPointer(Ray ray, bool lampFocused)
        {
            if (!held || !lampFocused || touching || (striking && strikeAge < .65f)) return;
            var plane = new Plane(PointerPlaneNormal, Wick.position + Vector3.back * .014f);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            // 限制在点灯工作簇的空闲空间，不能把燃烧火柴拖进桌面、墙或灯碗。
            point.x = Mathf.Clamp(point.x, Wick.position.x - .21f, Wick.position.x + .34f);
            point.y = Mathf.Clamp(point.y, Striker.position.y + .008f, Wick.position.y + .20f);
            desiredTip = point;
        }

        /// <summary>输入命中灯芯时保持小范围接触姿势；事实仍由输入适配器提交。</summary>
        public void HoldAtWick()
        {
            touching = true;
            desiredTip = Wick.position + new Vector3(.005f, .002f, -.007f);
        }

        /// <summary>返回房间时结束当前手持演示；不会清除划火柴或点灯的历史事实。</summary>
        public void PutMatchDown()
        {
            held = striking = touching = false;
            MatchTip.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (session == null) return;
            float dt = Time.deltaTime;
            clock += dt;
            if (lampAge >= 0) lampAge += dt;
            strikeAge += dt;
            if (held)
            {
                if (striking && strikeAge < .65f)
                {
                    // 火柴头沿砂纸的实体表面划过，再抬起；接触段没有凭空亮火。
                    float slide = Smooth(0, .28f, strikeAge);
                    float lift = Smooth(.29f, .65f, strikeAge);
                    desiredTip = Striker.position + Vector3.right * Mathf.Lerp(-.021f, .021f, slide)
                        + new Vector3(0, .001f + lift * .047f, -.002f - lift * .018f);
                    MatchTip.position = desiredTip;
                }
                else MatchTip.position = Vector3.SmoothDamp(MatchTip.position, desiredTip, ref tipVelocity, .045f, 2, dt);
                if (touching && lampAge > 1.3f)
                {
                    // 引燃后火柴退开并熄灭，炭化木杆仍在手持位置，不在画面中央凭空消失。
                    touching = false; strikeAge = Mathf.Max(strikeAge,7.5f);
                    desiredTip = Wick.position + new Vector3(.035f,-.025f,-.045f);
                }
            }
            Apply();
        }

        private void Apply()
        {
            float heat = session.State.HasFact(StudioRules.LampTurned) ? Smooth(.12f, 1.5f, lampAge) : 0;
            float flicker = 1 + Mathf.Sin(clock * 8.7f) * .025f + Mathf.Sin(clock * 13.1f) * .014f;
            LampFlame.GetPropertyBlock(flameProperties);
            flameProperties.SetFloat("_Burn", heat);
            flameProperties.SetFloat("_FlameClock", clock);
            LampFlame.SetPropertyBlock(flameProperties);
            LampLight.enabled = heat > .001f;
            // 微小灯芯只照亮近处，避免灯颈和瓷面被点光源冲成白块。
            LampLight.intensity = heat * flicker * .26f;
            WickSurface.GetPropertyBlock(wickProperties);
            wickProperties.SetColor("_EmissionColor", new Color(.72f, .12f, .012f) * heat);
            WickSurface.SetPropertyBlock(wickProperties);
            float matchBurn = held && striking ? Smooth(.28f, .45f, strikeAge) * (1 - Smooth(6.8f, 7.5f, strikeAge)) : 0;
            MatchFlame.GetPropertyBlock(matchProperties);
            matchProperties.SetFloat("_Burn", matchBurn);
            matchProperties.SetFloat("_FlameClock", clock + 1.7f);
            MatchFlame.SetPropertyBlock(matchProperties);
            MatchLight.enabled = matchBurn > .001f;
            MatchLight.intensity = matchBurn * .035f;
            MatchHead.GetPropertyBlock(headProperties);
            float charred = striking ? Smooth(.6f,4, strikeAge) : 0;
            headProperties.SetColor("_BaseColor",Color.Lerp(new Color(.25f,.065f,.034f),new Color(.025f,.019f,.012f),charred));
            MatchHead.SetPropertyBlock(headProperties);
        }

        private static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
        private void OnDestroy()
        {
            if (session != null) session.EventRaised -= OnEvent;
        }
    }
}
