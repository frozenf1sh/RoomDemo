// 文件：AtelierWashWater.shader。职责：受杯沿约束的洗笔水、接触水纹和实时颜料高光。
// 边界：颜料读取既有 GPU 平流数据；水波只在液面内，不生成出杯流束。
Shader "RoomDemo/Atelier/ErasureWashWater"
{
    Properties
    {
        _AtelierEraseGroup("擦除表面组",Float)=0
        _Pigment("运行时颜料",2D)="black"{}
        _WaterColor("洗笔水底色",Color)=(.12,.15,.14,1)
        _Flow("持续流动",Range(0,1))=0
        _Motion("液面时间",Float)=0
        _ContactAge("接触经过秒数",Float)=100
        _Contact("笔尖接触强度",Float)=0
        _LightGain("色纹发光",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
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
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            TEXTURE2D(_Pigment); SAMPLER(sampler_Pigment);
            float4 _WaterColor;
            float _Flow,_Motion,_ContactAge,_Contact,_LightGain;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float3 normal:TEXCOORD2; };
            float Height(float2 uv)
            {
                float2 d=(uv-.5)*2;
                float r=length(d), edge=saturate(1-r*r);
                float2 contact=uv-float2(.70,.54);
                float distance=length(contact);
                float pulse=sin(distance*94-_ContactAge*10)*exp(-distance*5)*exp(-_ContactAge*.75);
                float wash=sin(distance*65-_Motion*8)*_Contact;
                float swirl=sin(atan2(d.y,d.x)*2+r*12-_Motion*1.2)*.0012*r;
                // 最大波高低于杯沿余量，靠壁处只留下不足一毫米的弯月面。
                return edge*edge*(pulse*.0018+wash*.0008+swirl*_Flow)+smoothstep(.84,1,r)*.0007;
            }
            Varyings Vert(Attributes i)
            {
                Varyings o; float3 p=i.positionOS; p.y+=Height(i.uv);
                o.world=TransformObjectToWorld(p); o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv;
                const float e=.003; float h=Height(i.uv);
                float3 n=normalize(float3((h-Height(i.uv+float2(e,0)))/(e*.284),1,(h-Height(i.uv+float2(0,e)))/(e*.284)));
                o.normal=TransformObjectToWorldNormal(n); return o;
            }
            #include "AtelierErasureField.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                AtelierClip(i.world);
                float4 paint=SAMPLE_TEXTURE2D(_Pigment,sampler_Pigment,i.uv);
                float density=saturate((paint.r+paint.g)*7);
                float3 tint=lerp(float3(.025,.14,.17),float3(.033,.054,.10),paint.g/max(.001,paint.r+paint.g));
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(_WaterColor.rgb,tint,density*.82);
                surface.alpha=1; surface.smoothness=lerp(.96,.87,density);
                surface.occlusion=1; surface.normalTS=float3(0,0,1);
                surface.emission=tint*density*_LightGain;
                InputData input=(InputData)0;
                input.positionWS=i.world; input.normalWS=normalize(i.normal);
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                input.shadowCoord=TransformWorldToShadowCoord(i.world);
                input.bakedGI=SampleSH(input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
    }
}
