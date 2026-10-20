// 文件：AtelierDraftCamera.cs
// 职责：提供逐物件累积显色、黑场失焦与独立屋顶揭示的纯视觉参数。
// 边界：不访问GameState；旁白完成与交互许可由场景适配器判断。
using System;
using UnityEngine;
namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>明确的世界显色体积；仅用于窗外连续景物，不扩大房间内的物件包围体。</summary>
    [Serializable]
    public sealed class AtelierDraftRevealVolume
    {
        public Transform Anchor;
        public Vector3 Size = Vector3.one;
        [Range(1,4)] public int Stage = 3;
    }
    /// <summary>物件脚边的水平承托面显色；锚点与阶段许可都由场景显式配置。</summary>
    [Serializable]
    public sealed class AtelierDraftContactRegion
    {
        public Transform Anchor;
        [Range(1,4)] public int Stage = 1;
        public Vector2 Radii = new Vector2(.3f,.25f);
        [Range(.005f,.06f)] public float HeightTolerance = .025f;
    }

    /// <summary>世界锚定的显色清单；阶段推进不会解除整片背景的遮挡。</summary>
    public sealed class AtelierDraftCamera : MonoBehaviour
    {
        [Range(0,1)] public float LeftDraft = 1;
        [Range(0,1)] public float WindowDraft = 1;
        [Range(0,1)] public float SurroundingsDraft = 1;
        [Range(0,1)] public float Density = 1;
        [Range(0,1)] public float Ghost;
        public Renderer[] ClearObjects = Array.Empty<Renderer>();
        public Renderer[] StageTwoObjects = Array.Empty<Renderer>();
        public Renderer[] StageThreeObjects = Array.Empty<Renderer>();
        public Renderer[] StageFourObjects = Array.Empty<Renderer>();
        // 屋顶单列，任何RestoreStage调用都不能顺带开放屋顶。
        public Renderer[] RoofObjects = Array.Empty<Renderer>();
        public AtelierDraftContactRegion[] ContactRegions = Array.Empty<AtelierDraftContactRegion>();
        public AtelierDraftRevealVolume[] RevealVolumes = Array.Empty<AtelierDraftRevealVolume>();
        private readonly float[] weights = new float[5];
        private int targetStage;
        private float roofWeight;
        private bool roofRequested;
        private float openingAge = 100;
        /// <summary>已请求显色的阶段；不会授予对应领域来源。</summary>
        public int VisibleStage => targetStage;
        /// <summary>用于输入交接，渐显完成后才能点击新阶段物件。</summary>
        public bool RevealAnimating => targetStage > 0 && weights[targetStage] < .999f;
        /// <summary>开场黑场量，只影响世界，Overlay旁白仍可显示。</summary>
        public float OpeningDarkness { get; private set; }
        /// <summary>失焦归零后不再执行多点采样。</summary>
        public float OpeningBlur { get; private set; }
        /// <summary>只有房间擦开事实后使用，不作为普通阶段的全景显色入口。</summary>
        public void ReleaseForWorld(float erasure) => Density=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(erasure*3));
        /// <summary>旁白整组完成后调用；保留此前显色，未配置的阶段不解除背景遮挡。</summary>
        public void RestoreStage(int stage, bool immediate)
        {
            targetStage = Mathf.Clamp(stage,0,4);
            LeftDraft = WindowDraft = SurroundingsDraft = 1;
            if (immediate)
                for (int index = 1; index <= 4; index++) weights[index] = index <= targetStage ? 1 : 0;
        }
        /// <summary>未来屋顶流程在试刮许可与对应旁白完成后显式调用，与普通阶段清单隔离。</summary>
        public void RevealRoof() => roofRequested = true;
        /// <summary>读取冻结当前帧所需的对象与显色权重，包围体仍跟随真实几何。</summary>
        public Renderer[] ObjectsForStage(int stage) => stage == 1 ? ClearObjects : stage == 2 ? StageTwoObjects
            : stage == 3 ? StageThreeObjects : stage == 4 ? StageFourObjects : RoofObjects;
        /// <summary>阶段0专供独立屋顶组读取。</summary>
        public float WeightForStage(int stage) => stage == 0 ? roofWeight : Mathf.SmoothStep(0,1,weights[stage]);
        /// <summary>反射摄像机复制当前视觉许可，镜内也不能提前看清未来物件或屋顶。</summary>
        public void CopyVisualState(AtelierDraftCamera source)
        {
            ClearObjects = source.ClearObjects;
            StageTwoObjects = source.StageTwoObjects;
            StageThreeObjects = source.StageThreeObjects;
            StageFourObjects = source.StageFourObjects;
            RoofObjects = source.RoofObjects;
            ContactRegions = source.ContactRegions;
            RevealVolumes = source.RevealVolumes;
            targetStage = source.targetStage;
            for (int index = 0; index < weights.Length; index++) weights[index] = source.weights[index];
            roofWeight = source.roofWeight;
            roofRequested = source.roofRequested;
            LeftDraft = source.LeftDraft;
            WindowDraft = source.WindowDraft;
            SurroundingsDraft = source.SurroundingsDraft;
            Density = source.Density;
            Ghost = 0;
            OpeningDarkness = OpeningBlur = 0;
            openingAge = 100;
        }
        /// <summary>进场全底稿，N01播放中不提前露出第一阶段物件。</summary>
        public void PlayOpening()
        {
            openingAge = 0;
            OpeningDarkness = OpeningBlur = 1;
            roofRequested = false;
            roofWeight = 0;
            RestoreStage(0,true);
        }
        private void Update()
        {
            openingAge += Time.deltaTime;
            OpeningDarkness = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,2.4f,openingAge));
            OpeningBlur = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,4.8f,openingAge));
            Ghost = openingAge < 1.1f ? Mathf.Sin(openingAge / 1.1f * Mathf.PI) * .18f : 0;
            // 每组只向自己的目标移动，新增物件显色不会让此前物件褪回草稿。
            for (int index = 1; index <= 4; index++)
                weights[index] = Mathf.MoveTowards(weights[index],index <= targetStage ? 1 : 0,Time.deltaTime/1.4f);
            roofWeight = Mathf.MoveTowards(roofWeight,roofRequested ? 1 : 0,Time.deltaTime/1.4f);
        }
    }
}
