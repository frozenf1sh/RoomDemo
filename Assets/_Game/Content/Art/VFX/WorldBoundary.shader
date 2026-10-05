// 文件：WorldBoundary.shader。职责：实体墙面先被星纹侵入，再沿连续噪声边界溶解。
// 边界：纯渲染参数，不包含领域状态；共用材质由各 Renderer 的属性块独立驱动。
Shader "Game/WorldBoundary"
{
    Properties
    {
        _BaseMap ("表面纹理", 2D) = "white" {}
        _BaseColor ("实体颜色", Color) = (0.42,0.39,0.32,1)
        _Dissolve ("边界溶解", Range(0,1)) = 0
        _StarExposure ("星光侵蚀", Range(0,1)) = 0
        _NoiseScale ("侵蚀尺度", Float) = 1.15
        _EdgeWidth ("柔软边缘", Range(0.01,0.3)) = 0.095
        _EdgeColor ("星光边缘", Color) = (1.3,1.75,2.2,1)
        _BrushStrength ("笔触调制", Range(0,1)) = 0.12
        _WindStrength ("外景风动", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PainterlyPigment.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _EdgeColor;
            float _Dissolve, _StarExposure, _NoiseScale, _EdgeWidth, _BrushStrength, _WindStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };
            float Hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            float Field(float3 p) { return Noise(p)*.57+Noise(p*2.17+13)*.28+Noise(p*4.31+27)*.15; }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                // 室外草木使用同一表面显影；顶端微动不影响建筑的零风动材质。
                float weight=saturate(v.positionOS.y*.5);
                v.positionOS.xz += float2(sin(_Time.y*.9+v.positionOS.y*1.9),cos(_Time.y*.67+v.positionOS.x*2))*weight*_WindStrength*.15;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float3 p=v.positionWS*_NoiseScale;
                float field=Field(p*float3(.72,2.1,.72)+float3(0,sin(p.x*.8+p.z*.7)*.28,0));
                float threshold=lerp(-.075,1.10,_Dissolve);
                clip(field-threshold);
                float edge=(1-smoothstep(0,_EdgeWidth,field-threshold))*smoothstep(0,.05,_Dissolve);
                float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb*_BaseColor.rgb;
                float brush=Noise(p*float3(7,2,7)); base*=lerp(1, .81+brush*.34,_BrushStrength);
                base=PainterlyPigment(base,v.positionWS,normalize(v.normalWS));
                half3 n=normalize(v.normalWS);
                Light main=GetMainLight(TransformWorldToShadowCoord(v.positionWS));
                half3 irradiance=SampleSH(n)+main.color*(saturate(dot(n,main.direction))*.82+.18)*main.distanceAttenuation*main.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                InputData inputData=(InputData)0;
                inputData.positionWS=v.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                uint lightCount=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                {
                    Light light=GetAdditionalLight(lightIndex,v.positionWS);
                    irradiance+=light.color*(saturate(dot(n,light.direction))*.8+.2)*light.distanceAttenuation;
                }
                LIGHT_LOOP_END
                #endif
                float3 lit=base*max(irradiance,float3(.045,.045,.055));
                // 噪声脊线如颜料裂隙，先有星纹，再有破口；不是把墙染成蓝色。
                float ridge=1-smoothstep(.017,.044,abs(field-.49));
                float speck=pow(saturate(Noise(p*29)-.58)*2.38,12);
                float stroke=.5+.5*sin(p.y*9+sin(p.x*1.2+p.z)*2.2);
                float3 starSurface=lerp(float3(.05,.08,.15),float3(.14,.24,.29),field)+ridge*.26*lerp(float3(.37,.54,.65),float3(.87,.69,.34),stroke)+speck*.42;
                lit=lerp(lit,starSurface,saturate(_StarExposure)*.87);
                lit+=edge*_EdgeColor.rgb*(.26+.16*sin(_Time.y*.23+field*15));
                lit=MixFog(lit,v.fog);
                return half4(lit,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _EdgeColor;
            float _Dissolve, _StarExposure, _NoiseScale, _EdgeWidth, _BrushStrength, _WindStrength;
            CBUFFER_END
            float3 _LightDirection, _LightPosition;
            struct ShadowAttributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct ShadowVaryings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            float ShadowHash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float ShadowNoise(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(ShadowHash(i),ShadowHash(i+float3(1,0,0)),f.x),lerp(ShadowHash(i+float3(0,1,0)),ShadowHash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(ShadowHash(i+float3(0,0,1)),ShadowHash(i+float3(1,0,1)),f.x),lerp(ShadowHash(i+float3(0,1,1)),ShadowHash(i+1),f.x),f.y),f.z);
            }
            ShadowVaryings ShadowVert(ShadowAttributes v)
            {
                ShadowVaryings o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                float3 direction=_LightDirection;
                #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
                direction=normalize(_LightPosition-o.positionWS);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,TransformObjectToWorldNormal(v.normalOS),direction));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #endif
                return o;
            }
            half4 ShadowFrag(ShadowVaryings v):SV_Target
            {
                // 星光侵蚀也打开阴影：天空不能已经显露，地面却仍有完整天花板的阴影。
                float3 p=v.positionWS*_NoiseScale;
                p=p*float3(.72,2.1,.72)+float3(0,sin(p.x*.8+p.z*.7)*.28,0);
                float field=ShadowNoise(p)*.57+ShadowNoise(p*2.17+13)*.28+ShadowNoise(p*4.31+27)*.15;
                clip(field-lerp(-.075,1.10,_Dissolve)); return 0;
            }
            ENDHLSL
        }
    }
}
