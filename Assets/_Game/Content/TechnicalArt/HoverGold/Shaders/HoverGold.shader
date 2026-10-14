// 外侧疏松游丝：只保留局部光线、明暗和渐变细尾，不叠内圈或闭合成整圈。
Shader "Hidden/RoomDemo/HoverGold"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "外轮廓几何采集"
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _HoverCaptureVP;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings MaskVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = mul(_HoverCaptureVP, float4(TransformObjectToWorld(input.positionOS.xyz), 1));
                return output;
            }
            half4 MaskFragment(Varyings input) : SV_Target { return half4(1, 0, 0, 1); }
            ENDHLSL
        }
        Pass
        {
            Name "疏松游丝与渐变细尾"
            Cull Off ZWrite On ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex StrokeVertex
            #pragma fragment StrokeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _BraidHalfWidth, _BraidOpacity, _BraidFlowDistance, _BraidTravelPhase;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 flow : TEXCOORD1;
                float3 segment : TEXCOORD2;
                half4 colour : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 flow : TEXCOORD1;
                float3 segment : TEXCOORD2;
                half4 colour : COLOR;
            };
            Varyings StrokeVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.flow = input.flow;
                output.segment = input.segment;
                output.colour = input.colour;
                return output;
            }
            half4 StrokeFragment(Varyings input) : SV_Target
            {
                // 整段金丝的中心、头尾和空段一起顺时针前进；接缝处连续回绕。
                float distance = frac(input.uv.y - input.segment.x - _BraidTravelPhase + .5) - .5;
                float span = distance < 0 ? input.segment.y : input.segment.z;
                float local = abs(distance) / max(.001, span);
                float envelope = 1 - smoothstep(.25, 1, local);
                // 线内的亮头与渐暗长尾继续沿同一方向流动。
                float phase = frac(input.flow.x - _BraidFlowDistance * input.flow.y);
                float pulse = smoothstep(.06, .68, phase) * (1 - smoothstep(.68, .9, phase));
                float bright = .26 + .74 * pulse;
                float strandAlpha = envelope * bright;
                // 中心线保持清晰；细尾收窄，柔光仅存在于明亮片段的近侧。
                float across = abs(input.uv.x);
                float halfWidth = _BraidHalfWidth * (.08 + .92 * sqrt(envelope));
                float aa = max(.4, fwidth(across) * .6);
                float coverage = 1 - smoothstep(halfWidth - aa, halfWidth + aa, across);
                float glow = .16 * exp(-pow(across / (halfWidth + .7), 2)) * strandAlpha * strandAlpha;
                float opacity = (coverage + glow) * _BraidOpacity * strandAlpha;
                clip(opacity - .003);
                float edge = smoothstep(halfWidth * .25, halfWidth, across);
                half3 gold = lerp(input.colour.rgb, half3(1, .828, .342), pulse * pulse * .65) * (1 - edge * .12);
                return half4(gold, saturate(opacity));
            }
            ENDHLSL
        }
        Pass
        {
            Name "最上层外侧光带"
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_HoverBraid);
            half4 CompositeFragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X(_HoverBraid, sampler_PointClamp, input.texcoord);
            }
            ENDHLSL
        }
    }
}
