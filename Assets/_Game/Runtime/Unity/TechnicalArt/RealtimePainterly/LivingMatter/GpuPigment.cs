// 文件：GpuPigment.cs
// 职责：拥有 Hero 颜料的浮点反馈纹理，按固定时间步执行高阶平流。
// 边界：仅绘画物质的表现状态；不写游戏事实，不修改共享材质或全局管线。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>两个中间缓冲与两个历史缓冲实现 MacCormack；暂停时真正保留颜料状态。</summary>
    public sealed class GpuPigment : MonoBehaviour
    {
        public Shader SimulationShader;
        public Renderer Target;
        /// <summary>可选上游颜料；桌面湿膜直接接收杯中当前的浓度。</summary>
        public GpuPigment Feed;
        [Tooltip("0 星空，1 杯内，2 桌面湿膜")]
        public int Mode;
        public int Width=1024,Height=576;
        public float Flow=1,Injection;
        public bool AutomaticTime=true;
        public float VisualTime;
        private RenderTexture state,next,forward,backward;
        private Material material;
        private MaterialPropertyBlock properties;
        private float accumulator;

        /// <summary>当前真实模拟纹理；由组件拥有，调用方不得销毁。</summary>
        public RenderTexture Pigment => state;

        private void OnEnable()
        {
            if(!Application.isPlaying || SimulationShader==null) return;
            material=new Material(SimulationShader) { hideFlags=HideFlags.HideAndDontSave };
            state=Buffer("历史颜料");next=Buffer("新颜料");
            forward=Buffer("正向平流");backward=Buffer("反向校正");
            properties=new MaterialPropertyBlock();
            ResetPigment();
        }

        private RenderTexture Buffer(string label)
        {
            var texture=new RenderTexture(Width,Height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            { name=label,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
            texture.Create();return texture;
        }

        /// <summary>清空历史并恢复程序性初态；没有导入画作或离线油画贴图。</summary>
        public void ResetPigment()
        {
            if(material==null) return;
            VisualTime=0;accumulator=0;SetUniforms();
            Graphics.Blit(null,state,material,0);
            Apply();
        }

        private void Update()
        {
            if(AutomaticTime) Advance(Time.unscaledDeltaTime);
        }

        /// <summary>推进指定的真实秒数；固定 30 Hz，单帧最多补四步避免卡顿时爆量。</summary>
        /// <param name="seconds">演出经过的时间，暂停时传入零。</param>
        public void Advance(float seconds)
        {
            if(material==null) return;
            accumulator+=Mathf.Min(seconds,.1333f);
            int count=0;
            while(accumulator>=1f/30 && count++<4)
            {
                const float step=1f/30;
                VisualTime+=step;SetUniforms();
                material.SetFloat("_Delta",step);Graphics.Blit(state,forward,material,1);
                material.SetFloat("_Delta",-step);Graphics.Blit(forward,backward,material,1);
                material.SetFloat("_Delta",step);
                material.SetTexture("_OldPigment",state);material.SetTexture("_Backward",backward);
                Graphics.Blit(forward,next,material,2);
                // 只交换自己的纹理引用，不覆盖输入，也不做 CPU 像素回读。
                (state,next)=(next,state);accumulator-=step;
            }
            Apply();
        }

        private void SetUniforms()
        {
            material.SetFloat("_Mode",Mode);material.SetFloat("_Aspect",(float)Width/Height);
            material.SetFloat("_Flow",Flow);material.SetFloat("_VisualTime",VisualTime);
            material.SetFloat("_Injection",Injection);
            if(Feed!=null && Feed.Pigment!=null) material.SetTexture("_FeedPigment",Feed.Pigment);
            // 显式提供网格间距；限制器的四个旧样本必须落在真实像素中心。
            material.SetVector("_MainTex_TexelSize",new Vector4(1f/Width,1f/Height,Width,Height));
        }

        private void Apply()
        {
            if(Target==null) return;
            Target.GetPropertyBlock(properties);properties.SetTexture("_Pigment",state);
            properties.SetFloat("_VisualTime",VisualTime);properties.SetFloat("_Flow",Flow);
            properties.SetFloat("_Aspect",(float)Width/Height);properties.SetFloat("_Mode",Mode);
            Target.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            foreach(var texture in new[]{state,next,forward,backward})
                if(texture!=null) { texture.Release();Destroy(texture); }
            if(material!=null) Destroy(material);
        }
    }
}
