// 文件：PainterlyOpening.shader。职责：普通石膏面被一条有方向的刷痕擦开，露出后方。
// 采用 URP 标准 Lit 光照，所有可见性 Pass 共用同一裁剪规则，不产生纯色 dissolve 边缘。
Shader "RoomDemo/LivingPainting/Opening"
{
    Properties
    {
        _BaseColor("墙面颜色",Color)=(.79,.74,.64,1)
        _BaseMap("普通材质纹理",2D)="white"{}
        _Smoothness("光滑度",Range(0,1))=.05
        _Metallic("金属度",Range(0,1))=0
        _BumpMap("法线",2D)="bump"{}
        _BumpScale("法线强度",Float)=1
        _OcclusionMap("遮蔽",2D)="white"{}
        _OcclusionStrength("遮蔽强度",Float)=1
        _EmissionColor("自发光",Color)=(0,0,0,0)
        _EmissionMap("自发光纹理",2D)="white"{}
        _Cutoff("裁剪",Float)=.5
        _Rewrite("擦开",Range(0,1))=0
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
    float _Rewrite;
    float BrushCentre(float u) { return .47+.075*sin(u*5.1)+.022*sin(u*12.8); }
    void BrushClip(float2 uv)
    {
        if (any(uv<0) || any(uv>1)) return;
        float centre=BrushCentre(uv.x);
        float front=_Rewrite*1.12-.04;
        float width=.005+_Rewrite*.255;
        float edge=.008*sin(uv.x*93)+.004*sin(uv.x*213+uv.y*35);
        float mask=min(front-uv.x,width-abs(uv.y-centre)+edge);
        // 几何裁剪只在笔势连续扫过的范围内，关闭时返回完整墙面。
        clip(_Rewrite<.001 ? 1 : -mask);
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "OpenedLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment OpenFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            void OpenFragment(Varyings input,out half4 colour:SV_Target0)
            {
                BrushClip(input.uv);
                LitPassFragment(input,colour);
                // 擦开之前就能看到淡颜料线；裁剪和已有线共用同一中心路径。
                float guide=1-smoothstep(.002,.007,abs(input.uv.y-BrushCentre(input.uv.x)));
                if (any(input.uv<0) || any(input.uv>1)) guide=0;
                colour.rgb=lerp(colour.rgb,colour.rgb*half3(.28,.67,.72),guide*.48);
            }
            ENDHLSL
        }
        Pass
        {
            Name "OpenedDepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment OpenNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define _ALPHATEST_ON 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            void OpenNormals(Varyings input,out half4 normal:SV_Target0)
            {
                BrushClip(input.uv);
                DepthNormalsFragment(input,normal);
            }
            ENDHLSL
        }
        Pass
        {
            Name "OpenedDepth"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment OpenDepth
            #define _ALPHATEST_ON 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            half OpenDepth(Varyings input):SV_Target
            {
                BrushClip(input.uv);
                return DepthOnlyFragment(input);
            }
            ENDHLSL
        }
    }
}
