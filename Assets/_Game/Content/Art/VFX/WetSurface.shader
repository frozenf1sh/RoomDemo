// 文件：WetSurface.shader。职责：用局部暗色、表面细纹和轻微反光表现薄湿痕。
// 边界：专用于画框落滴，不用实时反射、通用透明 Lit 变体或真实流体模拟。
Shader "RoomDemo/WetSurface"
{
    Properties
    {
        [MainColor] _BaseColor("湿痕颜色", Color) = (.08,.11,.10,.6)
        _Smoothness("反光集中度", Range(0,1)) = .94
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // 极轻的静态细纹与视角反光，移动错觉由真实滴水节拍提供。
                float grain = sin(input.positionWS.x*36 + input.positionWS.z*29)*.006;
                float grazing = pow(1-saturate(abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),4);
                half3 color = _BaseColor.rgb + grain + grazing*_Smoothness*half3(.11,.14,.13);
                return half4(color,_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
