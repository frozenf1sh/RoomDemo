// 文件：FixedViewSkyComposite.shader。职责：只在房间真实深度之后显示固定绘画画幅。
// 边界：输入已完成原 Hero 后处理，不再次调色，不覆盖房间与 Overlay UI。
Shader "Hidden/RoomDemo/Production/FixedViewSkyComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_FixedSkyDepth);
            TEXTURE2D_X(_FixedSkyTexture);
            float _Opacity;
            float4 _FrameScaleBias;
            half4 Fragment(Varyings i):SV_Target
            {
                float depth=SAMPLE_TEXTURE2D_X(_FixedSkyDepth,sampler_PointClamp,i.texcoord).r;
                #if UNITY_REVERSED_Z
                    clip(.00001-depth);
                #else
                    clip(depth-.99999);
                #endif
                float2 uv=i.texcoord*_FrameScaleBias.xy+_FrameScaleBias.zw;
                return half4(SAMPLE_TEXTURE2D_X(_FixedSkyTexture,sampler_LinearClamp,uv).rgb,_Opacity);
            }
            ENDHLSL
        }
    }
}
