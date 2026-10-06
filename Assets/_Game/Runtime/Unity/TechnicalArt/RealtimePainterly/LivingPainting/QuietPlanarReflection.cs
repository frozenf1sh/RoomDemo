// 文件：QuietPlanarReflection.cs
// 职责：给独立 Hero 水面提供当前三维星夜的真实平面倒影。
// 边界：仅渲染资源；镜像摄像机排除水层，禁止递归和永久全局设置。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>通过 Unity 6 URP SingleCameraRequest 渲染镜像视角，没有预生成天空贴图。</summary>
    public sealed class QuietPlanarReflection : MonoBehaviour
    {
        public Camera Source;
        public Renderer Water;
        public float Height = .15f;
        /// <summary>可选的星夜演出时钟；指定后，水面运动随同一次暂停与重放。</summary>
        public QuietSkyMotion VisualClock;
        private Camera reflection;
        private RenderTexture texture;
        private MaterialPropertyBlock properties;
        private UniversalRenderPipeline.SingleCameraRequest request;
        private float nextFrame;

        private void OnEnable()
        {
            if (!Application.isPlaying || Source == null || Water == null) return;
            var node=new GameObject("水面镜像摄像机_运行时拥有");
            node.hideFlags=HideFlags.HideAndDontSave;
            reflection=node.AddComponent<Camera>();
            reflection.enabled=false;
            texture=new RenderTexture(1024,576,24,RenderTextureFormat.ARGBHalf) { name="星夜实时平面倒影",hideFlags=HideFlags.HideAndDontSave };
            texture.Create();
            properties=new MaterialPropertyBlock();
            request=new UniversalRenderPipeline.SingleCameraRequest { destination=texture };
            var data=reflection.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=false;
            data.antialiasing=AntialiasingMode.None;
            // 倒影先采集普通三维颜色，主摄像机统一绘画化；避免在水中重复滤波。
        }

        private void LateUpdate()
        {
            if (reflection==null || Source==null) return;
            if (Time.unscaledTime<nextFrame) return;
            nextFrame=Time.unscaledTime+1f/15;
            reflection.CopyFrom(Source);
            reflection.enabled=false;
            reflection.cullingMask=Source.cullingMask & ~(1<<4);
            reflection.targetTexture=texture;
            var mirror=Matrix4x4.identity;
            mirror.m11=-1;
            mirror.m13=Height*2;
            reflection.worldToCameraMatrix=Source.worldToCameraMatrix*mirror;
            var position=Source.transform.position;
            position.y=Height*2-position.y;
            reflection.transform.position=position;
            var normal=reflection.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            var point=reflection.worldToCameraMatrix.MultiplyPoint(new Vector3(0,Height+.015f,0));
            var plane=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal));
            reflection.projectionMatrix=Source.CalculateObliqueMatrix(plane);
            var data=reflection.GetUniversalAdditionalCameraData();
            data.antialiasing=AntialiasingMode.None;
            data.renderPostProcessing=false;
            bool previous=GL.invertCulling;
            try
            {
                GL.invertCulling=true;
                RenderPipeline.SubmitRenderRequest(reflection,request);
            }
            finally { GL.invertCulling=previous; }
            Water.GetPropertyBlock(properties);
            properties.SetTexture("_PlanarReflection",texture);
            // 倒影仍可重新采样镜头，但波纹只消费所属演出的时间，冻结后不继续滚动。
            properties.SetFloat("_VisualTime",VisualClock!=null ? VisualClock.VisualTime : Time.unscaledTime);
            Water.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            // 实验退出时只释放自己创建的隐藏资源，不触碰场景材质资产。
            if (texture!=null) { texture.Release(); Destroy(texture); }
            if (reflection!=null) Destroy(reflection.gameObject);
        }
    }
}
