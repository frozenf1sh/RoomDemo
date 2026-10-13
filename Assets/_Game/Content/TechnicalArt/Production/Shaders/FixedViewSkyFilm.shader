// 文件：FixedViewSkyFilm.shader。职责：以原 Hero 的 UV、颜色与笔触绘制固定画幅。
// 边界：输出给专用绘画摄像机；不包含球面投影、全景拼接或随机微光。
Shader "RoomDemo/Production/FixedViewSkyFilm"
{
    Properties
    {
        _Pigment("实时颜料",2D)="black"{}
        _PigmentNext("交接颜料",2D)="black"{}
        _SkyHistoryBlend("历史交接",Range(0,1))=0
        _Aspect("画幅比例",Float)=1.7777778
        _Mode("天空模式",Float)=0
        _StarGain("暖星",Range(0,1))=1
        _CloudPaint("颜料显现",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../LivingMatter/Shaders/PigmentField.hlsl"
            TEXTURE2D(_Pigment);SAMPLER(sampler_Pigment);float4 _Pigment_TexelSize;
            float _StarGain,_CloudPaint;
            float3 SkyStarLayer(float3 colour,float2 uv);
            #include "ApprovedSkyPaint.hlsl"
            #include "OrdinarySky.hlsl"
            struct Input { float3 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
            Output Vertex(Input i)
            {
                Output o;o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;return o;
            }
            half4 Fragment(Output i):SV_Target
            {
                half4 painted=LivingSkyPaint(i.uv);
                if(_CloudPaint<.999)
                {
                    float3 direction=normalize(float3((i.uv-.5)*float2(1.7777778,1),1));
                    painted.rgb=lerp(SkyStarLayer(OrdinarySkyColour(direction),i.uv),painted.rgb,_CloudPaint);
                }
                return painted;
            }
            ENDHLSL
        }
    }
}
