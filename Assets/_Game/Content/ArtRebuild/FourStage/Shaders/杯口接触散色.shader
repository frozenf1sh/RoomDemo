// 文件：洗笔接触.shader。职责：在真实杯口液面验证有限蓝色丝带的接触、展开与可读性。
// 边界：有限程序化色带原型，不声称完成GPU流体；不影响陶杯外壁。
Shader "RoomDemo/Atelier/FourStageWash"
{
    Properties { _ProbeProgress("散色进度",Range(0,1))=0 _ProbeTime("视觉时间",Float)=0 _ProbeStrength("颜料可读性",Range(0,2))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/AtelierErasureField.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float _ProbeProgress,_ProbeTime,_ProbeStrength;
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            V Vert(A i) { V o; o.world=TransformObjectToWorld(i.positionOS); o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                AtelierClip(i.world);
                float2 p=(i.uv-.5)*2;
                float r=length(p),a=atan2(p.y,p.x);
                float front=smoothstep(0,.14,_ProbeProgress)*smoothstep(.98,.84,r);
                float growth=lerp(.13,.92,smoothstep(0,1,_ProbeProgress));
                // 扭曲和疏密打断等距螺旋；这里只验证有限色丝，不求解流体。
                float distortion=sin(p.x*9+p.y*4)*.65+sin(p.y*13-p.x*3)*.38;
                float twist=a+r*9-_ProbeTime*.24+distortion;
                float ribbon=pow(saturate(.5+.5*sin(twist*1.6)),4);
                float fine=pow(saturate(.5+.5*sin(twist*3.1+r*7)),14)*.35;
                float broken=.45+.55*saturate(sin(p.x*17+sin(p.y*11)*2)*.5+.5);
                float cloud=exp(-dot(p-float2(.12,.06),p-float2(.12,.06))*3)*.23;
                float trail=((ribbon+fine)*broken+cloud)*(1-smoothstep(growth-.15,growth+.05,r));
                float seed=exp(-dot(i.uv-float2(.70,.54),i.uv-float2(.70,.54))*110);
                float pigment=saturate((trail*.9+seed*(1-_ProbeProgress)*1.1)*front*_ProbeStrength);
                float ripple=sin(length(i.uv-float2(.70,.54))*92-_ProbeTime*7)*exp(-_ProbeTime*.8)*_ProbeProgress;
                float3 normal=normalize(float3(ripple*.04,1,ripple*.025));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float fresnel=pow(1-saturate(dot(normal,view)),4);
                float3 water=float3(.018,.035,.052)+SampleSH(normal)*.06+fresnel*float3(.07,.10,.14);
                float3 blue=lerp(float3(.018,.045,.16),float3(.035,.15,.42),ribbon);
                float3 colour=lerp(water,blue,pigment*.88);
                colour+=pigment*float3(.003,.012,.036)+fresnel*.025;
                return half4(colour,1);
            }
            ENDHLSL
        }
    }
}
