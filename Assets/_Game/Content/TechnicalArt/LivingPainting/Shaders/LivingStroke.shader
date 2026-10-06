// 文件：LivingStroke.shader。职责：已有几何笔触上的克制色泽移动、路径光、末端延伸。
// 画风继续来自冻结的 renderer。此材质只定义局部颜料，不影响其他普通 Renderer。
Shader "RoomDemo/LivingPainting/Stroke"
{
    Properties
    {
        _BaseColor("颜料颜色",Color)=(.08,.34,.42,1)
        _WarmColor("路径暖光",Color)=(1,.72,.30,1)
        _Flow("流动",Range(0,1))=0
        _Light("点亮",Range(0,1))=0
        _Extend("延伸",Range(0,1))=.38
        _PainterlyIntensity("绘画强度",Range(0,1))=.27
        _StrokeLength("路径长度（米）",Float)=3
        _VisualTime("演出时间",Float)=0
        _LightDistance("光经过的距离（米）",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        Pass
        {
            Name "PigmentStroke"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _WarmColor;
            float _Flow, _Light, _Extend, _PainterlyIntensity, _StrokeLength, _VisualTime, _LightDistance;
            CBUFFER_END
            struct Input { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Output Vertex(Input i)
            {
                Output o;
                o.positionWS=TransformObjectToWorld(i.positionOS);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.uv=i.uv;
                return o;
            }
            half4 Fragment(Output i):SV_Target
            {
                float arc=i.uv.x;
                float side=i.uv.y;
                // 末端沿弧长渐进，不对整条线做 dissolve。边缘纹理固定在几何 UV。
                float end=_StrokeLength*_Extend;
                float tip=1-smoothstep(end-.045,end,arc);
                float bristles=.91+.06*sin(arc*37+sin(side*17))+.03*sin(arc*101+side*13);
                clip(bristles*tip-abs(side));
                float travel=arc-_VisualTime*.018*_Flow;
                float pigment=.94+.04*sin(travel*13+side*3)+.02*sin(travel*29-side*7);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float diffuse=saturate(dot(normalize(i.normalWS),sun.direction));
                half3 colour=_BaseColor.rgb*pigment*(.65+sun.color*diffuse*sun.shadowAttenuation*.55);
                // 一处宽而柔和的暖光经过颜料；不设置随机闪点、硬边条带或闪烁。
                float lightArc=fmod(max(_LightDistance,0),max(end+.45,.46))-.12;
                float pulse=exp(-pow((arc-lightArc)/.16,2))*pow(saturate(1-abs(side)),.7);
                colour+=_WarmColor.rgb*pulse*_Light*.85;
                colour*=1+_PainterlyIntensity*.035*sin(arc*63+side*14);
                return half4(colour,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "StrokeDepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            float _StrokeLength, _Extend;
            struct Input { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            Output Vertex(Input i) { Output o; o.positionCS=TransformObjectToHClip(i.positionOS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o; }
            half4 Fragment(Output i):SV_Target
            {
                float tip=1-smoothstep(_StrokeLength*_Extend-.045,_StrokeLength*_Extend,i.uv.x);
                float edge=.91+.06*sin(i.uv.x*37+sin(i.uv.y*17))+.03*sin(i.uv.x*101+i.uv.y*13);
                clip(edge*tip-abs(i.uv.y));
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(n); return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
