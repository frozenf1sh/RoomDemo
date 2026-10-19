// 文件：原始图样笔印.shader。职责：将一枚原始RGBA笔刷按位置、大小、旋转叠入作品。
// 边界：只绘制本次有限笔印，不持续沿鼠标路径喷涂，不生成标准构图。
Shader "Hidden/RoomDemo/Atelier/Stamp"
{
    Properties { _Brush("原始图样",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
            float4 _Placement,_StampScale,_AlphaRect;
            struct A { float3 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
            V Vert(A i) { V o;o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;return o; }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv-_Placement.xy;
                float c=cos(_Placement.w),s=sin(_Placement.w);
                float2 uv=float2(c*p.x+s*p.y,-s*p.x+c*p.y)/max(float2(.001,.001),_StampScale.xy)+.5;
                clip(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                return SAMPLE_TEXTURE2D(_Brush,sampler_Brush,_AlphaRect.xy+uv*_AlphaRect.zw);
            }
            ENDHLSL
        }
    }
}
