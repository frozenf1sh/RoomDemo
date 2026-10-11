// 文件：AtelierDistantTerrain.shader。职责：以真实山体法线和世界纹理呈现远山体积与冷色层次。
// 边界：只负责已存在的三维远山表面，不加入全屏雾、贴片山线或静态天空。
Shader "RoomDemo/Atelier/ErasureDistantTerrain"
{
    Properties
    {
        _AtelierEraseGroup("擦除表面组",Float)=0
        _BaseMap("碎石与土壤底色",2D)="white"{}
        _BaseColor("阴面颜色",Color)=(.015,.025,.045,1)
        _RidgeColor("受光岩脊",Color)=(.06,.10,.16,1)
        _MoonDirection("月光方向",Vector)=(.25,.65,-.5,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_RidgeColor,_MoonDirection;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
            Varyings Vert(Attributes i)
            {
                Varyings o; o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normalOS);return o;
            }
            #include "AtelierErasureField.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                AtelierClip(i.world);
                float light=saturate(dot(normalize(i.normal),normalize(_MoonDirection.xyz))*.7+.24);
                float3 dirt=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz*.035).rgb;
                float strata=.85+.15*sin(i.world.y*.7+sin(i.world.x*.18));
                return half4(lerp(_BaseColor.rgb,_RidgeColor.rgb,light)*(0.65+dirt*.65)*strata,1);
            }
            ENDHLSL
        }
    }
}
