// 书页专用弯曲：横向细分网格按圆柱卷曲，进度只由检视表现层驱动。
Shader "Game/Art/BookPaper"
{
    Properties
    {
        [MainColor] _BaseColor("纸张颜色", Color) = (0.86,0.80,0.66,1)
        [MainTexture] _BaseMap("纸纤维", 2D) = "white" {}
        _Turn("翻页进度", Range(0,1)) = 0
        _PageWidth("纸页宽度", Float) = 1.47
        _BendStrength("纸张卷曲", Range(0,1)) = 1
        _StaticCurl("页边弧度", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Name "弯曲纸页"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Turn, _PageWidth, _BendStrength, _StaticCurl;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float fog:TEXCOORD2; float3 positionWS:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float curl = sin(_Turn * PI) * 2.78 * _BendStrength;
                float heading = _Turn * PI - curl * .5;
                float angle = heading + input.uv.x * curl;
                float3 n = float3(-sin(angle), 0, -cos(angle));
                if (curl > .0001)
                {
                    float radius = _PageWidth / curl;
                    p.x = radius * (sin(angle) - sin(heading));
                    p.z += radius * (cos(angle) - cos(heading));
                }
                else
                {
                    p.x *= cos(_Turn * PI);
                    p.z -= input.positionOS.x * sin(_Turn * PI);
                }
                // 静止时也留一条柔软的书脊弧线，避免开放页像硬质矩形板。
                p.z -= _StaticCurl * sin(input.uv.x * PI);
                output.positionCS = TransformObjectToHClip(p);
                output.positionWS = TransformObjectToWorld(p);
                output.normalWS = TransformObjectToWorldNormal(n);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 normal = normalize(input.normalWS);
                // 左页采用镜像网格；以视线而非绕序校正双面法线，避免一侧纸页被错误变暗。
                normal *= dot(normal,GetWorldSpaceViewDir(input.positionWS)) < 0 ? -1 : 1;
                Light key = GetMainLight();
                half diffuse = .46 + .54 * saturate(dot(normal, key.direction));
                half3 indirect = SampleSH(normal);
                color *= max(half3(.59,.57,.54), indirect * .75 + key.color * diffuse * .43);
                #ifdef _ADDITIONAL_LIGHTS
                    half3 localLight=0;
                    InputData inputData=(InputData)0;
                    inputData.positionWS=input.positionWS;
                    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                    uint count=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                    {
                        Light light=GetAdditionalLight(lightIndex,input.positionWS);
                        localLight+=light.color*light.distanceAttenuation*(.25+.75*saturate(dot(normal,light.direction)))*.23;
                    }
                    LIGHT_LOOP_END
                    color*=1+localLight;
                #endif
                // 内侧略暗、外缘略亮，近景层叠纸张的厚度因而容易读出。
                color *= lerp(.90,1.03,smoothstep(0,.12,input.uv.x));
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
