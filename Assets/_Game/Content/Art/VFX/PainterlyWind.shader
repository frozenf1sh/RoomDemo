// 文件：PainterlyWind.shader。职责：统一树林、草叶与岩石的笔触色彩、雾和轻微顶点风动。
// 边界：风动是表现动画；材质不读取世界事实，不改变碰撞与交互状态。
Shader "Game/PainterlyWind"
{
    Properties
    {
        _BaseMap ("表面纹理", 2D) = "white" {}
        _BaseColor ("自然色彩", Color) = (0.14,0.28,0.22,1)
        _ShadowColor ("阴影色彩", Color) = (0.055,0.10,0.15,1)
        _TrunkColor ("近地枝干色彩", Color) = (0.20,0.14,0.09,1)
        _TrunkHeight ("树干范围：负数关闭", Float) = -1
        _WindStrength ("轻风幅度", Range(0,1)) = 0.28
        _WindSpeed ("风速", Range(0,3)) = 0.75
        _BrushStrength ("笔触调制", Range(0,1)) = 0.35
        _Dissolve ("外景显影", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
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
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _ShadowColor, _TrunkColor;
            float _WindStrength, _WindSpeed, _BrushStrength, _TrunkHeight, _Dissolve;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };
            float Hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world=TransformObjectToWorld(v.positionOS.xyz);
                float movement=saturate(v.positionOS.y*.72);
                float gust=sin(_Time.y*_WindSpeed+world.x*.41+world.z*.33)+sin(_Time.y*.53+world.z*.51)*.38;
                world.x+=gust*_WindStrength*.22*movement;
                world.z+=cos(_Time.y*_WindSpeed*.79+world.x*.35)*_WindStrength*.13*movement;
                o.positionWS=world; o.positionCS=TransformWorldToHClip(world);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings v) : SV_Target
            {
                float field=Noise(v.positionWS*.91)*.68+Noise(v.positionWS*2.31+17)*.32;
                clip(field-lerp(-.075,1.1,_Dissolve));
                float grain=Noise(v.positionWS*float3(9,2.7,9));
                float3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb*_BaseColor.rgb;
                albedo*=lerp(1,.80+grain*.43,_BrushStrength);
                if(_TrunkHeight>0) albedo=lerp(_TrunkColor.rgb,albedo,smoothstep(_TrunkHeight*.58,_TrunkHeight,v.positionWS.y));
                half3 normal=normalize(v.normalWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.positionWS));
                float diffuse=saturate(dot(normal,sun.direction));
                float softSteps=lerp(diffuse,floor(diffuse*5+.5)/5,.34);
                float3 light=SampleSH(normal)+sun.color*(softSteps*.8+.2)*sun.distanceAttenuation*sun.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                InputData inputData=(InputData)0;
                inputData.positionWS=v.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
                uint lightCount=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                {
                    Light local=GetAdditionalLight(lightIndex,v.positionWS);
                    light+=local.color*(saturate(dot(normal,local.direction))*.8+.2)*local.distanceAttenuation;
                }
                LIGHT_LOOP_END
                #endif
                float3 color=albedo*max(light,float3(.045,.06,.08));
                color+=_ShadowColor.rgb*.17*(1-diffuse);
                return half4(MixFog(color,v.fog),1);
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
            float4 _BaseMap_ST, _BaseColor, _ShadowColor, _TrunkColor;
            float _WindStrength, _WindSpeed, _BrushStrength, _TrunkHeight, _Dissolve;
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
                ShadowVaryings o;
                float3 world=TransformObjectToWorld(v.positionOS.xyz);
                float movement=saturate(v.positionOS.y*.72);
                float gust=sin(_Time.y*_WindSpeed+world.x*.41+world.z*.33)+sin(_Time.y*.53+world.z*.51)*.38;
                world.x+=gust*_WindStrength*.22*movement;
                world.z+=cos(_Time.y*_WindSpeed*.79+world.x*.35)*_WindStrength*.13*movement;
                o.positionWS=world;
                float3 direction=_LightDirection;
                #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
                direction=normalize(_LightPosition-world);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(v.normalOS),direction));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
                #endif
                return o;
            }
            half4 ShadowFrag(ShadowVaryings v):SV_Target
            {
                float field=ShadowNoise(v.positionWS*.91)*.68+ShadowNoise(v.positionWS*2.31+17)*.32;
                clip(field-lerp(-.075,1.1,_Dissolve)); return 0;
            }
            ENDHLSL
        }
    }
}
