// 艺术植物专用表面：细脉、笔触色差、沿枝条显现与柔软风动。
// 所有参数只服务表现，Shader 不读取领域事实。
Shader "Game/ArtistPlant"
{
    Properties
    {
        _BaseColor("叶片底色", Color) = (0.13,0.24,0.06,1)
        _TipColor("叶尖与细脉", Color) = (0.38,0.49,0.12,1)
        _EmissionColor("梦境细脉", Color) = (0,0,0,1)
        _Reveal("枝叶显现", Range(0,1)) = 1
        _Unfurl("叶片展开", Range(0,1)) = 1
        _Fade("轮廓替换", Range(0,1)) = 1
        _WindStrength("柔软风动", Range(0,0.2)) = 0.025
        _LeafMode("叶片表面", Float) = 1
        _Smoothness("湿润高光", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "PainterlyPigment.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor, _TipColor, _EmissionColor;
        float _Reveal, _Unfurl, _Fade, _WindStrength, _LeafMode, _Smoothness;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;
            half fog:TEXCOORD3;
        };
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
        }
        float3 Deform(Attributes input)
        {
            float3 p=input.positionOS.xyz;
            // 未展开的叶片围绕叶脉收拢，枝茎自身不使用这项弯曲。
            p.x*=lerp(1,lerp(.14,1,_Unfurl),_LeafMode);
            p.y+=sin(input.uv.y*PI)*.20*(1-_Unfurl)*_LeafMode;
            float3 world=TransformObjectToWorld(p);
            float wave=sin(_Time.y*1.18+world.x*1.7+world.z*.73)+sin(_Time.y*.67+world.z*2.1)*.35;
            p.x+=wave*_WindStrength*input.uv.y*input.uv.y;
            p.z+=sin(_Time.y*.86+world.x*2.3)*_WindStrength*.4*input.uv.y;
            return p;
        }
        void RevealClip(float2 uv)
        {
            // 完全未显现时拒绝所有像素；随机细边避免笔直的切割线。
            clip(_Reveal-.001);
            float grain=Noise(uv*float2(26,40));
            clip(_Reveal-uv.y+grain*.035);
            clip(_Fade-grain*.96-.002);
        }
        Varyings Vertex(Attributes input)
        {
            Varyings o;
            o.positionWS=TransformObjectToWorld(Deform(input));
            o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);
            o.uv=input.uv;
            o.fog=ComputeFogFactor(o.positionCS.z);
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            half4 Fragment(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                RevealClip(i.uv);
                half3 normal=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
                Light key=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float veins=exp(-abs(i.uv.x-.5)*34);
                veins+=pow(saturate(1-abs(frac(i.uv.y*9+abs(i.uv.x-.5)*3)-.5)*8),3)*.22;
                float brush=Noise(i.uv*float2(40,8));
                float variation=saturate(i.uv.y*.36+brush*.19+veins*.26);
                half3 albedo=lerp(_BaseColor.rgb,_TipColor.rgb,variation*_LeafMode+.12*(1-_LeafMode));
                albedo=PainterlyPigment(albedo,i.positionWS,normal);
                float diffuse=saturate(dot(normal,key.direction));
                diffuse=lerp(diffuse,floor(diffuse*5+.5)/5,.24);
                half3 illumination=SampleSH(normal)*.82+key.color*(.18+diffuse*.82)*key.shadowAttenuation;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                {
                    Light light=GetAdditionalLight(lightIndex,i.positionWS);
                    illumination+=light.color*light.distanceAttenuation*(.15+saturate(dot(normal,light.direction))*.85);
                }
                LIGHT_LOOP_END
                #endif
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float sheen=pow(saturate(dot(normal,normalize(view+key.direction))),lerp(12,75,_Smoothness));
                half3 color=albedo*illumination+key.color*sheen*_Smoothness*.16;
                color+=_EmissionColor.rgb*(veins*.7+brush*.1);
                float edge=(1-smoothstep(0,.055,abs(_Reveal-i.uv.y)))*step(.005,_Reveal)*step(_Reveal,.985);
                color+=edge*float3(.44,.65,.14);
                color=MixFog(color,i.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            half4 DepthFragment(Varyings i):SV_Target { RevealClip(i.uv); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            float3 _LightDirection;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings o=Vertex(input);
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,_LightDirection));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 DepthFragment(Varyings i):SV_Target { RevealClip(i.uv); return 0; }
            ENDHLSL
        }
    }
}
