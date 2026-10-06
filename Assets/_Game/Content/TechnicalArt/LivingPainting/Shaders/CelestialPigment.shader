// 文件：CelestialPigment.shader。职责：绘制作者布置的三维天空色带与星点。
// 只输出网格顶点的普通底色；笔触语言仍由冻结的 painterly renderer 产生。
Shader "RoomDemo/LivingPainting/CelestialPigment"
{
    Properties { _BaseColor("底色",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float4 _BaseColor;
        struct Input { float3 positionOS:POSITION; float3 normalOS:NORMAL; float4 colour:COLOR; };
        struct Output { float4 positionCS:SV_POSITION; float4 colour:TEXCOORD0; float3 normal:TEXCOORD1; };
        Output Vertex(Input i) { Output o;o.positionCS=TransformObjectToHClip(i.positionOS);o.colour=i.colour*_BaseColor;o.normal=TransformObjectToWorldNormal(i.normalOS);return o; }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            half4 Fragment(Output i):SV_Target { return i.colour; }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 Fragment(Output i):SV_Target
            {
                float3 n=normalize(i.normal);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
