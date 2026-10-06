// 文件：InfluenceComposite.shader。职责：用物体遮罩和世界区域混合冻结的普通／绘画帧。
// 区域不采样屏幕噪声，不移动材质 UV。物体遮罩遵守已有深度遮挡。
Shader "Hidden/RoomDemo/LivingPainting/InfluenceComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "LocalPainterlyComposite"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_X(_OriginalScene);
            TEXTURE2D_X(_ObjectInfluence);
            float _PainterlyIntensity;
            int _RegionCount;
            float4 _RegionCentres[16], _RegionRadii[16];

            half4 Composite(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float2 objectMask = SAMPLE_TEXTURE2D_X(_ObjectInfluence,sampler_LinearClamp,uv).rg;
                float strength = max(_PainterlyIntensity,objectMask.r);
                float d = SampleSceneDepth(uv);
                float3 world = ComputeWorldSpacePosition(uv,d,UNITY_MATRIX_I_VP);
                // 区域边缘固定在世界上，柔和地提高绘画比例，不覆盖原材质的色相。
                [loop] for (int n = 0; n < _RegionCount; n++)
                {
                    float r = length((world-_RegionCentres[n].xyz)/max(_RegionRadii[n].xyz,.001));
                    float influence = 1-smoothstep(1-_RegionRadii[n].w,1,r);
                    strength = max(strength,influence*_RegionCentres[n].w);
                }
                half3 original = SAMPLE_TEXTURE2D_X(_OriginalScene,sampler_LinearClamp,uv).rgb;
                half3 painted = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb;
                // 专门创作的细颜料线只保留自身材质，防止基础取色将其误采成相邻木纹。
                strength *= 1-objectMask.g;
                return half4(lerp(original,painted,saturate(strength)),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ObjectInfluenceMask"
            Cull Off ZWrite Off ZTest LEqual
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _InfluenceAmount, _KeepOriginal;
            struct Attributes { float3 positionOS : POSITION; };
            struct Output { float4 positionCS : SV_POSITION; };
            Output MaskVertex(Attributes a)
            {
                Output o;
                o.positionCS=TransformObjectToHClip(a.positionOS);
                return o;
            }
            half4 MaskFragment(Output i) : SV_Target { return half4(_InfluenceAmount,_KeepOriginal,0,0); }
            ENDHLSL
        }
    }
}
