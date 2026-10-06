// 文件：QuietWater.shader。职责：平静水面上的真实镜像，极小幅度方向涟漪。
// 不创建随机发光点；所有星光都来自镜像摄像机当前绘制的三维场景。
Shader "RoomDemo/LivingPainting/QuietWater"
{
    Properties
    {
        _BaseColor("深水颜色",Color)=(.018,.052,.077,1)
        _PlanarReflection("实时平面倒影",2D)="black"{}
        _VisualTime("演出时间",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PlanarReflection); SAMPLER(sampler_PlanarReflection);
            float4 _BaseColor;
            float _VisualTime;
            struct Input { float3 positionOS:POSITION; };
            struct Output { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            Output Vertex(Input i) { Output o; o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);return o; }
            half4 Fragment(Output i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float ripple=sin(i.world.z*12+i.world.x*.6-_VisualTime*.14)*.0008;
                ripple+=sin(i.world.z*28-i.world.x*.35-_VisualTime*.10)*.00045;
                float2 offset=float2(ripple*.65,ripple);
                // 细微纵向散射让星光的倒影柔和，波纹幅度不到画面千分之二。
                half3 reflected=SAMPLE_TEXTURE2D(_PlanarReflection,sampler_PlanarReflection,uv+offset).rgb*.50;
                reflected+=SAMPLE_TEXTURE2D(_PlanarReflection,sampler_PlanarReflection,uv+offset+float2(0,.0015)).rgb*.25;
                reflected+=SAMPLE_TEXTURE2D(_PlanarReflection,sampler_PlanarReflection,uv+offset-float2(0,.0015)).rgb*.25;
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=.43+.37*pow(1-saturate(view.y),3);
                return half4(lerp(_BaseColor.rgb,reflected,fresnel),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DepthNormalsOnly"
    }
}
