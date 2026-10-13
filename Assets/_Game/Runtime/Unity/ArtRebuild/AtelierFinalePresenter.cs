// =============================================================================
// 文件：AtelierFinalePresenter.cs
// 职责：读取后半段事实，驱动连续世界笔刷、屋顶显露和既有 GPU 程序化星夜。
// 边界：不提交会话命令，不奖励观察，不移动房间；运行时材质由本组件拥有并释放。
// =============================================================================
using System.Collections.Generic;
using Game.Application;
using Game.Domain;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEngine;

namespace Game.Unity.ArtRebuild
{
    /// <summary>同一世界笔势裁剪实体、阴影和深度；开始前保留原有扫描材质。</summary>
    public sealed class AtelierFinalePresenter : MonoBehaviour
    {
        public Transform ArtRoot, GameplayRoot;
        public Renderer[] RoofSurfaces, FrameSurfaces;
        public LivingSkyController Sky;
        public Shader ErasureLit, ErasurePaper, ErasureWater, ErasurePainting, ErasureFlame, ErasureTerrain,
            FinaleSky;
        private Material originalSky;
        public Light[] LocalLights;
        private GameSession session;
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<string, Material> owned = new Dictionary<string, Material>();
        private float roof, room, frame, mark;
        private bool lit, flowing, roofStarted, wholeStarted;
        private float[] intensities;
        /// <summary>仅用于输入屏蔽尚未落稳的表面；不是事实门槛。</summary>
        public bool RoofSettled => roof > .998f;
        /// <summary>完整演出完成后仍保留自由看天空与暂停菜单。</summary>
        public bool Finished => room > .999f;
        /// <summary>当前统一笔刷进度，供明确的开发取证采样。</summary>
        public float Erasure => room;

        /// <summary>接入新会话；清零视觉历史，不读取旧场景的预设状态。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            roof = room = frame = mark = 0;
            lit = flowing = roofStarted = wholeStarted = false;
            intensities = new float[LocalLights.Length];
            for (int i = 0; i < LocalLights.Length; i++)
                intensities[i] = LocalLights[i].intensity;
            ResetGlobals();
            originalSky = Sky.Surface.sharedMaterial;
            var skyMaterial =
                new Material(originalSky) { name = "玩法全方向运行时程序化星夜", shader = FinaleSky };
            owned.Add("sky", skyMaterial);
            Sky.Surface.sharedMaterial = skyMaterial;
            // 只克隆玩法副本实际用到的材质，原正式资产不被改写。
            InstallMaterials(ArtRoot);
            InstallMaterials(GameplayRoot);
            Sky.SeparateLightAndFlow = true;
            // 新会话取消上一轮颜料历史；只恢复表现，不改变任何世界事实。
            Sky.ResetEffect();
            Sky.SetCoverage(0);
            Sky.SetFlow(0);
            Sky.LightTo(0, 0);
        }
        private void InstallMaterials(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == Sky.Surface || originals.ContainsKey(renderer))
                    continue;
                var source = renderer.sharedMaterials;
                var output = new Material[source.Length];
                originals.Add(renderer, source);
                float group = System.Array.IndexOf(RoofSurfaces, renderer) >= 0    ? 1
                              : System.Array.IndexOf(FrameSurfaces, renderer) >= 0 ? 2
                                                                                   : 0;
                for (int i = 0; i < source.Length; i++)
                {
                    var m = source[i];
                    if (m == null)
                    {
                        output[i] = null;
                        continue;
                    }
                    string key = m.GetEntityId() + ":" + group;
                    if (!owned.TryGetValue(key, out var clone))
                    {
                        clone = new Material(m) { name = m.name + "_玩法连续擦除" };
                        Shader replacement = SelectShader(m.shader.name);
                        if (replacement != null)
                            clone.shader = replacement;
                        // 原 URP 材质关闭新增颜料调制，保持颜色、法线、AO、金属度和透明混合。
                        if (m.shader.name == "Universal Render Pipeline/Lit")
                        {
                            clone.SetFloat("_PigmentStrength", 0);
                            clone.SetFloat("_WearStrength", 0);
                            clone.SetFloat("_PaintStains", 0);
                        }
                        if (clone.HasProperty("_AtelierEraseGroup"))
                            clone.SetFloat("_AtelierEraseGroup", group);
                        owned.Add(key, clone);
                    }
                    output[i] = clone;
                }
                renderer.sharedMaterials = output;
            }
        }
        private Shader SelectShader(string name)
        {
            switch (name)
            {
            case "Universal Render Pipeline/Lit":
            case "RoomDemo/Atelier/AgedPigmentLit":
                return ErasureLit;
            case "RoomDemo/Atelier/HandledPaper":
                return ErasurePaper;
            case "RoomDemo/Atelier/WashWater":
                return ErasureWater;
            case "RoomDemo/Atelier/TaskPainting":
                return ErasurePainting;
            case "RoomDemo/Atelier/WickFlame":
                return ErasureFlame;
            case "RoomDemo/Atelier/DistantTerrain":
                return ErasureTerrain;
            default:
                return null;
            }
        }
        private void Update()
        {
            if (session == null)
                return;
            float dt = Time.deltaTime;
            var state = session.State;
            if (state.HasFact(AtelierRules.RoofErased) && !roofStarted)
            {
                roofStarted = true;
                // 擦开屋顶先看见普通黑夜；点亮与流动都由玩家以后分别解释。
                Sky.SetCoverage(0);
            }
            if (roofStarted)
                roof = Mathf.MoveTowards(roof, 1, dt / 7.5f);
            if (state.HasFact(AtelierRules.FrameErased))
                frame = Mathf.MoveTowards(frame, 1, dt / 5.5f);
            if (state.HasFact(StudioRules.SkyLit) && !lit)
            {
                lit = true;
                Sky.RevealTo(1, 6);
                Sky.LightTo(1, 6);
            }
            if (state.HasFact(StudioRules.SkyFlow) && !flowing)
            {
                flowing = true;
                Sky.RevealTo(1, 6);
                Sky.FlowTo(1, 6);
            }
            if (state.HasFact(StudioRules.FullSky))
                mark = Mathf.MoveTowards(mark, 1, dt / 5);
            if (state.HasFact(AtelierRules.RoomErased) && !wholeStarted)
            {
                wholeStarted = true;
                for (int i = 0; i < LocalLights.Length; i++)
                    intensities[i] = LocalLights[i].intensity;
            }
            if (wholeStarted)
                room = Mathf.MoveTowards(room, 1, dt / 16);
            Shader.SetGlobalFloat("_AtelierRoof", roof);
            Shader.SetGlobalFloat("_AtelierFrame", frame);
            Shader.SetGlobalFloat("_AtelierRoom", room);
            Shader.SetGlobalFloat("_AtelierMark", mark);
        }
        private void LateUpdate()
        {
            if (!wholeStarted)
                return;
            for (int i = 0; i < LocalLights.Length; i++)
            {
                // 等本帧灯芯表现更新后，再随同一笔势熄去局部光源。
                float order = RoomOrder(LocalLights[i].transform.position);
                LocalLights[i].intensity =
                    intensities[i] *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(order - .03f, order + .02f, room * 1.2f)));
            }
        }
        /// <summary>与 Shader 相同的世界顺序，供输入拒绝被擦去的实体。</summary>
        public static float RoomOrder(Vector3 p)
        {
            float grain = Mathf.Sin(p.x * 11.3f + p.z * 3.7f + Mathf.Sin(p.y * 9.2f)) * .10f +
                          Mathf.Sin(p.x * 83 + p.y * 47 + p.z * 37) * .007f;
            return Mathf.Clamp01((4.7f - p.y) / 5.9f + (p.x + 4.5f) * .018f +
                                 Mathf.Sin(p.z * .62f + p.x * .5f) * .045f +
                                 Mathf.Sin(p.x * 1.85f + p.z * .34f) * .075f +
                                 Mathf.Sin(p.x * 3.1f - p.z * .24f) * .028f + grain * .035f);
        }
        /// <summary>仅跳过中央已经完全显露的屋顶拾取表面；边缘保守保留。</summary>
        public bool RoofClear(Vector3 point) => RoofSettled &&
                                                new Vector2(point.x / 2.94f, (point.z + .3f) / 3.35f).magnitude <
                                                    .87f;
        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat("_AtelierRoof", 0);
            Shader.SetGlobalFloat("_AtelierRoom", 0);
            Shader.SetGlobalFloat("_AtelierFrame", 0);
            Shader.SetGlobalFloat("_AtelierMark", 0);
        }
        private void OnDestroy()
        {
            ResetGlobals();
            if (Sky != null && Sky.Surface != null && originalSky != null)
                Sky.Surface.sharedMaterial = originalSky;
            foreach (var pair in originals)
                if (pair.Key != null)
                    pair.Key.sharedMaterials = pair.Value;
            foreach (var material in owned.Values)
                Destroy(material);
        }
    }
}
