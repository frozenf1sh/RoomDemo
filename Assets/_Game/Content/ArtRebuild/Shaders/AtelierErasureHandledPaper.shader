// 文件：AtelierHandledPaper.shader。职责：旧火柴纸盒的纤维、磨损印色与粗糙受光。
// 边界：项目程序材质；不使用概念图或外部未授权图像，仍响应实际光源与阴影。
Shader "RoomDemo/Atelier/ErasureHandledPaper"
{
    Properties
    {
        _AtelierEraseGroup("擦除表面组",Float)=0
        _BaseColor("旧纸或印墨色",Color)=(.49,.38,.23,1)
        _Smoothness("表面平滑度",Range(0,1))=.03
    }
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            float4 _BaseColor;float _Smoothness;
            struct Attributes { float3 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;float3 normalWS:TEXCOORD2;};
            Varyings Vert(Attributes v)
            {
                Varyings o;o.world=TransformObjectToWorld(v.positionOS);o.positionCS=TransformWorldToHClip(o.world);
                o.local=v.positionOS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            #include "AtelierErasureField.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                AtelierClip(i.world);
                float3 p=i.local;
                float fibres=sin(p.x*16400+sin(p.z*800)*2)*sin(p.z*14100+p.y*12000);
                float fleck=frac(sin(dot(floor(p*9800),float3(19.13,63.17,27.31)))*43758.5453);
                float stain=.5+.5*sin(p.x*170+p.z*210+sin(p.z*375)*.4);
                float wear=smoothstep(.93,1,fleck)*.11;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=_BaseColor.rgb*(.89+.10*stain+fibres*.027)+wear*float3(.17,.13,.08);
                surface.smoothness=_Smoothness;surface.alpha=1;surface.occlusion=1;surface.normalTS=float3(0,0,1);
                InputData input=(InputData)0;input.positionWS=i.world;
                input.normalWS=normalize(i.normalWS+float3(fibres*.024,0,(fleck-.5)*.018));
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);input.shadowCoord=TransformWorldToShadowCoord(i.world);
                input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
