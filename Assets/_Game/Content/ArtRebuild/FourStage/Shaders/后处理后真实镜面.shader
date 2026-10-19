// 文件：后处理后真实镜面.shader
// 职责：在真实镜面轮廓内合成当前反射RT，采样世界深度以保留前景遮挡。
// 边界：不关闭全局Bloom或曝光，不把镜面当发光体，不生成固定画面。
Shader "RoomDemo/Atelier/FinalMirror"
{
    Properties { _BaseMap("实时反射",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X_FLOAT(_MirrorSceneDepth);
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float4 screen:TEXCOORD0; float2 uv:TEXCOORD1; float eye:TEXCOORD2; };
            V Vert(A i)
            {
                V o;
                float3 world=TransformObjectToWorld(i.positionOS);
                o.positionCS=TransformWorldToHClip(world);
                o.screen=ComputeScreenPos(o.positionCS);
                o.eye=-TransformWorldToView(world).z;
                o.uv=i.uv;
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 screen=i.screen.xy/i.screen.w;
                float raw=SAMPLE_TEXTURE2D_X(_MirrorSceneDepth,sampler_PointClamp,screen).r;
                float scene=LinearEyeDepth(raw,_ZBufferParams);
                clip(scene+.008-i.eye);
                float3 x=max(0,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb)*.86*1.4142;
                // 镜面单独采用有限LDR色调映射，不把未映射亮度再次送入主镜头Bloom。
                float3 colour=saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14));
                return half4(colour,1);
            }
            ENDHLSL
        }
    }
}
