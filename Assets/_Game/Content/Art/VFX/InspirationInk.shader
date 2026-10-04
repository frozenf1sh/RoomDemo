// 纸面墨迹与浅金墨光：只出现在一个短语附近，不形成按钮或奖励卡片。
Shader "Game/Art/InspirationInk"
{
    Properties
    {
        [MainColor] _BaseColor("墨色", Color) = (0.24,0.16,0.08,1)
        _Glow("悬停墨光", Range(0,1)) = 0
        _InkReveal("墨迹渗开", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Glow, _InkReveal;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(123.7,269.1)))*43758.5453); }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=(i.uv-.5)*float2(1,1.8);
                float grain=Hash(floor(i.uv*180));
                float ink=saturate(1-length(p)*2.0+grain*.12);
                float edge=smoothstep(.26,.04,abs(p.y));
                float radius=.05+_InkReveal*.77;
                float spread=(1-smoothstep(radius,radius+.045,length(p)+grain*.035))*smoothstep(0,.12,_InkReveal);
                float halo=exp(-dot(p,p)*8)*_Glow;
                float alpha=ink*edge*spread*.18+halo*.10;
                half3 color=lerp(_BaseColor.rgb,half3(1,.76,.32),_Glow);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
