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
        /// <summary>新流程沿用既有星空材质与参数，只改用明确的两次刮开事实门槛。</summary>
        public bool FourStageFlow;
        public Vector3 FlowRoofOrigin;
        public Vector2 FlowRoofSize = new Vector2(1.35f,1.35f);
        public Renderer[] RoofSurfaces, FrameSurfaces;
        /// <summary>第一次擦除暂留的外景、画架与真实作品。</summary>
        public Renderer[] FinalSurfaces=System.Array.Empty<Renderer>();
        /// <summary>贴在实物上的便签文字等世界UI随室内退去，屏幕笔记和字幕不受影响。</summary>
        public GameObject[] InteriorOverlays=System.Array.Empty<GameObject>();
        private float finalErase;
        private bool cloudShown;
        /// <summary>新流程独立视觉Prefab的明确表面；不依赖它们刚好挂在历史美术根下。</summary>
        public Renderer[] AdditionalSurfaces=System.Array.Empty<Renderer>();
        public Shader ErasureUnlit;
        public LivingSkyController Sky;
        public Shader ErasureLit, ErasurePaper, ErasureWater, ErasurePainting, ErasureFlame, ErasureTerrain,
            FinaleSky;
        private Material originalSky;
        public Light[] LocalLights;
        private GameSession session;
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<string, Material> owned = new Dictionary<string, Material>();
        private float roof, roofRaw, room, frame, mark;
        private bool lit, flowing, roofStarted, wholeStarted;
        private float[] intensities;
        /// <summary>仅用于输入屏蔽尚未落稳的表面；不是事实门槛。</summary>
        public bool RoofSettled => roof > .998f;
        /// <summary>完整演出完成后仍保留自由看天空与暂停菜单。</summary>
        public bool InteriorSettled => room>.999f;
        public bool Finished => FourStageFlow?finalErase>.999f&&Sky.Flow>.99f:room>.999f;
        /// <summary>当前统一笔刷进度，供明确的开发取证采样。</summary>
        public float Erasure => room;

        /// <summary>接入新会话；清零视觉历史，不读取旧场景的预设状态。</summary>
        public void Bind(GameSession game)
        {
            session = game;
            roof = roofRaw = room = frame = mark = finalErase = 0;
            cloudShown=false;
            lit = flowing = roofStarted = wholeStarted = false;
            intensities = new float[LocalLights.Length];
            for (int i = 0; i < LocalLights.Length; i++)
                intensities[i] = LocalLights[i].intensity;
            ResetGlobals();
            originalSky = Sky.Surface.sharedMaterial;
            if (!FourStageFlow)
            {
                var skyMaterial=new Material(originalSky) { name="玩法全方向运行时程序化星夜",shader=FinaleSky };
                owned.Add("sky",skyMaterial);
                Sky.Surface.sharedMaterial=skyMaterial;
            }
            // 只克隆玩法副本实际用到的材质，原正式资产不被改写。
            InstallMaterials(ArtRoot);
            InstallMaterials(GameplayRoot);
            InstallMaterials(AdditionalSurfaces);
            Sky.SeparateLightAndFlow = true;
            Sky.IndependentCloudPaint=FourStageFlow;
            // 新会话取消上一轮颜料历史；只恢复表现，不改变任何世界事实。
            Sky.ResetEffect();
            Sky.SetCoverage(0);
            Sky.SetFlow(0);
            Sky.LightTo(0, 0);
        }
        private void InstallMaterials(Transform root) => InstallMaterials(root.GetComponentsInChildren<Renderer>(true));
        private void InstallMaterials(IEnumerable<Renderer> surfaces)
        {
            foreach (var renderer in surfaces)
            {
                if (renderer == null || renderer == Sky.Surface || originals.ContainsKey(renderer))
                    continue;
                var source = renderer.sharedMaterials;
                var output = new Material[source.Length];
                originals.Add(renderer, source);
                float group = FourStageFlow&&System.Array.IndexOf(FinalSurfaces,renderer)>=0 ? 3 : System.Array.IndexOf(RoofSurfaces, renderer) >= 0    ? 1
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
            case "Universal Render Pipeline/Unlit":
                return ErasureUnlit;
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
            {
                // 屋顶开口先慢后快再收住；线性扩张会让大块木板像被匀速拽开。
                roofRaw = Mathf.MoveTowards(roofRaw, 1, dt / 7.5f);
                roof = Mathf.SmoothStep(0, 1, roofRaw);
            }
            if (state.HasFact(AtelierRules.FrameErased))
                frame = Mathf.MoveTowards(frame, 1, dt / 5.5f);
            if ((state.HasFact(StudioRules.SkyLit) || FourStageFlow && state.HasFact(AtelierFlowRules.SkyStarsPainted)) && !lit)
            {
                lit = true;
                Sky.RevealTo(1, 6);
                Sky.LightTo(1, 6);
            }
            // 云笔完成的同一帧就开始缓慢流动；房间余景仍按原时长退场，不再让星空等最后一块外景。
            if ((state.HasFact(StudioRules.SkyFlow) || FourStageFlow && state.HasFact(AtelierFlowRules.SkyFinished)) && !flowing)
            {
                flowing = true;
                Sky.RevealTo(1, 6);
                Sky.FlowTo(1, 6);
            }
            if (!FourStageFlow&&state.HasFact(StudioRules.FullSky))
                mark = Mathf.MoveTowards(mark, 1, dt / 5);
            if (state.HasFact(AtelierRules.RoomErased) && !wholeStarted)
            {
                wholeStarted = true;
                for (int i = 0; i < LocalLights.Length; i++)
                    intensities[i] = LocalLights[i].intensity;
            }
            if (wholeStarted)room = Mathf.MoveTowards(room,1,dt/(FourStageFlow?6:16));
            if(FourStageFlow&&state.HasFact(AtelierFlowRules.SkyCloudPainted)&&!cloudShown)
            {
                cloudShown=true;Sky.RevealTo(1,1.4f);Sky.CloudTo(1,1.4f);
            }
            if(FourStageFlow&&state.HasFact(AtelierFlowRules.SkyFinished))
                finalErase=Mathf.MoveTowards(finalErase,1,dt/3.5f);
            Shader.SetGlobalFloat("_AtelierFinal",finalErase);
            if(FourStageFlow && state.HasFact(AtelierFlowRules.RoofProbed) && state.AtelierCanvas.RoofProbe.Count>0)
            {
                var path=state.AtelierCanvas.RoofProbe;
                var a=path[0];var b=path[path.Count-1];
                Vector3 World(int u,int v)=>FlowRoofOrigin+new Vector3((u/10000f-.5f)*FlowRoofSize.x,0,(v/10000f-.5f)*FlowRoofSize.y);
                Shader.SetGlobalFloat("_AtelierFlowProbeEnabled",1);
                Shader.SetGlobalVector("_AtelierProbeFrom",World(a.U0,a.V0));
                Shader.SetGlobalVector("_AtelierProbeTo",World(b.U1,b.V1));
                Shader.SetGlobalFloat("_AtelierProbeRadius",.075f);
            }
            Shader.SetGlobalFloat("_AtelierRoof", roof);
            Shader.SetGlobalFloat("_AtelierFrame", frame);
            Shader.SetGlobalFloat("_AtelierRoom", room);
            Shader.SetGlobalFloat("_AtelierMark", mark);
        }
        private void LateUpdate()
        {
            if(FourStageFlow&&room>.999f)
            {
                // 特殊工具和描线可能不用裁剪Shader；全退完后统一停止残留的实体绘制。
                foreach(var pair in originals)
                {
                    if(pair.Key==null)continue;
                    bool retained=System.Array.IndexOf(FinalSurfaces,pair.Key)>=0;
                    if(!retained||finalErase>.999f)pair.Key.enabled=false;
                }
                foreach(var overlay in InteriorOverlays)if(overlay!=null)overlay.SetActive(false);
            }
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
        public bool RoofClear(Vector3 point)
        {
            if(!RoofSettled)return false;
            Vector2 q=new Vector2(point.x/2.94f,(point.z+.3f)/3.35f);
            if(!FourStageFlow)return q.magnitude<.87f;
            // 与共享裁剪场相同的边沿计算；已经擦去的实体遮挡必须同步让出点击天空的射线。
            float grain=Mathf.Sin(point.x*11.3f+point.z*3.7f+Mathf.Sin(point.y*9.2f))*.10f
                +Mathf.Sin(point.x*83+point.y*47+point.z*37)*.007f;
            return q.magnitude+grain*.13f+Mathf.Sin(Mathf.Atan2(q.y,q.x)*7)*.027f<.998f;
        }
        private static void ResetGlobals()
        {
            Shader.SetGlobalFloat("_AtelierFinal",0);
            Shader.SetGlobalFloat("_AtelierRoof", 0);
            Shader.SetGlobalFloat("_AtelierRoom", 0);
            Shader.SetGlobalFloat("_AtelierFrame", 0);
            Shader.SetGlobalFloat("_AtelierMark", 0);
            Shader.SetGlobalFloat("_AtelierFlowProbeEnabled",0);
            Shader.SetGlobalFloat("_AtelierProbeRadius",0);
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
