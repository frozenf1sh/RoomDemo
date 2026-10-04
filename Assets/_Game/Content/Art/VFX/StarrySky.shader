// 文件：StarrySky.shader。职责：在完整天空穹顶上绘制分层星点、夜色与月亮。
// 边界：纯原创数学图案，没有 AI、外部纹理、实时反射或世界规则。
Shader "Game/StarrySky"
{
    Properties
    {
        _Zenith ("天顶夜色", Color) = (0.012, 0.024, 0.065, 1)
        _Horizon ("远处夜色", Color) = (0.075, 0.12, 0.20, 1)
        _Stars ("星点亮度", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Front ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith, _Horizon;
            float _Stars;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.direction = input.positionOS.xyz; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float StarLayer(float2 uv, float2 density, float cutoff)
            {
                // 星点位置由格子种子固定；微弱闪烁仅是已显示天空的动画。
                float2 cell = floor(uv * density), local = frac(uv * density);
                float seed = Hash(cell); float2 center = float2(Hash(cell+31),Hash(cell+83))*.66+.17;
                float distance = length(local-center); float pixel = max(fwidth(distance),.01);
                return (1-smoothstep(.027,.027+pixel,distance))*step(cutoff,seed) * (.8+.2*sin(_Time.y*.7+seed*40));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float2 uv = float2(atan2(d.z,d.x)/6.2831853+.5,acos(clamp(d.y,-1,1))/3.1415927);
                float3 color = lerp(_Zenith.rgb,_Horizon.rgb,exp(-max(d.y,0)*4));
                float stars = StarLayer(uv,float2(360,180),.970)+StarLayer(uv+.173,float2(720,360),.991)*.5;
                color += stars * _Stars * smoothstep(-.03,.16,d.y) * float3(.75,.84,1);
                float moon = smoothstep(.99960,.99982,dot(d,normalize(float3(-.3,.22,1))));
                color += moon*float3(.62,.70,.76);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
