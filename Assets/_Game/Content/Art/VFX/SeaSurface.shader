Shader "RoomDemo/SeaSurface"
{
    Properties
    {
        _DeepColor("Deep sea", Color) = (0.025,0.19,0.27,1)
        _LightColor("Shallow sea", Color) = (0.08,0.60,0.66,1)
        _Speed("Wave speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _LightColor;
                float _Speed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _Speed;
                float wave = sin(input.uv.y * 37 + sin(input.uv.x * 12 + t) * 1.7 - t * 1.4);
                float swell = sin(input.uv.y * 13 - t * .7 + sin(input.uv.x * 8 + t * .3));
                half3 color = lerp(_DeepColor.rgb, _LightColor.rgb, saturate(.42 + swell * .25 + wave * .18));
                float foam = smoothstep(.93, 1, wave) * (.35 + .25 * sin(input.uv.x * 26 - t));
                return half4(lerp(color, half3(.80,.94,.91), saturate(foam)), 1);
            }
            ENDHLSL
        }
    }
}
