// 文件：无光照表面统一擦除.shader
// 职责：保留独立蜡笔画与平涂物件原有颜色，并在全屋擦开时同步裁掉颜色、深度和法线。
// 边界：不生成纹理、不改原始素材；只在运行时由末阶段材质副本使用。
Shader "RoomDemo/Atelier/ErasureUnlit"
{
    Properties { _BaseMap("原表面",2D)="white"{} _BaseColor("原色",Color)=(1,1,1,1) _AtelierEraseGroup("擦除组",Float)=0 }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #define ATELIER_GROUP_DEFINED
    CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor;
    float _AtelierEraseGroup;
    CBUFFER_END
    #include "../../Shaders/AtelierErasureField.hlsl"
    TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
    struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
    struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
    V Vert(A i) {V o;o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
    half4 Colour(V i):SV_Target {AtelierClip(i.world);return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;}
    half4 Depth(V i):SV_Target {AtelierClip(i.world);return 0;}
    half4 Normals(V i):SV_Target {AtelierClip(i.world);return half4(normalize(i.normal),0);}
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Cull Off ZWrite On
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Colour
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            ENDHLSL
        }
    }
}
